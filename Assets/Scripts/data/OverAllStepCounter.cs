using UnityEngine;
using System;
using System.Collections;
using System.IO;
using Repforge.StepCounterPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

public class OverallStepCounter : MonoBehaviour
{

    [Tooltip("Seconds between step polls. 0.2s provides faster updates while remaining practical — " +
             "lower values waste battery without gaining real-time accuracy because " +
             "the OS sensor batches data regardless of poll rate.")]
    public float refreshInterval = 0.2f;

    [Tooltip("Minimum step change before firing onStepsUpdated. Keep at 1.")]
    public int  stepChangeThreshold = 1;

    [Tooltip("Log every StepCounterRequest result to the console.")]
    public bool debugStepQueries = false;

    [Tooltip("Seconds between disk writes during play. Lower = safer on crash, slightly more I/O.")]
    public float diskSaveInterval = 10f;

    public StepData stepData;
    public int      overallSteps;
    public int      overallStepsBeforeToday;
    public bool     cloudLoaded = false;
    public string   stepDataJsonFilePath;

    [HideInInspector] public bool isLoggingOut = false;

    public static event Action          onLoaded;
    public static event Action<int,int> onStepsUpdated; // (overall, daily)

    private static OverallStepCounter instance;
    private Coroutine refreshCoroutine;

    private int  sessionGen = 0;

    private int  appOpenDeviceSteps = 0;
    private bool appOpenCaptured    = false;

    private int  signInDeviceSteps   = 0;
    private bool signedInThisSession = false;

    private int  savedDailyBase = 0;

    private bool beforeTodaySettled      = false;
    private bool waitingForCloudData     = false;
    private bool pendingLocalFireOnStart = false;
    private bool offsetRecalibrated      = false;

    [HideInInspector] public bool initializingFreshData = false;

    private bool  queryInFlight          = false;
    private float queryDispatchTime      = 0f;
    private const float QueryTimeout     = 5f;

    private int  lastKnownDeviceSteps    = 0;
    private bool lastKnownDeviceCaptured = false;

    private float lastDiskSaveTime = 0f;

    private int verbosePostCloudPolls = 0;

    private const string OverallOffsetKey = "OverallStepOffset";
    private const string DailyOffsetKey   = "DailyStepOffset";

    void Awake()
    {
        stepDataJsonFilePath = Application.persistentDataPath + "/stepData.json";

        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        CaptureAppOpenSteps();

        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
        {
            ResetStepDataCompletely();
            isLoggingOut = false;
            PlayerPrefs.SetInt("SuppressStepQuery", 1);
            PlayerPrefs.Save();
            InitializeStepDataAfterLogout();
            return;
        }

        if (IsSignedIn() && PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 0)
            waitingForCloudData = true;

        LoadStepData();
    }

    void Start()
    {
        Debug.Log($"[StepCounter] Start — HasLoggedOut={PlayerPrefs.GetInt("HasLoggedOut",0)}, " +
                  $"initializingFreshData={initializingFreshData}, IsSignedIn={IsSignedIn()}, " +
                  $"SuppressCloudRestore={PlayerPrefs.GetInt("SuppressCloudRestore",0)}, " +
                  $"SuppressStepQuery={PlayerPrefs.GetInt("SuppressStepQuery",0)}, " +
                  $"fileExists={File.Exists(stepDataJsonFilePath)}, " +
                  $"waitingForCloud={waitingForCloudData}");

        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1) return;
        if (initializingFreshData) return;

