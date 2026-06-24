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
    public float diskSaveInterval = 5f;

    public StepData stepData;
    public int overallSteps;
    public int daily;
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

    public int savedDailyBase = 0;

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

    // Guest login delay flag
    private bool isGuestLoginPending = false;
    private Coroutine delayedGuestStartCoroutine;
    private bool readyToCount = false;

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
            ResetStepDataForLogout();
            isLoggingOut = false;
            // PlayerPrefs.SetInt("SuppressStepQuery", 1);
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

        if (delayedGuestStartCoroutine != null)
            StopCoroutine(delayedGuestStartCoroutine);
    }

    void Start()
    {
        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1) return;
        if (initializingFreshData) return;
        if (PlayerPrefs.GetInt("GuestLoginPending", 0) == 1)
        {
            Debug.Log("[GUEST DEBUG] GuestLoginPending detected in Start()");
            isGuestLoginPending = true;
            StartCoroutine(DelayedGuestStart());
            return;
        }
        if (isGuestLoginPending) return;
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
        if (waitingForCloudData) return;
        if (AuthManager.Instance != null && AuthManager.Instance.IsGuest) return;

        if (PlayerPrefs.GetString("LastLoginMethod", "") == "Guest") return;

        cloudLoaded = false;
        if (PlayerPrefs.GetInt("IsGuestUpgrade", 0) == 1)
        {
            PlayerPrefs.DeleteKey("IsGuestUpgrade");
            PlayerPrefs.Save();
            Debug.Log("[StepCounter] Guest upgrade detected — saving existing steps to cloud instead of loading.");
            await SaveStepDataToCloud();
            cloudLoaded = true;

            // Restart refresh loop if not running
            if (refreshCoroutine == null)
                refreshCoroutine = StartCoroutine(RefreshLoop());
            return;
        }
        if (PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 1)
        {
            PlayerPrefs.DeleteKey("SuppressCloudRestore");
            PlayerPrefs.Save();
            Debug.Log("[StepCounter] Cleared SuppressCloudRestore on account sign-in.");
        }

        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
        {
            PlayerPrefs.DeleteKey("HasLoggedOut");
            PlayerPrefs.DeleteKey("SuppressStepQuery");
            PlayerPrefs.Save();
            Debug.Log("[StepCounter] HasLoggedOut cleared on new sign-in.");
        }

        // Reset session-specific state before loading fresh cloud data
        sessionGen++;                  // ← invalidate any in-flight queries from old session
        beforeTodaySettled = false;
        offsetRecalibrated = false;
        StopRefreshCoroutine();

        Debug.Log("[StepCounter] OnAuthStateChanged — account sign-in confirmed, loading from cloud.");
        await LoadStepDataFromCloud();
    }
    public void PrepareForLogout()
    {
        // Stop everything in flight so the next sign-in starts clean
        StopRefreshCoroutine();
        sessionGen++;              // invalidates any in-flight queries
        cloudLoaded = false;
        waitingForCloudData = false;
        beforeTodaySettled = false;
        readyToCount = false;
        offsetRecalibrated = false;
        signedInThisSession = false;
        appOpenCaptured = false;
        appOpenTcs = new TaskCompletionSource<int>();

        overallSteps = 0;
        overallStepsBeforeToday = 0;
        savedDailyBase = 0;
        lastKnownDeviceSteps = 0;
        lastKnownDeviceCaptured = false;
        stepData = new StepData
        {
            registrationTime = DateTime.Today.ToString("yyyy-MM-dd"),
            lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd")
        };
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

        // if (isAccountSession && !suppressCloud)
        // {
        //     Debug.Log("[StepCounter] Auth settled — loading step data from cloud.");
        //     if (!waitingForCloudData && !cloudLoaded)
        //         _ = LoadStepDataFromCloud();
        //     yield break;
        // }
        if (!isGuest && IsSignedIn() && !suppressCloud)
        {
            Debug.Log("[StepCounter] Signed-in account detected on start — loading from cloud.");
            if (!waitingForCloudData && !cloudLoaded)
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

        // if (lastKnownDeviceCaptured)
        // {
        //     int recalcDaily = CalcDailySteps(lastKnownDeviceSteps);
        //     stepData.dailySteps = Mathf.Clamp(recalcDaily, 0, overallSteps);
        //     Debug.Log($"[EXIT] Daily recalculated: savedBase={savedDailyBase}, " +
        //               $"device={lastKnownDeviceSteps}, appOpen={appOpenDeviceSteps}, " +
        //               $"stepsSinceOpen={lastKnownDeviceSteps - appOpenDeviceSteps}, " +
        //               $"daily={stepData.dailySteps}");
        // }
        // else if (savedDailyBase > 0 && stepData.dailySteps == 0)
        // {
        //     stepData.dailySteps = Math.Min(savedDailyBase, overallSteps);
        //     Debug.Log($"[EXIT] No device reading — preserving savedDailyBase={savedDailyBase} as daily");
        // }
        stepData.dailySteps = Mathf.Clamp(stepData.dailySteps, 0, overallSteps);

        WriteToDisk();
        Debug.Log($"[EXIT] Committed — overall={overallSteps}, daily={stepData.dailySteps}, stepsBeforeToday={overallStepsBeforeToday}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Step polling
    // ─────────────────────────────────────────────────────────────────────────

    public void GetOverallSteps()
    {
        Debug.Log($"[GUEST DEBUG] GetOverallSteps called — " +
              $"isLoggingOut={isLoggingOut}, " +
              $"queryInFlight={queryInFlight}, " +
              $"waitingForCloud={waitingForCloudData}, " +
              $"registrationTime={stepData?.registrationTime}, " +
              $"lastSaveTime={stepData?.lastSaveTime}");

        if (isLoggingOut) return;
        if (queryInFlight) return;
        if (waitingForCloudData) return;
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
            if (stepData.stepsBeforeToday > 0)
                overallStepsBeforeToday = stepData.stepsBeforeToday;
            else
                overallStepsBeforeToday = Math.Max(0, overallSteps);

            if (savedDailyBase == 0 && stepData.dailySteps > 0)
                savedDailyBase = stepData.dailySteps;

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
                queryInFlight = false;
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
                queryInFlight = false;
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
        if (savedDailyBase == 0 && stepData.dailySteps > 0)
            savedDailyBase = stepData.dailySteps;

        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            queryInFlight = false;

            if (savedDailyBase == 0 && stepData.dailySteps > 0)
            {
                savedDailyBase = stepData.dailySteps;
                Debug.Log($"[Steps] Restored savedDailyBase from stepData: {savedDailyBase}");
            }

            if (!isLoggingOut)
            {
                if (lastKnownDeviceCaptured && deviceNow < lastKnownDeviceSteps - 500)
                {
                    Debug.LogWarning($"[StepCounter] Rejected suspicious reading: {deviceNow} < {lastKnownDeviceSteps} — using last known.");
                    deviceNow = lastKnownDeviceSteps;
                }
                lastKnownDeviceSteps = deviceNow;
                lastKnownDeviceCaptured = true;
            }

            if (IsStale(gen) || isLoggingOut) return;

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
            int prevDaily = stepData.dailySteps;
            int daily = CalcDailySteps(deviceNow);
            daily = Mathf.Clamp(daily, 0, int.MaxValue);
            //int todayNet = CalcTodayNetSteps(deviceNow);
            //overallSteps = overallStepsBeforeToday + todayNet;

            
            //daily = Mathf.Max(daily, savedDailyBase);
            // daily = Mathf.Max(daily, prevDaily);
            
            overallSteps = overallStepsBeforeToday + daily;

            // ← Never let overall or daily decrease mid-session
            // Steps only go up unless it's a new day
            overallSteps = Mathf.Max(overallSteps, prev);
            


            // NOTE: Do NOT do `savedDailyBase = Mathf.Max(savedDailyBase, daily);` here.
            // `daily` already equals `savedDailyBase + stepsSinceOpen` (see CalcDailySteps).
            // Re-assigning it back into savedDailyBase bakes the already-counted
            // stepsSinceOpen into the base, so the *next* poll adds the same
            // stepsSinceOpen on top again — causing daily/overall steps to climb every
            // refresh tick even with zero real device movement (the runaway-without-
            // movement bug seen on guest first login and same-day cloud restore, where
            // stepData.baselineSteps ends up <= 0 and CalcDailySteps falls into the
            // appOpenCaptured fallback branch). savedDailyBase must stay a fixed
            // snapshot for the session; it's already (re)seeded correctly on load/init.
            // stepData.dailySteps = daily;

            int overallDelta = Math.Abs(overallSteps - prev);
            int dailyDelta = Math.Abs(daily - prevDaily);

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

            //stepData.dailySteps = daily;
            SaveStepData(deviceNow, gen, dailyOverride: daily);
        }).Execute();
    }

    private IEnumerator RefreshLoop()
    {
        while (true)
        {
            if (!waitingForCloudData && readyToCount)  // ← wrap the call
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

        // Guard against concurrent cloud loads (e.g. OnAuthStateChanged + WaitForAuthThenLoad racing).
        if (waitingForCloudData)
        {
            Debug.Log("[CLOUD] Load already in progress — skipping duplicate call.");
            return;
        }
        if (!appOpenCaptured)
        {
            CaptureAppOpenSteps();
        }
        waitingForCloudData = true;

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
                waitingForCloudData = false;
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

            // GUARD: If cloud returned null/empty (e.g. unstable connection), fall back
            // gracefully rather than overwriting real local data with zeros.
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[CLOUD] Received null/empty JSON — aborting cloud load to protect local data.");
                waitingForCloudData = false;
                cloudLoaded = true;
                if (stepData != null) { onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps); onLoaded?.Invoke(); }
                else LoadStepData();
                if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
                    refreshCoroutine = StartCoroutine(RefreshLoop());
                return;
            }

            //int preservedBaseline = stepData?.baselineSteps ?? 0;
            // Snapshot local data so we can fall back if cloud data is worse
            int localOverallSnapshot = overallSteps;
            int localDailySnapshot = stepData?.dailySteps ?? 0;

            StepData parsedCloud = JsonUtility.FromJson<StepData>(json);

            // GUARD: If parsing produced a null or zeroed result but we have real local data,
            // abort — do NOT overwrite good data with a zeroed cloud payload.
            if (parsedCloud == null ||
                (parsedCloud.overallSteps == 0 && parsedCloud.numberOfSteps == 0 && localOverallSnapshot > 0))
            {
                Debug.LogWarning($"[CLOUD] Parsed cloud data is zero/null but local has {localOverallSnapshot} steps — aborting to protect local data.");
                waitingForCloudData = false;
                cloudLoaded = true;
                onStepsUpdated?.Invoke(overallSteps, stepData?.dailySteps ?? 0);
                onLoaded?.Invoke();
                if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
                    refreshCoroutine = StartCoroutine(RefreshLoop());
                return;
            }

            stepData = parsedCloud;
            Debug.Log($"[CLOUD] Raw: overall={stepData.overallSteps}, daily={stepData.dailySteps}, last={stepData.lastSaveTime}");

            if (stepData.overallSteps == 0 && stepData.numberOfSteps > 0)
            {
                stepData.overallSteps = stepData.numberOfSteps;
                Debug.Log($"[CLOUD] Fixed: overallSteps was 0, set to {stepData.numberOfSteps}");
            }

            if (stepData.overallSteps > 0 && stepData.dailySteps == 0 && stepData.numberOfSteps > 0)
            {
                stepData.dailySteps = Mathf.Min(stepData.numberOfSteps, stepData.dailySteps);
                Debug.Log($"[CLOUD] Recovered daily steps: {stepData.dailySteps}");
            }

            // if (preservedBaseline > 0)
            // {
            //     stepData.baselineSteps = preservedBaseline;
            //     Debug.Log($"[CLOUD] Restored baseline: {preservedBaseline}");
            // }

            DateTime cloudDate = DateTime.Parse(stepData.lastSaveTime).Date;
            int daysSince = (DateTime.Today - cloudDate).Days;
            int cloudBase = stepData.overallSteps != 0 ? stepData.overallSteps : stepData.numberOfSteps;

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
        int cloudSavedDaily = Mathf.Clamp(stepData.dailySteps, 0, cloudBase);

        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            if (IsStale(gen) || isLoggingOut) return;

            // GUARD: Never let a zero cloudBase overwrite real existing steps.
            if (cloudBase == 0 && overallSteps > 0)
            {
                Debug.LogWarning($"[CLOUD SameDay] cloudBase=0 but local overallSteps={overallSteps} — keeping local data.");
                FinalizeCloudLoad();
                return;
            }
            int stepsBeforeToday = stepData.stepsBeforeToday > 0
        ? stepData.stepsBeforeToday
        : Math.Max(0, cloudBase - cloudSavedDaily);

            // if (!appOpenCaptured)
            // {
            //     appOpenDeviceSteps = deviceNow;
            //     appOpenCaptured = true;
            // }
            appOpenDeviceSteps = deviceNow;
            appOpenCaptured = true;
            signInDeviceSteps = deviceNow;
            signedInThisSession = false;

            overallStepsBeforeToday = stepsBeforeToday;
            beforeTodaySettled = true;
            overallSteps = cloudBase;
            readyToCount = true;

            savedDailyBase = cloudSavedDaily;
            stepData.dailySteps = cloudSavedDaily;
            //stepData.baselineSteps = 0;

            int impliedBaseline = Math.Max(0, deviceNow - cloudSavedDaily);
            stepData.baselineSteps = impliedBaseline;

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

            // GUARD: Never let a zero cloudBase overwrite real existing steps.
            if (cloudBase == 0 && overallSteps > 0)
            {
                Debug.LogWarning($"[CLOUD NewDay] cloudBase=0 but local overallSteps={overallSteps} — keeping local data.");
                FinalizeCloudLoad();
                return;
            }

            appOpenDeviceSteps = deviceNow;
            appOpenCaptured = true;
            signInDeviceSteps = deviceNow;
            signedInThisSession = true;
            stepData.baselineSteps = 0;

            overallStepsBeforeToday = cloudBase;
            overallSteps = cloudBase;
            beforeTodaySettled = true;
            readyToCount = true;

            stepData.baselineSteps = deviceNow;

            savedDailyBase = stepData.dailySteps > 0 ? stepData.dailySteps : 0;
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

                // GUARD: Never let a zero accumulated value overwrite real existing steps.
                if (accumulated == 0 && overallSteps > 0)
                {
                    Debug.LogWarning($"[CLOUD MultiDay] accumulated=0 but local overallSteps={overallSteps} — keeping local data.");
                    FinalizeCloudLoad();
                    return;
                }

                appOpenDeviceSteps = deviceNow;
                appOpenCaptured = true;
                signInDeviceSteps = deviceNow;
                signedInThisSession = true;

                overallStepsBeforeToday = accumulated;
                overallSteps = accumulated;
                beforeTodaySettled = true;
                readyToCount = true;

                stepData.baselineSteps = deviceNow;

                savedDailyBase = stepData.dailySteps > 0 ? stepData.dailySteps : 0;
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

        // GUARD: Final safety net — if somehow we reach finalize with zero steps
        // but the file on disk has real data, reload from disk instead of writing zeros.
        if (overallSteps == 0 && File.Exists(stepDataJsonFilePath))
        {
            StepData diskData = JsonUtility.FromJson<StepData>(File.ReadAllText(stepDataJsonFilePath));
            int diskOverall = diskData?.overallSteps != 0 ? diskData.overallSteps : diskData?.numberOfSteps ?? 0;
            if (diskOverall > 0)
            {
                Debug.LogWarning($"[CLOUD] FinalizeCloudLoad would write 0 but disk has {diskOverall} — keeping disk data.");
                overallSteps = diskOverall;
                overallStepsBeforeToday = diskData.stepsBeforeToday > 0 ? diskData.stepsBeforeToday : diskOverall;
                stepData.overallSteps = diskOverall;
                stepData.numberOfSteps = diskOverall;
                //stepData.dailySteps = diskData.dailySteps;
                stepData.stepsBeforeToday = overallStepsBeforeToday;
            }
        }

        stepData.lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd");
        stepData.numberOfSteps = overallSteps;
        stepData.overallSteps = overallSteps;
        stepData.stepsBeforeToday = overallStepsBeforeToday;
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
            overallStepsBeforeToday = stepData.stepsBeforeToday > 0
                ? stepData.stepsBeforeToday
                : Math.Max(0, overallSteps - stepData.dailySteps);
            beforeTodaySettled = true;
            savedDailyBase = stepData.dailySteps;
        }
        else
        {
            overallStepsBeforeToday = stepData.numberOfSteps;
            savedDailyBase = 0;
            beforeTodaySettled = false;
        }

        Debug.Log($"[LOAD] overall={overallSteps}, daily={stepData.dailySteps}, " +
                  $"savedDailyBase={savedDailyBase}, isSameDay={isSameDay}, isNewDay={isNewDay}, " +
                  $"stepsBeforeToday={overallStepsBeforeToday}");

        pendingLocalFireOnStart = true;

        if (suppressCloud) return;
        if (waitingForCloudData) return;

        bool cloudRestored = PlayerPrefs.GetInt("CloudRestored", 0) == 1;
        if (!cloudRestored && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
            GetOverallSteps();

        if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0 && !waitingForCloudData)   // ← add this guard
        {
            refreshCoroutine = StartCoroutine(RefreshLoop());
            Debug.Log("[LOAD] Starting RefreshLoop.");
        }
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

            // PlayerPrefs.SetInt(OverallOffsetKey, deviceNow);
            // PlayerPrefs.SetInt("HasEverSignedIn", 1);
            // PlayerPrefs.Save();
            if (PlayerPrefs.GetInt("NewGamePendingBaseline", 0) == 1)
            {
                stepData.baselineSteps = deviceNow;
                appOpenDeviceSteps = deviceNow;
                appOpenCaptured = true;
                PlayerPrefs.DeleteKey("NewGamePendingBaseline");
                PlayerPrefs.Save();
                Debug.Log($"[NewGame] Baseline anchored at {deviceNow} device steps.");
            }
            else
            {
                PlayerPrefs.SetInt(OverallOffsetKey, deviceNow);
                PlayerPrefs.Save();
            }

            beforeTodaySettled = true;
            onLoaded?.Invoke();

            if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
                refreshCoroutine = StartCoroutine(RefreshLoop());
        }).Execute();
    }
    // In OverallStepCounter.cs

    public void ReInitializeForNewGame()
    {
        Debug.Log("[NewGame] Re-initializing OverallStepCounter for new session...");

        // 1. Invalidate all in-flight callbacks from the old session
        sessionGen++;

        // 2. Stop any lingering coroutines
        StopRefreshCoroutine();
        if (delayedGuestStartCoroutine != null)
        {
            StopCoroutine(delayedGuestStartCoroutine);
            delayedGuestStartCoroutine = null;
        }

        // 3. Full memory reset
        isLoggingOut = false;
        queryInFlight = false;
        cloudLoaded = false;
        waitingForCloudData = false;
        pendingLocalFireOnStart = false;
        initializingFreshData = false;
        offsetRecalibrated = false;
        queryInFlight = false;
        beforeTodaySettled = false;
        readyToCount = false;
        isGuestLoginPending = false;

        overallSteps = 0;
        overallStepsBeforeToday = 0;
        savedDailyBase = 0;
        appOpenDeviceSteps = 0;
        appOpenCaptured = false;
        signInDeviceSteps = 0;
        signedInThisSession = false;
        lastKnownDeviceSteps = 0;
        lastKnownDeviceCaptured = false;
        appOpenTcs = new TaskCompletionSource<int>();

        // 4. Fresh StepData with today's date
        stepData = new StepData
        {
            registrationTime = DateTime.Today.ToString("yyyy-MM-dd"),
            lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd"),
            baselineSteps = 0,
            overallSteps = 0,
            numberOfSteps = 0,
            dailySteps = 0
        };

        // 5. Fire zero immediately so UI clears
        onStepsUpdated?.Invoke(0, 0);

        // 6. Query device pedometer — use current reading as the new baseline
        int gen = sessionGen;
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            queryInFlight = false;
            if (IsStale(gen)) return;
            if (isGuestLoginPending) return; // If a guest login is pending, we'll capture the baseline later when we initialize for guest

            // Anchor today's device steps as baseline so we count from 0
            stepData.baselineSteps = deviceNow;
            appOpenDeviceSteps = deviceNow;
            appOpenCaptured = true;
            beforeTodaySettled = true;
            readyToCount = true;

            PlayerPrefs.SetInt(OverallOffsetKey, deviceNow);
            PlayerPrefs.DeleteKey("NewGamePendingBaseline");
            PlayerPrefs.Save();

            WriteToDisk();

            Debug.Log($"[NewGame] Baseline anchored at {deviceNow}. Step counting restarted.");

            onStepsUpdated?.Invoke(0, 0);
            onLoaded?.Invoke();

            // 7. Restart the refresh loop — this is what actually drives future updates
            //if (refreshCoroutine == null)
            refreshCoroutine = StartCoroutine(RefreshLoop());

        }).Execute();

        // Timeout fallback in case pedometer is slow
        StartCoroutine(NewGameBaselineTimeout(gen));
    }

    private IEnumerator NewGameBaselineTimeout(int gen)
    {
        yield return new WaitForSeconds(5f);

        if (IsStale(gen)) yield break;
        if (appOpenCaptured) yield break; // already succeeded

        Debug.LogWarning("[NewGame] Baseline query timed out — starting RefreshLoop with zero baseline.");
        beforeTodaySettled = true;

        onStepsUpdated?.Invoke(0, 0);
        onLoaded?.Invoke();

        if (refreshCoroutine == null)
            refreshCoroutine = StartCoroutine(RefreshLoop());
    }

    public void InitializeStepDataAfterLogout()
    {
        stepData = new StepData
        {
            registrationTime = DateTime.Today.ToString("yyyy-MM-dd"),
            lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd")
        };

        overallSteps = 0;
        overallStepsBeforeToday = 0;
        savedDailyBase = 0;
        beforeTodaySettled = false;
        readyToCount = false;
        offsetRecalibrated = false;
        cloudLoaded = false;
        appOpenCaptured = false;
        appOpenDeviceSteps = 0;

        onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);

        int gen = sessionGen;
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            if (IsStale(gen)) return;

            stepData.baselineSteps = deviceNow;
            appOpenDeviceSteps = deviceNow;
            appOpenCaptured = true;
            beforeTodaySettled = true;
            readyToCount = true;

            PlayerPrefs.DeleteKey("HasLoggedOut");
            PlayerPrefs.DeleteKey("SuppressStepQuery");
            PlayerPrefs.Save();

            onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);

            if (refreshCoroutine == null)
                refreshCoroutine = StartCoroutine(RefreshLoop());

            Debug.Log($"[LOGOUT INIT] Baseline set: {deviceNow}. Preserved steps: overall={overallSteps}, daily={stepData.dailySteps}");
        }).Execute();
    }

    public void ResetStepDataForLogout()
    {
        sessionGen++;
        Debug.Log($"[LOGOUT] Preparing for logout - preserving step data. Gen → {sessionGen}");

        isLoggingOut = true;

        cloudLoaded = false;
        waitingForCloudData = false;
        pendingLocalFireOnStart = false;
        initializingFreshData = false;
        offsetRecalibrated = false;
        queryInFlight = false;

        StopRefreshCoroutine();

        Debug.Log($"[LOGOUT] Step data preserved: overall={overallSteps}, daily={stepData?.dailySteps ?? 0}");
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
    // Guest Login Initialization - DELAYED UNTIL MAIN SCREEN IS SHOWN
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when guest login button is clicked - sets up pending state
    /// </summary>
    public void PrepareForGuestLogin()
    {
        Debug.Log("[StepCounter] Preparing for guest login - will start counting when main screen loads");

        // Set guest session flags but don't start counting yet
        PlayerPrefs.SetInt("IsGuestSession", 1);
        PlayerPrefs.SetInt("SuppressCloudRestore", 1);
        PlayerPrefs.SetInt("GuestLoginPending", 1);
        PlayerPrefs.SetString("LastLoginMethod", "Guest");
        PlayerPrefs.Save();

        isGuestLoginPending = true;
    }
    /// <summary>
    /// Initialize step counter for guest login - starts step counting immediately
    /// </summary>
    public void InitializeForGuestLogin()
    {
        PlayerPrefs.SetInt("SuppressCloudRestore", 1);
        PlayerPrefs.SetInt("IsGuestSession", 1);
        PlayerPrefs.SetString("LastLoginMethod", "Guest");
        // ← Clear logout flags so guest init isn't blocked
        PlayerPrefs.DeleteKey("HasLoggedOut");
        PlayerPrefs.SetInt("SuppressStepQuery", 0);

        Debug.Log("[StepCounter] Initializing step counter for guest login - starting step counting");

        // Set guest session flags


        // PlayerPrefs.SetInt("SuppressStepQuery", 0); // Allow step queries
        PlayerPrefs.SetInt("StepCountingActive", 1); // Mark step counting as active
        PlayerPrefs.SetInt("GuestLoginStepCountingStarted", 1);
        PlayerPrefs.DeleteKey("GuestLoginPending");
        PlayerPrefs.Save();

        isLoggingOut = false;   // ← unblock WriteToDisk and events
        queryInFlight = false;
        waitingForCloudData = false;
        isGuestLoginPending = false; // ← unblock Start() guard too

        sessionGen++;
        StopRefreshCoroutine();
        overallSteps = 0;
        overallStepsBeforeToday = 0;
        savedDailyBase = 0;
        appOpenDeviceSteps = 0;
        appOpenCaptured = false;
        signInDeviceSteps = 0;
        signedInThisSession = false;
        lastKnownDeviceSteps = 0;
        lastKnownDeviceCaptured = false;
        beforeTodaySettled = false;
        readyToCount = false;
        offsetRecalibrated = false;
        cloudLoaded = false;
        appOpenTcs = new TaskCompletionSource<int>();

        // Always fresh for guest — never restore previous session data
        stepData = new StepData
        {
            registrationTime = DateTime.Today.ToString("yyyy-MM-dd"),
            lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd"),
            baselineSteps = 0,
            overallSteps = 0,
            numberOfSteps = 0,
            dailySteps = 0
        };
        overallSteps = 0;
        overallStepsBeforeToday = 0;
        savedDailyBase = 0;

        // Remove the hadExistingData disk restore block entirely for guest-after-logout
        // Guest always starts from zero when coming from a signed-in session

        beforeTodaySettled = true;
        int gen = ++sessionGen;

        Debug.Log("[StepCounter] Executing StepCounterRequest for guest login...");

        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            if (IsStale(gen)) return;

            appOpenDeviceSteps = deviceNow;
            appOpenCaptured = true;

            // Always anchor baseline so guest counts from 0
            stepData.baselineSteps = deviceNow;
            overallStepsBeforeToday = 0;
            overallSteps = 0;
            stepData.dailySteps = 0;
            stepData.overallSteps = 0;
            stepData.numberOfSteps = 0;
            savedDailyBase = 0;

            beforeTodaySettled = true;
            readyToCount = true;

            WriteToDisk();

            onStepsUpdated?.Invoke(0, 0);
            onLoaded?.Invoke();

            if (refreshCoroutine == null)
                refreshCoroutine = StartCoroutine(RefreshLoop());

            isGuestLoginPending = false;

            Debug.Log($"[GuestLogin] Baseline anchored at {deviceNow}. Guest starts at 0.");

        }).Execute();

        StartCoroutine(GuestLoginTimeoutFallback(gen));
    }


    // // // Ensure stepData exists
    // // if (stepData == null)
    // // {
    // //     stepData = new StepData();
    // //     stepData.registrationTime = DateTime.Today.ToString("yyyy-MM-dd");
    // //     stepData.lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd");
    // // }

    // // // Reset for guest login - start fresh from today
    // // overallStepsBeforeToday = 0;
    // // beforeTodaySettled = true;
    // // overallSteps = 0;
    // // stepData.dailySteps = 0;
    // // savedDailyBase = 0;

    // // // Increment session generation
    // // Load existing guest save data if it exists — don't zero it out
    // bool hadExistingData = false;
    // if (File.Exists(stepDataJsonFilePath))
    // {
    //     try
    //     {
    //         var loaded = JsonUtility.FromJson<StepData>(
    //             File.ReadAllText(stepDataJsonFilePath));

    //         if (loaded != null && loaded.overallSteps > 0)
    //         {
    //             stepData = loaded;
    //             overallSteps = loaded.overallSteps;
    //             overallStepsBeforeToday = loaded.stepsBeforeToday > 0
    //                 ? loaded.stepsBeforeToday
    //                 : Math.Max(0, loaded.overallSteps - loaded.dailySteps);
    //             savedDailyBase = loaded.dailySteps;
    //             hadExistingData = true;

    //             Debug.Log($"[GuestLogin] Restored existing data — " +
    //                       $"overall={overallSteps}, daily={savedDailyBase}");
    //         }
    //     }
    //     catch (Exception e)
    //     {
    //         Debug.LogWarning($"[GuestLogin] Could not load existing data: {e.Message}");
    //     }
    // }

    // if (!hadExistingData)
    // {
    //     // Fresh guest — initialize clean
    //     stepData = new StepData
    //     {
    //         registrationTime = DateTime.Today.ToString("yyyy-MM-dd"),
    //         lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd"),
    //         baselineSteps = 0,
    //         overallSteps = 0,
    //         numberOfSteps = 0,
    //         dailySteps = 0
    //     };
    //     overallSteps = 0;
    //     overallStepsBeforeToday = 0;
    //     savedDailyBase = 0;
    // }

    // beforeTodaySettled = true;
    // int gen = ++sessionGen;

    // Debug.Log("[StepCounter] Executing StepCounterRequest for guest login...");

    // // Execute StepCounterRequest to get today's steps
    // new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
    // {
    //     Debug.Log($"[StepCounter] StepCounterRequest SUCCESS - Device steps today: {deviceNow}");

    //     if (IsStale(gen))
    //     {
    //         Debug.Log($"[StepCounter] Callback is stale (gen {gen} vs {sessionGen}), ignoring");
    //         return;
    //     }

    //     // Capture app open steps
    //     appOpenDeviceSteps = deviceNow;
    //     appOpenCaptured = true;
    //     Debug.Log($"[StepCounter] App open steps captured: {appOpenDeviceSteps}");

    //     if (!hadExistingData)
    //     {
    //         stepData.baselineSteps = deviceNow;
    //         overallStepsBeforeToday = 0;     // ← add this
    //         overallSteps = 0;                // ← add this
    //         stepData.dailySteps = 0;         // ← explicitly zero for fresh guest
    //     }
    //     else
    //     {
    //         // Recalculate daily from saved base + steps walked since app opened
    //         int stepsSinceOpen = Math.Max(0, deviceNow - appOpenDeviceSteps); // 0 right now
    //         stepData.dailySteps = Mathf.Clamp(savedDailyBase, 0, overallSteps);
    //     }

    //     // // For guest login, baseline should be 0 (start counting from today)
    //     // stepData.baselineSteps = 0;

    //     // // Calculate overall steps - for guest, overall steps = today's steps
    //     // overallSteps = deviceNow;
    //     // stepData.overallSteps = overallSteps;
    //     // stepData.numberOfSteps = overallSteps;

    //     // // Calculate daily steps - same as overall for guest
    //     // stepData.dailySteps = deviceNow;

    //     // stepData.baselineSteps = 0;
    //     // stepData.overallSteps = 0;
    //     // stepData.numberOfSteps = 0;
    //     // stepData.dailySteps = 0;
    //     Debug.Log($"[StepCounter] Guest login final values - Overall: {overallSteps}, Daily: {deviceNow}");

    //     beforeTodaySettled = true;           // ← move this BEFORE the event fires
    //     stepData.overallSteps = overallSteps;
    //     stepData.numberOfSteps = overallSteps;
    //     // Save to disk
    //     WriteToDisk();

    //     // Trigger update event to notify UserLevel and other listeners
    //     Debug.Log("[StepCounter] Triggering onStepsUpdated event");
    //     onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);
    //     onLoaded?.Invoke();

    //     // Start the refresh loop if not already running
    //     if (refreshCoroutine == null)
    //     {
    //         Debug.Log("[StepCounter] Starting refresh coroutine");
    //         refreshCoroutine = StartCoroutine(RefreshLoop());
    //     }

    //     isGuestLoginPending = false;

    // }).Execute();

    // // Set a timeout fallback
    // StartCoroutine(GuestLoginTimeoutFallback(gen));
    // }

    private IEnumerator GuestLoginTimeoutFallback(int gen)
    {
        yield return new WaitForSeconds(5f);

        if (isGuestLoginPending && !IsStale(gen))
        {
            Debug.LogWarning("[StepCounter] Guest login step query timeout - setting default values");

            overallSteps = 0;
            stepData.dailySteps = 0;
            beforeTodaySettled = true;
            readyToCount = true;

            onStepsUpdated?.Invoke(0, 0);

            if (refreshCoroutine == null)
            {
                refreshCoroutine = StartCoroutine(RefreshLoop());
            }

            isGuestLoginPending = false;
        }
    }



    /// <summary>
    /// Called when main screen is loaded - starts the delayed guest step counting
    /// </summary>
    public void StartDelayedGuestStepCounting()
    {
        if (isGuestLoginPending)
        {
            Debug.Log("[StepCounter] Main screen loaded - starting guest step counting");

            if (delayedGuestStartCoroutine != null)
                StopCoroutine(delayedGuestStartCoroutine);

            delayedGuestStartCoroutine = StartCoroutine(DelayedGuestStart());
        }
    }

    private IEnumerator DelayedGuestStart()
    {
        Debug.Log($"[GUEST DEBUG] DelayedGuestStart called — " +
              $"isLoggingOut={isLoggingOut}, " +
              $"sessionGen={sessionGen}, " +
              $"overallSteps={overallSteps}, " +
              $"savedDailyBase={savedDailyBase}, " +
              $"appOpenDeviceSteps={appOpenDeviceSteps}, " +
              $"appOpenCaptured={appOpenCaptured}, " +
              $"refreshCoroutine={(refreshCoroutine != null ? "RUNNING" : "NULL")}");
        // Wait a moment for the main screen to fully initialize
        yield return new WaitForSeconds(0.5f);

        InitializeForGuestLogin();

        delayedGuestStartCoroutine = null;
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
        readyToCount = false;
        waitingForCloudData = false;
        cloudLoaded = false;
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
        if (refreshCoroutine != null) { StopCoroutine(refreshCoroutine); refreshCoroutine = null; }
        if (delayedGuestStartCoroutine != null) { StopCoroutine(delayedGuestStartCoroutine); delayedGuestStartCoroutine = null; }
        // StopAllCoroutines();
        // refreshCoroutine = null;
        // delayedGuestStartCoroutine = null;

        // if (refreshCoroutine == null) return;
        // StopCoroutine(refreshCoroutine);
        // refreshCoroutine = null;
    }

    /// <summary>
    /// Resets step counter to new game state (zero steps) - Use this for NEW GAME only
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

        appOpenDeviceSteps = 0;
        appOpenCaptured = false;
        signInDeviceSteps = 0;
        signedInThisSession = false;
        beforeTodaySettled = false;
        offsetRecalibrated = false;

        PlayerPrefs.DeleteKey(OverallOffsetKey);
        PlayerPrefs.DeleteKey(DailyOffsetKey);

        WriteToDisk();
        onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);

        Debug.Log($"[NewGame] Step counter reset: overall={overallSteps}, daily={stepData.dailySteps}");
    }
    public void RestartAsNewSession()
    {
        Debug.Log("[StepCounter] Restarting as new session...");

        // 1. Invalidate everything in flight
        sessionGen++;
        StopRefreshCoroutine();

        // 2. Full memory reset — same as a fresh Awake
        isLoggingOut = false;
        queryInFlight = false;
        cloudLoaded = false;
        waitingForCloudData = false;
        pendingLocalFireOnStart = false;
        initializingFreshData = false;
        offsetRecalibrated = false;
        beforeTodaySettled = false;
        isGuestLoginPending = false;
        signedInThisSession = false;
        appOpenCaptured = false;
        lastKnownDeviceCaptured = false;

        overallSteps = 0;
        overallStepsBeforeToday = 0;
        savedDailyBase = 0;
        appOpenDeviceSteps = 0;
        signInDeviceSteps = 0;
        lastKnownDeviceSteps = 0;
        appOpenTcs = new TaskCompletionSource<int>();

        stepData = new StepData
        {
            registrationTime = DateTime.Today.ToString("yyyy-MM-dd"),
            lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd")
        };

        // 3. Re-run Awake logic
        EnsureBackgroundStepCollection();
        CaptureAppOpenSteps();

        // 4. Re-run Start logic
        StartCoroutine(WaitForAuthThenLoad());
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