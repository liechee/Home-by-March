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
    [Tooltip("Seconds between step polls.")]
    public float refreshInterval = 0.5f;

    [Tooltip("Minimum step change before firing onStepsUpdated. Keep at 1.")]
    public int stepChangeThreshold = 1;

    [Tooltip("Log every StepCounterRequest result to the console.")]
    public bool debugStepQueries = false;

    [Tooltip("Seconds between disk writes during play.")]
    public float diskSaveInterval = 10f;

    public StepData stepData;
    public int overallSteps;
    public int overallStepsBeforeToday;
    public bool cloudLoaded = false;
    public string stepDataJsonFilePath;

    [HideInInspector] public bool isLoggingOut = false;

    public static event Action onLoaded;
    public static event Action<int, int> onStepsUpdated; // (overall, daily)

    private static OverallStepCounter instance;
    private Coroutine refreshCoroutine;

    private int sessionGen = 0;

    private int appOpenDeviceSteps = 0;
    private bool appOpenCaptured = false;

    private int signInDeviceSteps = 0;
    private bool signedInThisSession = false;

    private int savedDailyBase = 0;

    private bool beforeTodaySettled = false;
    private bool waitingForCloudData = false;
    private bool pendingLocalFireOnStart = false;
    private bool offsetRecalibrated = false;

    [HideInInspector] public bool initializingFreshData = false;

    private bool queryInFlight = false;
    private float queryDispatchTime = 0f;
    private const float QueryTimeout = 2f;

    private int lastKnownDeviceSteps = 0;
    private bool lastKnownDeviceCaptured = false;

    private float lastDiskSaveTime = 0f;

    private int verbosePostCloudPolls = 0;

    private const string OverallOffsetKey = "OverallStepOffset";
    private const string DailyOffsetKey = "DailyStepOffset";
    private bool backgroundCollectionInitialized = false;

    private TaskCompletionSource<int> appOpenTcs = new TaskCompletionSource<int>();

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        stepDataJsonFilePath = Application.persistentDataPath + "/stepData.json";
        lastDiskSaveTime = Time.realtimeSinceStartup - Mathf.Max(0.1f, diskSaveInterval);

        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        if (AuthManager.Instance != null)
            AuthManager.Instance.OnStateChanged += OnAuthStateChanged;

        EnsureBackgroundStepCollection();
        CaptureAppOpenSteps();

        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
        {
            //ResetStepDataCompletely();
            isLoggingOut = false;
            PlayerPrefs.SetInt("SuppressStepQuery", 1);
            PlayerPrefs.Save();
            InitializeStepDataAfterLogout();
            return;
        }

        LoadStepData();
    }

    private void OnDestroy()
    {
        if (AuthManager.Instance != null)
            AuthManager.Instance.OnStateChanged -= OnAuthStateChanged;
    }

    void Start()
    {
        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1) return;
        if (initializingFreshData) return;
        StartCoroutine(WaitForAuthThenLoad());
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus || isLoggingOut) return;

        if (IsSignedIn() && PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 0)
        {
            if (!cloudLoaded) _ = LoadStepDataFromCloud();
            else GetOverallSteps();
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
            if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
            {
                Debug.Log("[StepCounter] Resuming — restarting RefreshLoop.");
                refreshCoroutine = StartCoroutine(RefreshLoop());
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Auth
    // ─────────────────────────────────────────────────────────────────────────

    private async void OnAuthStateChanged()
    {
        if (isLoggingOut) return;
        if (!IsSignedIn()) return;
        if (PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 1) return;
        if (cloudLoaded) return;
        if (AuthManager.Instance != null && AuthManager.Instance.IsGuest) return;

        Debug.Log("[StepCounter] OnAuthStateChanged — account sign-in confirmed, loading from cloud.");
        await LoadStepDataFromCloud();
    }

    private IEnumerator WaitForAuthThenLoad()
    {
        const float timeout = 10f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (cloudLoaded) yield break;
            if (AuthManager.Instance != null && AuthManager.Instance.IsReady) break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log($"[StepCounter] WaitForAuthThenLoad — elapsed={elapsed:F2}s, " +
                  $"IsReady={AuthManager.Instance?.IsReady}, IsSignedIn={IsSignedIn()}, " +
                  $"cloudLoaded={cloudLoaded}, " +
                  $"LastLoginMethod={PlayerPrefs.GetString("LastLoginMethod", "none")}, " +
                  $"PlayerSignedIn={PlayerPrefs.GetInt("PlayerSignedIn", 0)}, " +
                  $"SuppressCloudRestore={PlayerPrefs.GetInt("SuppressCloudRestore", 0)}");

        if (cloudLoaded) yield break;

        bool suppressCloud = PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 1;
        bool isGuest = AuthManager.Instance != null && AuthManager.Instance.IsGuest;
        bool isAccountSession = !isGuest &&
                                (IsSignedIn() || PlayerPrefs.GetInt("PlayerSignedIn", 0) == 1) &&
                                PlayerPrefs.GetString("LastLoginMethod", "") == "UsernamePassword";

        if (isAccountSession && !suppressCloud)
        {
            Debug.Log("[StepCounter] Auth settled — loading step data from cloud.");
            _ = LoadStepDataFromCloud();
            yield break;
        }

        // Guest or not signed in — go offline immediately.
        if (pendingLocalFireOnStart)
        {
            pendingLocalFireOnStart = false;
            onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);
            onLoaded?.Invoke();
        }

        if (!cloudLoaded)
            GetOverallSteps();

        if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
            refreshCoroutine = StartCoroutine(RefreshLoop());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Background collection
    // ─────────────────────────────────────────────────────────────────────────

    private void EnsureBackgroundStepCollection()
    {
        if (backgroundCollectionInitialized) return;
        if (PlayerPrefs.GetInt("SuppressStepQuery", 0) == 1) return;

        backgroundCollectionInitialized = true;

        new StepCounterRequest()
            .OnPermissionGranted(() =>
            {
                new StepCounterRequest().Enable();
                if (debugStepQueries)
                    Debug.Log("[StepCounter] Background step collection enabled.");
            })
            .OnPermissionDenied(() =>
            {
                Debug.LogWarning("[StepCounter] Activity recognition permission denied.");
            })
            .RequestPermission();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Disk commit
    // ─────────────────────────────────────────────────────────────────────────

    private void CommitCurrentStateToDisk()
    {
        if (isLoggingOut) return;
        if (!beforeTodaySettled) return;

        stepData.lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd");
        stepData.numberOfSteps = overallSteps;
        stepData.overallSteps = overallSteps;
        stepData.stepsBeforeToday = overallStepsBeforeToday;

        if (lastKnownDeviceCaptured)
        {
            int recalcDaily = CalcDailySteps(lastKnownDeviceSteps);
            // FIX: clamp daily — can never exceed overall or raw device today count
            stepData.dailySteps = Mathf.Clamp(recalcDaily, 0, overallSteps);
            Debug.Log($"[EXIT] Daily recalculated: savedBase={savedDailyBase}, " +
                      $"device={lastKnownDeviceSteps}, appOpen={appOpenDeviceSteps}, " +
                      $"stepsSinceOpen={lastKnownDeviceSteps - appOpenDeviceSteps}, " +
                      $"daily={stepData.dailySteps}");
        }
        else if (savedDailyBase > 0 && stepData.dailySteps == 0)
        {
            stepData.dailySteps = Math.Min(savedDailyBase, overallSteps);
            Debug.Log($"[EXIT] No device reading — preserving savedDailyBase={savedDailyBase} as daily");
        }

        WriteToDisk();
        Debug.Log($"[EXIT] Committed — overall={overallSteps}, daily={stepData.dailySteps}, stepsBeforeToday={overallStepsBeforeToday}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Step polling
    // ─────────────────────────────────────────────────────────────────────────

    public void GetOverallSteps()
    {
        if (isLoggingOut) return;
        if (queryInFlight) return;
        if (string.IsNullOrEmpty(stepData?.registrationTime) ||
            string.IsNullOrEmpty(stepData?.lastSaveTime)) return;

        int gen = sessionGen;

        if (beforeTodaySettled)
        {
            queryInFlight = true;
            queryDispatchTime = Time.realtimeSinceStartup;
            QueryTodayAndUpdate(gen);
            return;
        }

        DateTime lastSave = DateTime.Parse(stepData.lastSaveTime).Date;
        int days = GetDaysSinceLastSave();

        if (days == 0)
        {
            // FIX: use persisted stepsBeforeToday if available, not subtraction
            if (stepData.stepsBeforeToday > 0)
                overallStepsBeforeToday = stepData.stepsBeforeToday;
            else
                overallStepsBeforeToday = Math.Max(0, overallSteps);

            beforeTodaySettled = true;
            queryInFlight = true;
            queryDispatchTime = Time.realtimeSinceStartup;
            QueryTodayAndUpdate(gen);
        }
        else if (days == 1)
        {
            savedDailyBase = 0;
            overallStepsBeforeToday = stepData.numberOfSteps;
            beforeTodaySettled = true;
            queryInFlight = true;
            queryDispatchTime = Time.realtimeSinceStartup;
            QueryTodayAndUpdate(gen);
        }
        else if (days <= 10)
        {
            int snapshot = stepData.numberOfSteps;
            new StepCounterRequest().From(lastSave).To(DateTime.Today).OnQuerySuccess((range) =>
            {
                if (IsStale(gen)) return;
                overallStepsBeforeToday = snapshot + range;
                beforeTodaySettled = true;
                queryInFlight = true;
                queryDispatchTime = Time.realtimeSinceStartup;
                QueryTodayAndUpdate(gen);
            }).Execute();
        }
        else
        {
            new StepCounterRequest().From(DateTime.Today.AddDays(-days)).To(DateTime.Today).OnQuerySuccess((range) =>
            {
                if (IsStale(gen)) return;
                overallStepsBeforeToday = range;
                beforeTodaySettled = true;
                queryInFlight = true;
                queryDispatchTime = Time.realtimeSinceStartup;
                QueryTodayAndUpdate(gen);
            }).Execute();
        }
    }

    private void QueryTodayAndUpdate(int gen)
    {
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            queryInFlight = false;

            if (!isLoggingOut)
            {
                lastKnownDeviceSteps = deviceNow;
                lastKnownDeviceCaptured = true;
            }

            if (IsStale(gen) || isLoggingOut) return;

            // Recalibrate OverallOffsetKey once on same-day disk restore
            if (!offsetRecalibrated && !signedInThisSession && stepData.baselineSteps == 0)
            {
                offsetRecalibrated = true;
                int todayAlreadyRecorded = Math.Max(0, overallSteps);
                int newOffset = Math.Max(0, deviceNow - todayAlreadyRecorded);
                PlayerPrefs.SetInt(OverallOffsetKey, newOffset);
                PlayerPrefs.Save();
                Debug.Log($"[RECAL] Offset recalibrated: deviceNow={deviceNow}, todayRecorded={todayAlreadyRecorded}, offset={newOffset}");
            }

            int prev = overallSteps;
            int todayNet = CalcTodayNetSteps(deviceNow);
            overallSteps = overallStepsBeforeToday + todayNet;
            int daily = CalcDailySteps(deviceNow);

            // FIX: clamp daily at point of calculation too
            daily = Mathf.Clamp(daily, 0, overallSteps);

            int overallDelta = Math.Abs(overallSteps - prev);
            int dailyDelta = Math.Abs(daily - stepData.dailySteps);

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
            SaveStepData(deviceNow, gen, dailyOverride: daily);
        }).Execute();
    }

    private IEnumerator RefreshLoop()
    {
        while (true)
        {
            GetOverallSteps();
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, refreshInterval));

            if (queryInFlight && (Time.realtimeSinceStartup - queryDispatchTime) > QueryTimeout)
            {
                Debug.LogWarning("[StepCounter] Query timed out — clearing in-flight flag and retrying.");
                queryInFlight = false;
                if (!appOpenCaptured)
                {
                    appOpenTcs = new TaskCompletionSource<int>();
                    CaptureAppOpenSteps();
                }
            }
        }
    }

    public void SetRefreshInterval(float seconds)
    {
        refreshInterval = Mathf.Max(0.1f, seconds);
        StopRefreshCoroutine();
        refreshCoroutine = StartCoroutine(RefreshLoop());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Step calculation
    // ─────────────────────────────────────────────────────────────────────────

    private int CalcTodayNetSteps(int deviceNow)
    {
        if (stepData.baselineSteps > 0)
            return Math.Max(0, deviceNow - stepData.baselineSteps);

        if (appOpenCaptured)
            return Math.Max(0, deviceNow - appOpenDeviceSteps);

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

        if (!appOpenCaptured) return savedDailyBase;

        int stepsSinceOpen = Math.Max(0, deviceNow - appOpenDeviceSteps);
        int daily = savedDailyBase + stepsSinceOpen;

        if (debugStepQueries)
            Debug.Log($"[Daily] device={deviceNow}, appOpen={appOpenDeviceSteps}, " +
                      $"stepsSinceOpen={stepsSinceOpen}, base={savedDailyBase}, daily={daily}");
        return daily;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Save
    // ─────────────────────────────────────────────────────────────────────────

    public void SaveStepData(int todayDeviceSteps, int gen = -1, bool forceWrite = false, int? dailyOverride = null)
    {
        if (isLoggingOut) return;
        if (gen >= 0 && IsStale(gen)) return;

        stepData.lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd");
        stepData.numberOfSteps = overallSteps;
        stepData.overallSteps = overallSteps;
        stepData.stepsBeforeToday = overallStepsBeforeToday;

        int computedDaily = dailyOverride ?? CalcDailySteps(todayDeviceSteps);
        // FIX: hard clamp — daily can never exceed overall
        stepData.dailySteps = Mathf.Clamp(computedDaily, 0, overallSteps);

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
        await CloudSaver2.SaveData("stepData", stepData);
        Debug.Log($"[CLOUD SAVE] overall={stepData.overallSteps}, daily={stepData.dailySteps}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cloud load
    // ─────────────────────────────────────────────────────────────────────────

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
            signInDeviceSteps = appOpenDeviceSteps;
            signedInThisSession = true;
        }

        int gen = sessionGen;
        try
        {
            var waitTask = appOpenTcs.Task;
            if (await Task.WhenAny(waitTask, Task.Delay(5000)) != waitTask)
                Debug.LogWarning("[CLOUD] appOpenTcs timed out — proceeding without app-open capture.");

            string json = await CloudSaver2.LoadData("stepData");
            if (IsStale(gen) || isLoggingOut) return;

            int preservedBaseline = stepData?.baselineSteps ?? 0;

            stepData = JsonUtility.FromJson<StepData>(json);
            Debug.Log($"[CLOUD] Raw: overall={stepData.overallSteps}, daily={stepData.dailySteps}, last={stepData.lastSaveTime}");

            // FIX: Validate and fix cloud data
            if (stepData.overallSteps == 0 && stepData.numberOfSteps > 0)
            {
                stepData.overallSteps = stepData.numberOfSteps;
                Debug.Log($"[CLOUD] Fixed: overallSteps was 0, set to {stepData.numberOfSteps}");
            }

            // FIX: Ensure daily steps are preserved when overall steps > 0
            if (stepData.overallSteps > 0 && stepData.dailySteps == 0 && stepData.numberOfSteps > 0)
            {
                stepData.dailySteps = Mathf.Min(stepData.numberOfSteps, stepData.dailySteps);
                Debug.Log($"[CLOUD] Recovered daily steps: {stepData.dailySteps}");
            }

            if (preservedBaseline > 0)
            {
                stepData.baselineSteps = preservedBaseline;
                Debug.Log($"[CLOUD] Restored baseline: {preservedBaseline}");
            }

            DateTime cloudDate = DateTime.Parse(stepData.lastSaveTime).Date;
            int daysSince = (DateTime.Today - cloudDate).Days;
            int cloudBase = stepData.overallSteps != 0 ? stepData.overallSteps : stepData.numberOfSteps;

            // FIX: sanitize cloud data — daily can never exceed overall
            if (stepData.dailySteps > cloudBase)
            {
                Debug.LogWarning($"[CLOUD] Corrupted daily ({stepData.dailySteps}) > overall ({cloudBase}), clamping.");
                stepData.dailySteps = cloudBase;
            }

            Debug.Log($"[CLOUD] daysSince={daysSince}, cloudBase={cloudBase}, " +
                      $"cloudDaily={stepData.dailySteps}, " +
                      $"cloudDate={cloudDate:yyyy-MM-dd}, today={DateTime.Today:yyyy-MM-dd}");
            Debug.Log($"[CLOUD] Path → {(daysSince == 0 ? "SameDay" : daysSince == 1 ? "NewDay" : "MultiDay")}");

            if (daysSince == 0) ApplyCloudSameDay(cloudBase, gen, localDailyBeforeCloud);
            else if (daysSince == 1) ApplyCloudNewDay(cloudBase, gen);
            else ApplyCloudMultiDayGap(cloudBase, cloudDate, gen);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CLOUD] Load failed: {e.Message}");
            waitingForCloudData = false;
            cloudLoaded = false;

            if (stepData != null) { onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps); onLoaded?.Invoke(); }
            else LoadStepData();

            if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
                refreshCoroutine = StartCoroutine(RefreshLoop());
        }
    }

    private void ApplyCloudSameDay(int cloudBase, int gen, int localDailyBeforeCloud)
    {
        // FIX: sanitize before using — daily can never exceed overall
        int cloudSavedDaily = Mathf.Clamp(stepData.dailySteps, 0, cloudBase);

        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            if (IsStale(gen) || isLoggingOut) return;

            // FIX: always reset anchor unconditionally — guest steps must not bleed in
            appOpenDeviceSteps = deviceNow;
            appOpenCaptured = true;
            signInDeviceSteps = deviceNow;
            signedInThisSession = true;

            overallStepsBeforeToday = Math.Max(0, cloudBase);
            beforeTodaySettled = true;
            overallSteps = cloudBase;
            savedDailyBase = cloudSavedDaily;
            stepData.dailySteps = cloudSavedDaily; // FIX: Preserve cloud daily steps

            PlayerPrefs.SetInt(OverallOffsetKey, deviceNow);
            PlayerPrefs.Save();

            Debug.Log($"[CLOUD SameDay] cloudBase={cloudBase}, cloudDaily={cloudSavedDaily}, " +
                      $"beforeToday={overallStepsBeforeToday}, deviceNow={deviceNow}");

            FinalizeCloudLoad();
        }).Execute();
    }

    private void ApplyCloudNewDay(int cloudBase, int gen)
    {
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            if (IsStale(gen) || isLoggingOut) return;

            // FIX: always reset anchor unconditionally
            appOpenDeviceSteps = deviceNow;
            appOpenCaptured = true;
            signInDeviceSteps = deviceNow;
            signedInThisSession = true;

            overallStepsBeforeToday = cloudBase;
            overallSteps = cloudBase;
            beforeTodaySettled = true;

            // FIX: Don't reset daily steps to zero - preserve from cloud if available
            savedDailyBase = stepData.dailySteps > 0 ? stepData.dailySteps : 0;
            // FIX: Ensure daily steps don't exceed overall steps
            stepData.dailySteps = Mathf.Clamp(stepData.dailySteps, 0, overallSteps);

            PlayerPrefs.SetInt(OverallOffsetKey, deviceNow);
            PlayerPrefs.Save();

            Debug.Log($"[CLOUD NewDay] cloudBase={cloudBase}, deviceNow={deviceNow}, daily={stepData.dailySteps}");

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

                // FIX: always reset anchor unconditionally
                appOpenDeviceSteps = deviceNow;
                appOpenCaptured = true;
                signInDeviceSteps = deviceNow;
                signedInThisSession = true;

                overallStepsBeforeToday = accumulated;
                overallSteps = accumulated;
                beforeTodaySettled = true;

                // FIX: Don't reset daily steps to zero - preserve from cloud if available
                savedDailyBase = stepData.dailySteps > 0 ? stepData.dailySteps : 0;
                // FIX: Ensure daily steps don't exceed overall steps
                stepData.dailySteps = Mathf.Clamp(stepData.dailySteps, 0, overallSteps);

                PlayerPrefs.SetInt(OverallOffsetKey, deviceNow);
                PlayerPrefs.Save();

                Debug.Log($"[CLOUD MultiDay] cloudBase={cloudBase}, range={range}, accumulated={accumulated}, deviceNow={deviceNow}, daily={stepData.dailySteps}");

                FinalizeCloudLoad();
            }).Execute();
        }).Execute();
    }

    private void FinalizeCloudLoad()
    {
        if (isLoggingOut) return;

        stepData.lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd");
        stepData.numberOfSteps = overallSteps;
        stepData.overallSteps = overallSteps;
        stepData.stepsBeforeToday = overallStepsBeforeToday;
        // FIX: ensure daily is clamped before writing to disk
        stepData.dailySteps = Mathf.Clamp(stepData.dailySteps, 0, overallSteps);

        WriteToDisk();

        PlayerPrefs.SetInt("CloudRestored", 1);
        PlayerPrefs.SetInt("HasEverSignedIn", 1);
        PlayerPrefs.DeleteKey("HasLoggedOut");
        PlayerPrefs.DeleteKey("SuppressStepQuery");
        PlayerPrefs.DeleteKey("SuppressCloudRestore");
        PlayerPrefs.Save();

        cloudLoaded = true;
        waitingForCloudData = false;
        offsetRecalibrated = true;
        verbosePostCloudPolls = 10;

        onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);
        onLoaded?.Invoke();

        StartCoroutine(FireStepsNextFrame(overallSteps, stepData.dailySteps));

        StopRefreshCoroutine();
        refreshCoroutine = StartCoroutine(RefreshLoop());

        Debug.Log($"[CLOUD] Finalized — overall={overallSteps}, daily={stepData.dailySteps}, " +
                  $"savedDailyBase={savedDailyBase}, appOpen={appOpenDeviceSteps}, " +
                  $"stepsBeforeToday={overallStepsBeforeToday}");
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

    // ─────────────────────────────────────────────────────────────────────────
    // Local load & initialization
    // ─────────────────────────────────────────────────────────────────────────

    public void LoadStepData()
    {
        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
        { ZeroState(); FireZero(); return; }

        bool suppressCloud = PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 1;

        if (!File.Exists(stepDataJsonFilePath))
        {
            if (suppressCloud)
            {
                // Fresh install during login flow — wait for cloud
                ZeroState();
                FireZero();
                return;
            }
            initializingFreshData = true;
            InitializeStepData();
            return;
        }

        stepData = JsonUtility.FromJson<StepData>(File.ReadAllText(stepDataJsonFilePath)) ?? new StepData();

        overallSteps = stepData.overallSteps != 0 ? stepData.overallSteps : stepData.numberOfSteps;

        bool isSameDay = !string.IsNullOrEmpty(stepData.lastSaveTime) &&
                         DateTime.Parse(stepData.lastSaveTime).Date == DateTime.Today;
        bool isNewDay = !string.IsNullOrEmpty(stepData.lastSaveTime) &&
                         DateTime.Parse(stepData.lastSaveTime).Date < DateTime.Today;

        if (isSameDay)
        {
            // FIX: use persisted stepsBeforeToday; fall back to subtraction only if missing
            overallStepsBeforeToday = stepData.stepsBeforeToday > 0
                ? stepData.stepsBeforeToday
                : Math.Max(0, overallSteps - stepData.dailySteps);
            beforeTodaySettled = true;
            // FIX: savedDailyBase set ONCE here — not overwritten below
            savedDailyBase = stepData.dailySteps;
        }
        else
        {
            // New day or unknown — will be settled by GetOverallSteps
            overallStepsBeforeToday = stepData.numberOfSteps;
            savedDailyBase = 0;
            beforeTodaySettled = false;
        }

        Debug.Log($"[LOAD] overall={overallSteps}, daily={stepData.dailySteps}, " +
                  $"savedDailyBase={savedDailyBase}, isSameDay={isSameDay}, isNewDay={isNewDay}, " +
                  $"stepsBeforeToday={overallStepsBeforeToday}");

        pendingLocalFireOnStart = true;

        // FIX: if suppressCloud is set, don't start polling — WaitForAuthThenLoad handles it
        if (suppressCloud) return;
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
            lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd")
        };
        overallSteps = 0;
        overallStepsBeforeToday = 0;
        savedDailyBase = 0;
        stepData.baselineSteps = 0;

        WriteToDisk();
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
            lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd"),
            baselineSteps = 0,
            numberOfSteps = 0,
            overallSteps = 0,
            dailySteps = 0,  // FIX: Explicitly set daily to 0 for fresh logout
            stepsBeforeToday = 0
        };
        overallSteps = 0;
        savedDailyBase = 0;
        onStepsUpdated?.Invoke(0, 0);

        int gen = sessionGen;
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            if (IsStale(gen)) return;

            stepData.baselineSteps = deviceNow;
            appOpenDeviceSteps = deviceNow;
            appOpenCaptured = true;
            beforeTodaySettled = true;

            PlayerPrefs.DeleteKey("HasLoggedOut");
            PlayerPrefs.DeleteKey("SuppressStepQuery");
            PlayerPrefs.Save();

            // FIX: Don't set overall steps to zero here - preserve the device reading
            // overallSteps remains 0 for new session after logout
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

        isLoggingOut = true;
        stepData = new StepData();
        overallSteps = 0;
        overallStepsBeforeToday = 0;
        cloudLoaded = false;
        beforeTodaySettled = false;
        waitingForCloudData = false;
        savedDailyBase = 0;
        appOpenDeviceSteps = 0;
        appOpenCaptured = false;
        signInDeviceSteps = 0;
        signedInThisSession = false;
        pendingLocalFireOnStart = false;
        initializingFreshData = false;
        offsetRecalibrated = false;
        queryInFlight = false;
        lastKnownDeviceSteps = 0;
        lastKnownDeviceCaptured = false;
        appOpenTcs = new TaskCompletionSource<int>();

        StopRefreshCoroutine();

        if (File.Exists(stepDataJsonFilePath)) File.Delete(stepDataJsonFilePath);

        PlayerPrefs.DeleteKey(OverallOffsetKey);
        PlayerPrefs.DeleteKey(DailyOffsetKey);
        PlayerPrefs.DeleteKey("SuppressStepQuery");
        PlayerPrefs.Save();

        onStepsUpdated?.Invoke(0, 0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

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

    private bool IsSignedIn()
    {
        return AuthenticationService.Instance != null &&
               AuthenticationService.Instance.IsSignedIn;
    }

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

    private void CaptureAppOpenSteps()
    {
        _ = TimeoutAppOpenTcs(2f);
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((n) =>
        {
            if (!appOpenCaptured)
            {
                appOpenDeviceSteps = n;
                appOpenCaptured = true;
                Debug.Log($"[SESSION] App-open steps captured: {n}");
            }
            if (!lastKnownDeviceCaptured)
            {
                lastKnownDeviceSteps = n;
                lastKnownDeviceCaptured = true;
            }
            appOpenTcs.TrySetResult(n);
        }).Execute();
    }

    private async Task TimeoutAppOpenTcs(float seconds)
    {
        await Task.Delay(TimeSpan.FromSeconds(seconds));
        appOpenTcs.TrySetResult(appOpenDeviceSteps);
        if (!appOpenCaptured)
            Debug.LogWarning("[SESSION] appOpenTcs timed out — cloud load unblocked without sensor reading.");
    }

    private IEnumerator FireStepsNextFrame(int overall, int daily)
    {
        yield return null;
        if (!isLoggingOut)
        {
            onStepsUpdated?.Invoke(overall, daily);
            onLoaded?.Invoke();
        }
    }

    private void WriteToDisk()
    {
        if (isLoggingOut) return;
        File.WriteAllText(stepDataJsonFilePath, JsonUtility.ToJson(stepData));
        Debug.Log($"[DISK] overall={stepData.overallSteps}, daily={stepData.dailySteps}");
    }

    private void ZeroState()
    {
        stepData = new StepData();
        overallSteps = 0;
        overallStepsBeforeToday = 0;
        beforeTodaySettled = false;
        waitingForCloudData = false;
        savedDailyBase = 0;
        appOpenDeviceSteps = 0;
        appOpenCaptured = false;
        signInDeviceSteps = 0;
        signedInThisSession = false;
        pendingLocalFireOnStart = false;
        offsetRecalibrated = false;
        queryInFlight = false;
        lastKnownDeviceSteps = 0;
        lastKnownDeviceCaptured = false;
        appOpenTcs = new TaskCompletionSource<int>();
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
    /// <summary>
    /// Resets step counter to new game state (zero steps)
    /// </summary>
    public void ResetToNewGameState(bool resetDaily = true, bool resetOverall = true)
    {
        if (resetOverall)
        {
            overallSteps = 0;
            overallStepsBeforeToday = 0;
        }

        if (resetDaily)
        {
            stepData.dailySteps = 0;
            savedDailyBase = 0;
        }

        stepData.numberOfSteps = overallSteps;
        stepData.overallSteps = overallSteps;
        stepData.stepsBeforeToday = overallStepsBeforeToday;
        stepData.baselineSteps = 0;

        // Reset tracking variables
        appOpenDeviceSteps = 0;
        appOpenCaptured = false;
        signInDeviceSteps = 0;
        signedInThisSession = false;
        beforeTodaySettled = false;
        offsetRecalibrated = false;

        // Clear offsets
        PlayerPrefs.DeleteKey(OverallOffsetKey);
        PlayerPrefs.DeleteKey(DailyOffsetKey);

        // Write to disk
        WriteToDisk();

        // Fire update event
        onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);

        Debug.Log($"[NewGame] Step counter reset: overall={overallSteps}, daily={stepData.dailySteps}");
    }

    /// <summary>
    /// Alternative method to reset steps to any custom value
    /// </summary>
    public void SetStepsToValue(int newOverallSteps, int newDailySteps)
    {
        overallSteps = Mathf.Max(0, newOverallSteps);
        stepData.dailySteps = Mathf.Clamp(newDailySteps, 0, overallSteps);
        stepData.numberOfSteps = overallSteps;
        stepData.overallSteps = overallSteps;

        WriteToDisk();
        onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);

        Debug.Log($"[NewGame] Steps set to: overall={overallSteps}, daily={stepData.dailySteps}");
    }
}