        if (pendingLocalFireOnStart)
        {
            pendingLocalFireOnStart = false;
            onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);
            onLoaded?.Invoke();
        }

        bool signedIn      = IsSignedIn();
        bool suppressCloud = PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 1;

        if (signedIn && !suppressCloud)
        {
            _ = LoadStepDataFromCloud();
            return; // FinalizeCloudLoad owns the loop from here
        }

        if (!cloudLoaded)
            GetOverallSteps();

        if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
            refreshCoroutine = StartCoroutine(RefreshLoop());
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus || isLoggingOut) return;

        if (IsSignedIn() && PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 0)
        {
            if (!cloudLoaded) _ = LoadStepDataFromCloud();
            else              GetOverallSteps();
        }

        if (PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0 && refreshCoroutine == null)
        {
            Debug.Log("[StepCounter] Regained focus — restarting RefreshLoop.");
            refreshCoroutine = StartCoroutine(RefreshLoop());
        }
    }

    async void OnApplicationQuit()
    {
        if (isLoggingOut) return;
        CommitCurrentStateToDisk();
        await SaveStepDataToCloud();
    }

    async void OnApplicationPause(bool isPaused)
    {
        if (isLoggingOut) return;

        if (isPaused)
        {
            CommitCurrentStateToDisk();
            await SaveStepDataToCloud();
        }
        else
        {
            // Resuming from background — restart loop if it stopped
            if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
            {
                Debug.Log("[StepCounter] Resuming — restarting RefreshLoop.");
                refreshCoroutine = StartCoroutine(RefreshLoop());
            }
        }
    }

    private void CommitCurrentStateToDisk()
    {
        if (isLoggingOut) return;
        stepData.lastSaveTime  = DateTime.Today.ToString("yyyy-MM-dd");
        stepData.numberOfSteps = overallSteps;
        stepData.overallSteps  = overallSteps;

        if (lastKnownDeviceCaptured)
        {
            int recalcDaily = CalcDailySteps(lastKnownDeviceSteps);
            if (recalcDaily >= 0)
            {
                stepData.dailySteps = recalcDaily;
                Debug.Log($"[EXIT] Daily recalculated: savedBase={savedDailyBase}, " +
                          $"device={lastKnownDeviceSteps}, appOpen={appOpenDeviceSteps}, " +
                          $"stepsSinceOpen={lastKnownDeviceSteps - appOpenDeviceSteps}, " +
                          $"daily={recalcDaily}");
            }
        }
        else if (savedDailyBase > 0 && stepData.dailySteps == 0)
        {
            stepData.dailySteps = savedDailyBase;
            Debug.Log($"[EXIT] No device reading — preserving savedDailyBase={savedDailyBase} as daily");
        }

        WriteToDisk();
        Debug.Log($"[EXIT] Committed — overall={overallSteps}, daily={stepData.dailySteps}");
    }

    public void GetOverallSteps()
    {
        if (isLoggingOut) return;
        if (queryInFlight) return;
        if (string.IsNullOrEmpty(stepData?.registrationTime) ||
            string.IsNullOrEmpty(stepData?.lastSaveTime)) return;

        int gen = sessionGen;

        if (beforeTodaySettled)
        {
            queryInFlight     = true;
            queryDispatchTime = Time.realtimeSinceStartup;
            QueryTodayAndUpdate(gen);
            return;
        }

        DateTime lastSave = DateTime.Parse(stepData.lastSaveTime).Date;
        int      days     = GetDaysSinceLastSave();

        if (days == 0)
        {
            overallStepsBeforeToday = stepData.numberOfSteps > 0
                ? stepData.numberOfSteps - GetEstimatedTodayStepsFromDisk() : 0;
            beforeTodaySettled = true;
            queryInFlight      = true;
            queryDispatchTime  = Time.realtimeSinceStartup;
            QueryTodayAndUpdate(gen);
        }
        else if (days == 1)
        {
            savedDailyBase          = 0;
            overallStepsBeforeToday = stepData.numberOfSteps;
            beforeTodaySettled      = true;
            queryInFlight           = true;
            queryDispatchTime       = Time.realtimeSinceStartup;
            QueryTodayAndUpdate(gen);
        }
        else if (days <= 10)
        {
            int snapshot = stepData.numberOfSteps;
            new StepCounterRequest().From(lastSave).To(DateTime.Today).OnQuerySuccess((range) =>
            {
                if (IsStale(gen)) return;
                overallStepsBeforeToday = snapshot + range;
                beforeTodaySettled      = true;
                queryInFlight           = true;
                queryDispatchTime       = Time.realtimeSinceStartup;
                QueryTodayAndUpdate(gen);
            }).Execute();
        }
        else
        {
            new StepCounterRequest().From(DateTime.Today.AddDays(-days)).To(DateTime.Today).OnQuerySuccess((range) =>
            {
                if (IsStale(gen)) return;
                overallStepsBeforeToday = range;
                beforeTodaySettled      = true;
                queryInFlight           = true;
                queryDispatchTime       = Time.realtimeSinceStartup;
                QueryTodayAndUpdate(gen);
            }).Execute();
        }
    }

    private void QueryTodayAndUpdate(int gen)
    {
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            // Always clear in-flight first so the next poll is never permanently blocked
            queryInFlight = false;

            if (!isLoggingOut)
            {
                lastKnownDeviceSteps    = deviceNow;
                lastKnownDeviceCaptured = true;
            }

            if (IsStale(gen) || isLoggingOut) return;

            // Recalibrate OverallOffsetKey once on same-day disk restore
            if (!offsetRecalibrated && !signedInThisSession && stepData.baselineSteps == 0)
            {
                offsetRecalibrated = true;
                int todayAlreadyRecorded = Math.Max(0, overallSteps - overallStepsBeforeToday);
                int newOffset            = Math.Max(0, deviceNow - todayAlreadyRecorded);
                PlayerPrefs.SetInt(OverallOffsetKey, newOffset);
                PlayerPrefs.Save();
                Debug.Log($"[RECAL] Offset recalibrated: deviceNow={deviceNow}, todayRecorded={todayAlreadyRecorded}, offset={newOffset}");
            }

            int prev     = overallSteps;
            int todayNet = CalcTodayNetSteps(deviceNow);
            overallSteps = overallStepsBeforeToday + todayNet;
            int daily    = CalcDailySteps(deviceNow);

            int overallDelta = Math.Abs(overallSteps - prev);
            int dailyDelta   = Math.Abs(daily - stepData.dailySteps);

            // Verbose post-cloud logging so you can verify daily is counting
            bool verbose = debugStepQueries || verbosePostCloudPolls > 0;
            if (verbosePostCloudPolls > 0) verbosePostCloudPolls--;

            if (verbose)
                Debug.Log($"[Steps] overall={overallSteps}(Δ{overallDelta}), " +
                          $"daily={daily}(Δ{dailyDelta}), " +
                          $"device={deviceNow}, appOpen={appOpenDeviceSteps}, " +
                          $"savedBase={savedDailyBase}, " +
                          $"stepsSinceOpen={deviceNow - appOpenDeviceSteps}");

            if (overallDelta >= stepChangeThreshold || dailyDelta >= stepChangeThreshold)
                onStepsUpdated?.Invoke(overallSteps, daily);

            stepData.dailySteps = daily;
            SaveStepData(deviceNow, gen);
        }).Execute();
    }


    private IEnumerator RefreshLoop()
    {
        GetOverallSteps();
        while (true)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, refreshInterval));

            // Watchdog: clear a stuck in-flight flag so the loop never permanently stops
            if (queryInFlight && (Time.realtimeSinceStartup - queryDispatchTime) > QueryTimeout)
            {
                Debug.LogWarning("[StepCounter] Query timed out — clearing. Check ACTIVITY_RECOGNITION permission.");
                queryInFlight = false;
            }

            GetOverallSteps();
        }
    }

    public void SetRefreshInterval(float seconds)
    {
        refreshInterval = Mathf.Max(0.1f, seconds);
        StopRefreshCoroutine();
        refreshCoroutine = StartCoroutine(RefreshLoop());
    }


    private int CalcTodayNetSteps(int deviceNow)
    {
        if (stepData.baselineSteps > 0)
            return Math.Max(0, deviceNow - stepData.baselineSteps);

        if (signedInThisSession)
            return Math.Max(0, deviceNow - signInDeviceSteps);

        return Math.Max(0, deviceNow - PlayerPrefs.GetInt(OverallOffsetKey, 0));
    }

    private int CalcDailySteps(int deviceNow)
    {
        if (stepData.baselineSteps > 0)
        {
            int result = Math.Max(0, deviceNow - stepData.baselineSteps);
            if (debugStepQueries)
                Debug.Log($"[Daily][baseline] device={deviceNow}, baseline={stepData.baselineSteps}, daily={result}");
            return result;
        }

        int stepsSinceOpen = appOpenCaptured
            ? Math.Max(0, deviceNow - appOpenDeviceSteps)
            : 0;

        int daily = savedDailyBase + stepsSinceOpen;

        if (debugStepQueries)
            Debug.Log($"[Daily] device={deviceNow}, appOpen={appOpenDeviceSteps}, " +
                      $"stepsSinceOpen={stepsSinceOpen}, base={savedDailyBase}, daily={daily}");
        return daily;
    }


    public void SaveStepData(int todayDeviceSteps, int gen = -1, bool forceWrite = false)
    {
        if (isLoggingOut) return;
        if (gen >= 0 && IsStale(gen)) return;

        stepData.lastSaveTime  = DateTime.Today.ToString("yyyy-MM-dd");
        stepData.numberOfSteps = overallSteps;
        stepData.overallSteps  = overallSteps;
        stepData.dailySteps    = CalcDailySteps(todayDeviceSteps);

        float now = Time.realtimeSinceStartup;
        if (forceWrite || (now - lastDiskSaveTime) >= diskSaveInterval)
        {
            lastDiskSaveTime = now;
            WriteToDisk();
        }
    }

    public async Task SaveStepDataToCloud()
    {
        if (isLoggingOut) return;
        CommitCurrentStateToDisk();

        await CloudSaver.SaveDataToCloud("stepData", stepData);
        Debug.Log($"[CLOUD SAVE] overall={stepData.overallSteps}, daily={stepData.dailySteps}");
    }


    public async Task LoadStepDataFromCloud()
    {
        if (isLoggingOut) return;

        int localDailyBeforeCloud = stepData?.dailySteps ?? 0;

        if (PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 1)
        {
            bool newSession = IsSignedIn() && PlayerPrefs.GetInt("HasLoggedOut", 0) == 0;
            if (newSession)
            {
                PlayerPrefs.DeleteKey("SuppressCloudRestore");
                PlayerPrefs.Save();
            }
            else
            {
                Debug.LogWarning("[CLOUD] Suppressed — waiting for new player sign-in.");
                return;
            }
        }

        if (!signedInThisSession && appOpenCaptured)
        {
            signInDeviceSteps   = appOpenDeviceSteps;
            signedInThisSession = true;
        }

        int gen = sessionGen;
        try
        {
            var waitTask = appOpenTcs.Task;
            if (await Task.WhenAny(waitTask, Task.Delay(5000)) != waitTask)
                Debug.LogWarning("[CLOUD] appOpenTcs timed out — proceeding without app-open capture.");

            string json = await CloudSaver.LoadDataFromCloud("stepData");
            if (IsStale(gen) || isLoggingOut) return;

            int  preservedBaseline = stepData?.baselineSteps ?? 0;

            stepData = JsonUtility.FromJson<StepData>(json);
            Debug.Log($"[CLOUD] Raw: overall={stepData.overallSteps}, daily={stepData.dailySteps}, last={stepData.lastSaveTime}");

            if (preservedBaseline > 0)
            {
                stepData.baselineSteps = preservedBaseline;
                Debug.Log($"[CLOUD] Restored baseline: {preservedBaseline}");
            }

            DateTime cloudDate = DateTime.Parse(stepData.lastSaveTime).Date;
            int      daysSince = (DateTime.Today - cloudDate).Days;
            int      cloudBase = stepData.overallSteps != 0 ? stepData.overallSteps : stepData.numberOfSteps;

            // ── DIAGNOSTIC: confirm which path and what daily value cloud has ──────
            Debug.Log($"[CLOUD] daysSince={daysSince}, cloudBase={cloudBase}, " +
                      $"cloudDaily={stepData.dailySteps}, " +
                      $"cloudDate={cloudDate:yyyy-MM-dd}, today={DateTime.Today:yyyy-MM-dd}");
            Debug.Log($"[CLOUD] Path → {(daysSince == 0 ? "SameDay (daily preserved)" : daysSince == 1 ? "NewDay (daily=0)" : "MultiDay (daily=0)")}");

            if      (daysSince == 0) ApplyCloudSameDay(cloudBase, gen, localDailyBeforeCloud);
            else if (daysSince == 1) ApplyCloudNewDay(cloudBase, gen);
            else                     ApplyCloudMultiDayGap(cloudBase, cloudDate, gen);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CLOUD] Load failed: {e.Message}");
            waitingForCloudData = false;
            cloudLoaded         = false;

            if (stepData != null) { onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps); onLoaded?.Invoke(); }
            else LoadStepData();

            if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
                refreshCoroutine = StartCoroutine(RefreshLoop());
        }
    }

    private void ApplyCloudSameDay(int cloudBase, int gen, int localDailyBeforeCloud)
    {
        int cloudSavedDaily = stepData.dailySteps;

        if (cloudSavedDaily == 0 && stepData != null)
        {
            if (savedDailyBase > 0)
            {
                cloudSavedDaily = savedDailyBase;
                Debug.Log($"[CLOUD SameDay] cloudDaily was 0 — using local savedDailyBase={savedDailyBase} as fallback");
            }
        }

        int localDailyAtSignIn = Math.Max(0, localDailyBeforeCloud);
        int preCloudAppOpen    = appOpenCaptured ? appOpenDeviceSteps : -1;

        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            if (IsStale(gen) || isLoggingOut) return;

            PlayerPrefs.SetInt(OverallOffsetKey, deviceNow);
            PlayerPrefs.Save();

            if (!appOpenCaptured)
                Debug.LogWarning("[CLOUD] appOpenDeviceSteps not captured — using deviceNow as fallback.");

            appOpenDeviceSteps = deviceNow;
            appOpenCaptured    = true;

            overallSteps            = cloudBase;
            overallStepsBeforeToday = cloudBase;
            beforeTodaySettled      = true;

            int offlineSteps = preCloudAppOpen >= 0 ? Math.Max(0, deviceNow - preCloudAppOpen) : 0;
            int effectiveBase = Math.Max(cloudSavedDaily, localDailyAtSignIn);

            savedDailyBase      = effectiveBase;
            stepData.dailySteps = effectiveBase + offlineSteps;

            Debug.Log($"[CLOUD SameDay] cloudDaily={cloudSavedDaily}, localDaily={localDailyAtSignIn}, " +
                      $"effectiveBase={effectiveBase}, deviceNow={deviceNow}, appOpen={appOpenDeviceSteps}, " +
                      $"offlineSteps={offlineSteps}, finalDaily={stepData.dailySteps}");

            FinalizeCloudLoad();
        }).Execute();
    }

    private void ApplyCloudNewDay(int cloudBase, int gen)
    {
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            if (IsStale(gen) || isLoggingOut) return;

            PlayerPrefs.SetInt(OverallOffsetKey, deviceNow);
            PlayerPrefs.Save();

            appOpenDeviceSteps = deviceNow;
            appOpenCaptured    = true;

            overallSteps            = cloudBase;
            overallStepsBeforeToday = cloudBase;
            beforeTodaySettled      = true;

            int todaySteps = Math.Max(0, deviceNow);
            savedDailyBase       = todaySteps;
            stepData.dailySteps  = todaySteps;

            FinalizeCloudLoad();
        }).Execute();
    }

    private void ApplyCloudMultiDayGap(int cloudBase, DateTime cloudDate, int gen)
    {
        new StepCounterRequest().From(cloudDate).To(DateTime.Today).OnQuerySuccess((range) =>
        {
            if (IsStale(gen) || isLoggingOut) return;
            int accumulated = cloudBase + range;

            new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
            {
                if (IsStale(gen) || isLoggingOut) return;

                PlayerPrefs.SetInt(OverallOffsetKey, deviceNow);
                PlayerPrefs.Save();

                appOpenDeviceSteps = deviceNow;
                appOpenCaptured    = true;

                overallSteps            = accumulated;
                overallStepsBeforeToday = accumulated;
                beforeTodaySettled      = true;

                int todaySteps = Math.Max(0, deviceNow);
                savedDailyBase       = todaySteps;
                stepData.dailySteps  = todaySteps;

                FinalizeCloudLoad();
            }).Execute();
        }).Execute();
    }

    private void FinalizeCloudLoad()
    {
        if (isLoggingOut) return;

        stepData.lastSaveTime  = DateTime.Today.ToString("yyyy-MM-dd");
        stepData.numberOfSteps = overallSteps;
        stepData.overallSteps  = overallSteps;

        WriteToDisk();

        PlayerPrefs.SetInt("CloudRestored", 1);
        PlayerPrefs.SetInt("HasEverSignedIn", 1);
        PlayerPrefs.DeleteKey("HasLoggedOut");
        PlayerPrefs.DeleteKey("SuppressStepQuery");
        PlayerPrefs.DeleteKey("SuppressCloudRestore");
        PlayerPrefs.Save();

        cloudLoaded          = true;
        waitingForCloudData  = false;
        offsetRecalibrated   = true;
        verbosePostCloudPolls = 10; // Log the next 10 polls 

        signedInThisSession = false;

        onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);
        onLoaded?.Invoke();

        // Restart the loop with the settled cloud values
        StopRefreshCoroutine();
        refreshCoroutine = StartCoroutine(RefreshLoop());

        Debug.Log($"[CLOUD] Finalized — overall={overallSteps}, daily={stepData.dailySteps}, " +
                  $"savedDailyBase={savedDailyBase}, appOpen={appOpenDeviceSteps}, " +
                  $"signedInThisSession={signedInThisSession}, " +
                  $"OverallOffsetKey={PlayerPrefs.GetInt(OverallOffsetKey, 0)}");
        Debug.Log("[CLOUD] Daily formula going forward: " +
                  $"savedDailyBase({savedDailyBase}) + (deviceNow - appOpen({appOpenDeviceSteps}))");
        Debug.Log("[CLOUD] As player walks: daily will increase by (newDeviceCount - " +
                  $"{appOpenDeviceSteps}). If this stays 0, appOpenDeviceSteps is wrong.");
    }

    public async void LoadFromCloudButton()
    {
        if (PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 1)
        { Debug.LogWarning("[CLOUD] Suppressed."); return; }
        if (!IsSignedIn())
        { Debug.LogWarning("[CLOUD] Not signed in."); return; }
        try { await LoadStepDataFromCloud(); }
        catch (Exception e) { Debug.LogError($"[CLOUD] Manual load failed: {e.Message}"); }
    }


    public void LoadStepData()
    {
        if (PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 1)
        { ZeroState(); FireZero(); return; }

        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
        { ZeroState(); FireZero(); return; }

        if (!File.Exists(stepDataJsonFilePath))
        {
            initializingFreshData = true;
            InitializeStepData();
            return;
        }

        stepData = JsonUtility.FromJson<StepData>(File.ReadAllText(stepDataJsonFilePath)) ?? new StepData();

        overallSteps            = stepData.overallSteps != 0 ? stepData.overallSteps : stepData.numberOfSteps;
        overallStepsBeforeToday = stepData.numberOfSteps;

        bool isNewDay = !string.IsNullOrEmpty(stepData.lastSaveTime) &&
                        DateTime.Parse(stepData.lastSaveTime).Date < DateTime.Today;
        savedDailyBase = isNewDay ? 0 : stepData.dailySteps;

        if (isNewDay)
        {
            overallStepsBeforeToday = stepData.numberOfSteps;
            beforeTodaySettled      = false;
        }

        Debug.Log($"[LOAD] overall={overallSteps}, daily={stepData.dailySteps}, " +
                  $"savedDailyBase={savedDailyBase}, newDay={isNewDay}");

        pendingLocalFireOnStart = true;

        if (waitingForCloudData) return;

        bool cloudRestored = PlayerPrefs.GetInt("CloudRestored", 0) == 1;
        if (!cloudRestored && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
            GetOverallSteps();

        if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
            refreshCoroutine = StartCoroutine(RefreshLoop());
    }

    public void InitializeStepData()
    {
        stepData = new StepData
        {
            registrationTime = DateTime.Today.ToString("yyyy-MM-dd"),
            lastSaveTime     = DateTime.Today.ToString("yyyy-MM-dd")
        };
        overallSteps            = 0;
        overallStepsBeforeToday = 0;
        savedDailyBase          = 0;
        stepData.baselineSteps  = 0;

        onStepsUpdated?.Invoke(0, 0);

        int gen = sessionGen;
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            if (IsStale(gen)) return;
            initializingFreshData = false;

            PlayerPrefs.SetInt(OverallOffsetKey, deviceNow);
            PlayerPrefs.SetInt("HasEverSignedIn", 1);
            PlayerPrefs.Save();

            beforeTodaySettled = true;
            onLoaded?.Invoke();

            if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
                refreshCoroutine = StartCoroutine(RefreshLoop());
        }).Execute();
    }

    public void InitializeStepDataAfterLogout()
    {
        stepData = new StepData
        {
            registrationTime = DateTime.Today.ToString("yyyy-MM-dd"),
            lastSaveTime     = DateTime.Today.ToString("yyyy-MM-dd")
        };
        overallSteps   = 0;
        savedDailyBase = 0;
        onStepsUpdated?.Invoke(0, 0);

        int gen = sessionGen;
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            if (IsStale(gen)) return;

            stepData.baselineSteps = deviceNow;
            appOpenDeviceSteps     = deviceNow;
            appOpenCaptured        = true;
            beforeTodaySettled     = true;

            PlayerPrefs.DeleteKey("HasLoggedOut");
            PlayerPrefs.DeleteKey("SuppressStepQuery");
            PlayerPrefs.Save();

            onStepsUpdated?.Invoke(0, 0);

            if (refreshCoroutine == null)
                refreshCoroutine = StartCoroutine(RefreshLoop());

            Debug.Log($"[LOGOUT INIT] Baseline set: {deviceNow}. HasLoggedOut cleared.");
        }).Execute();
    }

    public void ResetStepDataCompletely()
    {
        sessionGen++;
        Debug.Log($"[RESET] Gen → {sessionGen}. All in-flight callbacks invalidated.");

        isLoggingOut            = true;
        stepData                = new StepData();
        overallSteps            = 0;
        overallStepsBeforeToday = 0;
        cloudLoaded             = false;
        beforeTodaySettled      = false;
        waitingForCloudData     = false;
        savedDailyBase          = 0;
        appOpenDeviceSteps      = 0;
        appOpenCaptured         = false;
        signInDeviceSteps       = 0;
        signedInThisSession     = false;
        pendingLocalFireOnStart = false;
        initializingFreshData   = false;
        offsetRecalibrated      = false;
        queryInFlight           = false;
        lastKnownDeviceSteps    = 0;
        lastKnownDeviceCaptured = false;
        appOpenTcs              = new TaskCompletionSource<int>();

        StopRefreshCoroutine();

        if (File.Exists(stepDataJsonFilePath)) File.Delete(stepDataJsonFilePath);

        PlayerPrefs.DeleteKey(OverallOffsetKey);
        PlayerPrefs.DeleteKey(DailyOffsetKey);
        PlayerPrefs.DeleteKey("SuppressStepQuery");
        PlayerPrefs.Save();

        onStepsUpdated?.Invoke(0, 0);
    }

    public int GetDaysSinceLastSave()
    {
        if (string.IsNullOrEmpty(stepData?.lastSaveTime)) return 0;
        return (DateTime.Today - DateTime.Parse(stepData.lastSaveTime).Date).Days;
    }

    public int GetDaysSinceRegistration()
    {
        if (string.IsNullOrEmpty(stepData?.registrationTime)) return 0;
        return (DateTime.Today - DateTime.Parse(stepData.registrationTime).Date).Days;
    }

    private bool IsSignedIn() =>
        UnityServices.State == ServicesInitializationState.Initialized &&
        AuthenticationService.Instance != null &&
        AuthenticationService.Instance.IsSignedIn;

    private bool IsStale(int capturedGen)
    {
        if (capturedGen == sessionGen) return false;
        if (debugStepQueries) Debug.Log($"[STALE] Discarded (gen {capturedGen} vs {sessionGen})");
        return true;
    }

    private void ValidateBaseline(int deviceNow)
    {
        if (stepData.baselineSteps > 0 && deviceNow < stepData.baselineSteps)
        {
            Debug.LogWarning($"[Steps] Device ({deviceNow}) < baseline — clearing.");
            stepData.baselineSteps = 0;
        }
    }

    private int GetEstimatedTodayStepsFromDisk() => stepData.dailySteps;


    private TaskCompletionSource<int> appOpenTcs = new TaskCompletionSource<int>();

    private void CaptureAppOpenSteps()
    {
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((n) =>
        {
            if (!appOpenCaptured)
            {
                appOpenDeviceSteps = n;
                appOpenCaptured    = true;
                Debug.Log($"[SESSION] App-open steps captured: {n}");
            }
            if (!lastKnownDeviceCaptured)
            {
                lastKnownDeviceSteps    = n;
                lastKnownDeviceCaptured = true;
            }
            appOpenTcs.TrySetResult(n);
        }).Execute();
    }

    private void WriteToDisk()
    {
        if (isLoggingOut) return;
        File.WriteAllText(stepDataJsonFilePath, JsonUtility.ToJson(stepData));
        Debug.Log($"[DISK] overall={stepData.overallSteps}, daily={stepData.dailySteps}");
    }

    private void ZeroState()
    {
        stepData                = new StepData();
        overallSteps            = 0;
        overallStepsBeforeToday = 0;
        beforeTodaySettled      = false;
        waitingForCloudData     = false;
        savedDailyBase          = 0;
        appOpenDeviceSteps      = 0;
        appOpenCaptured         = false;
        signInDeviceSteps       = 0;
        signedInThisSession     = false;
        pendingLocalFireOnStart = false;
        offsetRecalibrated      = false;
        queryInFlight           = false;
        lastKnownDeviceSteps    = 0;
        lastKnownDeviceCaptured = false;
        appOpenTcs              = new TaskCompletionSource<int>();
        StopRefreshCoroutine();
    }

    private void FireZero()
    {
        onStepsUpdated?.Invoke(0, 0);
        onLoaded?.Invoke();
    }

    private void StopRefreshCoroutine()
    {
        if (refreshCoroutine == null) return;
        StopCoroutine(refreshCoroutine);
        refreshCoroutine = null;
    }
}