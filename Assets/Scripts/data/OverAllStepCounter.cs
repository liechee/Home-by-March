using UnityEngine;
using System;
using System.Collections;
using System.IO;
using Repforge.StepCounterPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

/// <summary>
/// Step counting rules (per design):
///
///  FIRST OPEN (never signed in)
///    overall = 0, daily = 0. Count steps from when the app opened.
///
///  SIGN IN mid-session (cloud load)
///    overall = cloud overall (pre-signin steps NOT added to overall)
///    daily   = cloud daily + steps walked since the app opened this session
///
///  EXIT and REOPEN (auto-signed-in from last session)
///    Always re-fetch cloud on every launch (per design).
///    Shows local file instantly while cloud loads, then updates.
///
///  NORMAL EXIT (no logout)
///    Saves current state to disk + cloud so everything is retained.
///
///  LOGOUT
///    Wipes disk, cloud, and all memory. Next player starts from zero.
///
///  DAILY RESET
///    At midnight (new calendar day). Logout also wipes it.
///
///  OVERALL
///    Lifetime accumulating total. Never resets except on logout.
/// </summary>
public class OverallStepCounter : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────

    [Tooltip("Seconds between step polls.")]
    public float refreshInterval = 0.5f;
    [Tooltip("Minimum step delta before firing onStepsUpdated.")]
    public int   stepChangeThreshold = 1;
    [Tooltip("Log every StepCounterRequest result.")]
    public bool  debugStepQueries = false;
    [Tooltip("Seconds between disk writes during normal play.")]
    public float diskSaveInterval = 10f;

    // ─────────────────────────────────────────────────────────
    //  Public State
    // ─────────────────────────────────────────────────────────

    public StepData stepData;
    public int      overallSteps;
    public int      overallStepsBeforeToday;
    public bool     cloudLoaded = false;
    public string   stepDataJsonFilePath;

    /// <summary>
    /// Set TRUE by LogOutManager as the very first action of logout.
    /// Every save method returns immediately while true so no old-player
    /// data can be written to disk or cloud after the wipe.
    /// </summary>
    [HideInInspector] public bool isLoggingOut = false;

    // ─────────────────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────────────────

    public static event Action          onLoaded;
    public static event Action<int,int> onStepsUpdated; // (overall, daily)

    // ─────────────────────────────────────────────────────────
    //  Private State
    // ─────────────────────────────────────────────────────────

    private static OverallStepCounter instance;
    private Coroutine refreshCoroutine;

    /// <summary>
    /// Incremented on every full reset. Callbacks capture this at dispatch time
    /// and self-discard if it has changed — kills all in-flight callbacks instantly.
    /// </summary>
    private int sessionGen = 0;

    /// <summary>
    /// Raw device step count the moment this app session opened.
    /// Subtracted from live device steps to get "steps since app opened".
    /// Used for: daily counting (pre-signin steps = appOpen → sign-in).
    /// NOT used for overall (overall comes purely from cloud + post-signin device delta).
    /// </summary>
    private int  appOpenDeviceSteps  = 0;
    private bool appOpenCaptured     = false;

    /// <summary>
    /// Device step count at the moment the player signed in this session.
    /// Subtracted from live device steps to get "steps since sign-in".
    /// Overall = cloudOverall + (deviceNow - deviceAtSignIn).
    /// Set to 0 if the player was already signed in when the app opened
    /// (auto-sign-in path uses cloud data directly without this offset).
    /// </summary>
    private int  signInDeviceSteps   = 0;
    private bool signedInThisSession = false;

    /// <summary>
    /// The daily step count loaded from cloud (or disk on re-open).
    /// On re-open: daily = savedDaily + (deviceNow - appOpenDeviceSteps)
    ///             so the player continues where they left off.
    /// On new day:  savedDaily = 0, daily = (deviceNow - appOpenDeviceSteps)
    /// </summary>
    private int savedDailyBase = 0;

    /// <summary>
    /// True once overallStepsBeforeToday is calculated for this session.
    /// After settling, every poll uses the fast single-query path.
    /// </summary>
    private bool beforeTodaySettled      = false;
    /// <summary>
    /// True after the first QueryTodayAndUpdate recalibrates OverallOffsetKey
    /// on a same-day disk restore. Prevents recalibration from running every poll.
    /// </summary>
    private bool offsetRecalibrated = false;

    private bool waitingForCloudData     = false;
    private bool pendingLocalFireOnStart = false;

    /// <summary>
    /// True while a StepCounterRequest is in-flight.
    /// Prevents RefreshLoop from stacking new requests before the previous
    /// one returns — a primary cause of increasing query latency after each
    /// logout, because unresolved requests pile up in the hardware sensor queue.
    /// </summary>
    private bool queryInFlight = false;

    /// <summary>
    /// The most recent device step count returned by a StepCounterRequest.
    /// Updated in QueryTodayAndUpdate on every successful callback.
    /// Used by CommitCurrentStateToDisk so it can calculate an accurate dailySteps
    /// even if the app exits before the first poll callback has completed.
    /// </summary>
    private int  lastKnownDeviceSteps = 0;
    private bool lastKnownDeviceCaptured = false;

    [HideInInspector] public bool initializingFreshData = false;

    private float lastDiskSaveTime = 0f;

    private const string OverallOffsetKey = "OverallStepOffset";
    private const string DailyOffsetKey   = "DailyStepOffset";

    // ─────────────────────────────────────────────────────────
    //  Singleton / Startup
    // ─────────────────────────────────────────────────────────

    void Awake()
    {
        stepDataJsonFilePath = Application.persistentDataPath + "/stepData.json";

        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        // Capture raw device steps at app-open as the daily zero-point.
        // This value is used for: pre-signin daily steps, and daily counting
        // after re-opening the app. It is NOT subtracted from overall.
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
        // ── Startup diagnostic — shows exactly which path is taken ──────────
        // If steps stay at 0, check these values in the log:
        Debug.Log($"[StepCounter] Start() — " +
            $"HasLoggedOut={PlayerPrefs.GetInt("HasLoggedOut", 0)}, " +
            $"initializingFreshData={initializingFreshData}, " +
            $"IsSignedIn={IsSignedIn()}, " +
            $"SuppressCloudRestore={PlayerPrefs.GetInt("SuppressCloudRestore", 0)}, " +
            $"SuppressStepQuery={PlayerPrefs.GetInt("SuppressStepQuery", 0)}, " +
            $"fileExists={File.Exists(stepDataJsonFilePath)}, " +
            $"waitingForCloudData={waitingForCloudData}");

        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1) return;
        if (initializingFreshData) return;

        // Fire deferred event now that all Awake() calls have run and
        // subscribers (UserLevel etc.) are guaranteed to be listening.
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
            // Always re-fetch cloud on every launch (per design).
            // FinalizeCloudLoad will own the refresh loop from here.
            _ = LoadStepDataFromCloud();
            return;
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

        // Always ensure the loop is running when the app regains focus.
        // The loop may have stopped if the coroutine was killed during a
        // long background period or after a scene reload.
        if (PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
        {
            if (refreshCoroutine == null)
            {
                Debug.Log("[StepCounter] Regained focus — starting RefreshLoop.");
                refreshCoroutine = StartCoroutine(RefreshLoop());
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    //  App Lifecycle
    // ─────────────────────────────────────────────────────────

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
            // App going to background — commit state immediately.
            CommitCurrentStateToDisk();
            await SaveStepDataToCloud();
        }
        else
        {
            // App returning from background — restart the loop if it stopped.
            // Unity may stop coroutines when the app is paused depending on
            // platform and background mode settings.
            if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
            {
                Debug.Log("[StepCounter] Resuming from pause — restarting RefreshLoop.");
                refreshCoroutine = StartCoroutine(RefreshLoop());
            }
        }
    }

    /// <summary>
    /// Synchronous disk write using current in-memory values.
    /// No new StepCounterRequest — safe to call at app exit/pause.
    /// </summary>
    private void CommitCurrentStateToDisk()
    {
        if (isLoggingOut) return;
        stepData.lastSaveTime  = DateTime.Today.ToString("yyyy-MM-dd");
        stepData.numberOfSteps = overallSteps;
        stepData.overallSteps  = overallSteps;

        // Recalculate dailySteps using the last known device reading so the
        // value on disk is always accurate at exit — even if the app exits
        // before diskSaveInterval has elapsed or before the first poll callback
        // has completed (which would leave stepData.dailySteps at 0).
        if (lastKnownDeviceCaptured)
        {
            int recalcDaily = CalcDailySteps(lastKnownDeviceSteps);
            if (recalcDaily >= 0)
                stepData.dailySteps = recalcDaily;
        }

        WriteToDisk();
        Debug.Log($"[EXIT] Committed — overall={overallSteps}, daily={stepData.dailySteps}");
    }

    // ─────────────────────────────────────────────────────────
    //  Step Querying
    // ─────────────────────────────────────────────────────────

    public void GetOverallSteps()
    {
        if (isLoggingOut) return;
        // Skip if a device query is already in-flight. This prevents the refresh
        // loop from stacking requests when the device sensor is slow to respond —
        // the primary cause of increasing latency after repeated logouts.
        if (queryInFlight) return;
        if (string.IsNullOrEmpty(stepData?.registrationTime) ||
            string.IsNullOrEmpty(stepData?.lastSaveTime)) return;

        int gen = sessionGen;

        // Fast path: overallStepsBeforeToday already settled — one query per poll.
        if (beforeTodaySettled)
        {
            queryInFlight     = true;
            queryDispatchTime = Time.realtimeSinceStartup;
            QueryTodayAndUpdate(gen);
            return;
        }

        // First-time path: compute overallStepsBeforeToday then settle.
        DateTime lastSave = DateTime.Parse(stepData.lastSaveTime).Date;
        int      days     = GetDaysSinceLastSave();

        if (days == 0)
        {
            // Saved today — use stored value directly.
            overallStepsBeforeToday = stepData.numberOfSteps > 0
                ? stepData.numberOfSteps - GetEstimatedTodayStepsFromDisk()
                : 0;
            beforeTodaySettled = true;
            queryInFlight     = true;
            queryDispatchTime = Time.realtimeSinceStartup;
            QueryTodayAndUpdate(gen);
        }
        else if (days == 1)
        {
            // New day — yesterday's total is the pre-today base. Daily resets.
            savedDailyBase          = 0;
            overallStepsBeforeToday = stepData.numberOfSteps;
            beforeTodaySettled      = true;
            queryInFlight     = true;
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
                beforeTodaySettled      = true;
                queryInFlight     = true;
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
                beforeTodaySettled      = true;
                queryInFlight     = true;
                queryDispatchTime = Time.realtimeSinceStartup;
                QueryTodayAndUpdate(gen);
            }).Execute();
        }
    }

    private void QueryTodayAndUpdate(int gen)
    {
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            // Always clear the in-flight flag so the next poll can proceed,
            // whether this callback is stale, a logout happened, or it is valid.
            queryInFlight = false;

            // Record the raw device count regardless of stale/logout state.
            // CommitCurrentStateToDisk uses this to compute accurate dailySteps
            // at exit even if no full poll cycle has completed yet this session.
            if (!isLoggingOut)
            {
                lastKnownDeviceSteps   = deviceNow;
                lastKnownDeviceCaptured = true;
            }

            if (IsStale(gen) || isLoggingOut) return;

            // On the first query after restoring from disk (same-day reopen),
            // OverallOffsetKey is stale — it was set at the previous session's
            // app-open, not this one. Recalibrate it so CalcTodayNetSteps gives
            // the correct delta going forward.
            //
            // Correct offset = deviceNow - todayStepsAlreadyRecorded
            // where todayStepsAlreadyRecorded = steps already in the disk save
            // for today = overallSteps (restored) - overallStepsBeforeToday.
            //
            // This only applies on the first call (before beforeTodaySettled locked in)
            // when NOT in mid-session sign-in and NOT post-logout (those have their own
            // zero-points). Cloud-loaded sessions set OverallOffsetKey themselves.
            if (!offsetRecalibrated && !signedInThisSession && stepData.baselineSteps == 0)
            {
                offsetRecalibrated = true;
                int todayAlreadyRecorded = Math.Max(0, overallSteps - overallStepsBeforeToday);
                int recalibratedOffset   = Math.Max(0, deviceNow - todayAlreadyRecorded);
                PlayerPrefs.SetInt(OverallOffsetKey, recalibratedOffset);
                PlayerPrefs.Save();
                Debug.Log($"[RECAL] OverallOffsetKey recalibrated once: deviceNow={deviceNow}, todayRecorded={todayAlreadyRecorded}, offset={recalibratedOffset}");
            }

            int prev = overallSteps;

            // Overall = everything before today + today's net steps.
            int todayNet = CalcTodayNetSteps(deviceNow);
            overallSteps = overallStepsBeforeToday + todayNet;

            // Daily = savedDailyBase (from disk/cloud) + steps since app opened today.
            int daily = CalcDailySteps(deviceNow);

            int overallDelta = Math.Abs(overallSteps - prev);
            int dailyDelta   = Math.Abs(daily - stepData.dailySteps);

            // Fire whenever overall OR daily changed meaningfully.
            // Previously only overall was checked — daily could change
            // without triggering a UI update.
            if (overallDelta >= stepChangeThreshold || dailyDelta >= stepChangeThreshold)
            {
                if (debugStepQueries)
                    Debug.Log($"[Steps] Firing update — overall: {prev}→{overallSteps} " +
                              $"(Δ{overallDelta}), daily: {stepData.dailySteps}→{daily} (Δ{dailyDelta})");
                onStepsUpdated?.Invoke(overallSteps, daily);
            }
            else if ((overallDelta > 0 || dailyDelta > 0) && debugStepQueries)
                Debug.Log($"[Steps] Change below threshold — suppressed. overallΔ={overallDelta}, dailyΔ={dailyDelta}");

            stepData.dailySteps = daily;
            SaveStepData(deviceNow, gen);
        }).Execute();
    }

    // Max seconds to wait for a StepCounterRequest before force-clearing queryInFlight.
    // If the device sensor is slow or permission was just granted, this ensures the
    // counter recovers rather than staying at 0 forever.
    private const float QueryTimeout = 5f;
    private float queryDispatchTime = 0f;

    private IEnumerator RefreshLoop()
    {
        GetOverallSteps();
        while (true)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, refreshInterval));

            // Watchdog: if a query has been in-flight longer than QueryTimeout,
            // the callback likely never fired (permission denied, sensor unavailable).
            // Force-clear so the next tick can try again.
            if (queryInFlight && (Time.realtimeSinceStartup - queryDispatchTime) > QueryTimeout)
            {
                Debug.LogWarning("[StepCounter] Query timed out — clearing queryInFlight. " +
                    "Check ACTIVITY_RECOGNITION permission is granted.");
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

    // ─────────────────────────────────────────────────────────
    //  Daily and Overall Step Calculations
    //
    //  These are the core of the design. Read carefully.
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Today's net contribution to OVERALL steps.
    ///
    /// Three cases:
    ///
    ///  POST-LOGOUT new player (baseline set):
    ///    net = deviceNow - baselineSteps
    ///    (baseline = device count at logout time, so only post-logout steps count)
    ///
    ///  SIGNED IN this session (mid-session sign-in):
    ///    net = deviceNow - signInDeviceSteps
    ///    Pre-signin steps are NOT added to overall (per design).
    ///
    ///  RETURNING player (auto-signed-in, cloud already loaded):
    ///    overall comes directly from cloud + accumulation across days.
    ///    QueryTodayAndUpdate uses overallStepsBeforeToday which was set
    ///    from cloud in FinalizeCloudLoad. So net = deviceNow - offset
    ///    where offset is the stored OverallOffsetKey (set at cloud load time
    ///    to align device steps with cloud overall).
    /// </summary>
    private int CalcTodayNetSteps(int deviceNow)
    {
        // Post-logout: baseline is the zero-point
        if (stepData.baselineSteps > 0)
            return Math.Max(0, deviceNow - stepData.baselineSteps);

        // Mid-session sign-in: only count steps since signing in
        if (signedInThisSession)
            return Math.Max(0, deviceNow - signInDeviceSteps);

        // Returning player / normal: use stored offset
        return Math.Max(0, deviceNow - PlayerPrefs.GetInt(OverallOffsetKey, 0));
    }

    /// <summary>
    /// Daily step count for display.
    ///
    /// daily = savedDailyBase + (deviceNow - appOpenDeviceSteps)
    ///
    /// savedDailyBase:
    ///   - Set from cloud daily when cloud loads (FinalizeCloudLoad)
    ///   - Set from disk daily when app reopens (LoadStepData seeds it)
    ///   - 0 on a new day (CalcTodayNetSteps clears it at midnight)
    ///   - 0 after logout (ZeroState clears it)
    ///
    /// (deviceNow - appOpenDeviceSteps) = steps walked since app opened.
    ///   For a pre-signin player this is steps since the app opened.
    ///   After sign-in, the cloud daily becomes savedDailyBase, so the
    ///   pre-signin steps already walked are naturally included.
    ///
    /// Post-logout new player uses baseline instead of appOpenDeviceSteps
    /// because the baseline was captured at logout time (which may differ
    /// from app-open if the app was not closed between logout and new session).
    /// </summary>
    private int CalcDailySteps(int deviceNow)
    {
        // Post-logout: baseline is the zero-point
        if (stepData.baselineSteps > 0)
            return Math.Max(0, deviceNow - stepData.baselineSteps);

        int stepsSinceOpen = appOpenCaptured
            ? Math.Max(0, deviceNow - appOpenDeviceSteps)
            : 0;

        return savedDailyBase + stepsSinceOpen;
    }

    // ─────────────────────────────────────────────────────────
    //  Save
    // ─────────────────────────────────────────────────────────

    public void SaveStepData(int todayDeviceSteps, int gen = -1, bool forceWrite = false)
    {
        if (isLoggingOut) return;
        if (gen >= 0 && IsStale(gen)) return;

        stepData.lastSaveTime  = DateTime.Today.ToString("yyyy-MM-dd");
        stepData.numberOfSteps = overallSteps;
        stepData.overallSteps  = overallSteps;
        stepData.dailySteps    = CalcDailySteps(todayDeviceSteps);
        if (stepData.dailySteps > stepData.overallSteps && overallSteps > 0)
            stepData.dailySteps = stepData.overallSteps;

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
        await CloudSaver.SaveDataToCloud("stepData", stepData);
    }

    // ─────────────────────────────────────────────────────────
    //  Load — Cloud
    // ─────────────────────────────────────────────────────────

    public async Task LoadStepDataFromCloud()
    {
        if (isLoggingOut) return;

        if (PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 1)
        {
            // Only lift suppression once HasLoggedOut is also cleared,
            // meaning InitializeStepDataAfterLogout has run for the new session.
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

        // Capture device steps at the moment of sign-in so CalcTodayNetSteps
        // can exclude pre-signin steps from overall (per design).
        // For auto-sign-in (already signed in when app opened), this value
        // equals appOpenDeviceSteps — net effect: today's overall from cloud
        // base is computed from the offset, which is correct.
        if (!signedInThisSession && appOpenCaptured)
        {
            signInDeviceSteps    = appOpenDeviceSteps;
            signedInThisSession  = true;
        }

        int gen = sessionGen;
        try
        {
            string json = await CloudSaver.LoadDataFromCloud("stepData");
            if (IsStale(gen) || isLoggingOut) return;

            // Preserve in-memory baseline (set by InitializeStepDataAfterLogout).
            // JsonUtility.FromJson creates a new StepData, wiping it.
            int  preservedBaseline = stepData?.baselineSteps ?? 0;
            bool preservedBaselineFlag = (stepData?.baselineSteps ?? 0) > 0;

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

            if      (daysSince == 0) ApplyCloudSameDay(cloudBase, gen);
            else if (daysSince == 1) ApplyCloudNewDay(cloudBase, gen);
            else                     ApplyCloudMultiDayGap(cloudBase, cloudDate, gen);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CLOUD] Load failed: {e.Message}");
            waitingForCloudData = false;
            cloudLoaded         = false;

            // Start() returned early handing control to FinalizeCloudLoad.
            // Since cloud failed, FinalizeCloudLoad never ran and the refresh
            // loop was never started. Start it now as the fallback.
            if (stepData != null)
            {
                onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);
                onLoaded?.Invoke();
            }
            else
            {
                LoadStepData();
            }

            // Ensure the loop is running regardless of which fallback path ran.
            if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
            {
                Debug.Log("[CLOUD] Starting RefreshLoop after cloud load failure.");
                refreshCoroutine = StartCoroutine(RefreshLoop());
            }
        }
    }

    /// <summary>
    /// Cloud was saved today.
    /// overall = cloudBase (today's delta is accounted for by OverallOffsetKey).
    /// daily   = cloud daily + steps walked since app opened this session.
    /// </summary>
    /// <summary>
    /// Cloud was saved TODAY.
    /// Daily = cloud daily + pre-signin steps (steps since app opened this session).
    /// Pre-signin steps ARE included because the data is from the same day —
    /// the player was already accumulating steps toward today's count.
    /// </summary>
    private void ApplyCloudSameDay(int cloudBase, int gen)
    {
        int cloudSavedDaily = stepData.dailySteps;

        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            if (IsStale(gen) || isLoggingOut) return;

            PlayerPrefs.SetInt(OverallOffsetKey, deviceNow);
            PlayerPrefs.Save();

            overallSteps            = cloudBase;
            overallStepsBeforeToday = cloudBase;
            beforeTodaySettled      = true;

            // Same day: carry cloud daily forward and add pre-signin steps on top.
            // savedDailyBase = cloud daily, CalcDailySteps adds (deviceNow - appOpenDeviceSteps).
            savedDailyBase      = cloudSavedDaily;
            stepData.dailySteps = CalcDailySteps(deviceNow);

            FinalizeCloudLoad();
        }).Execute();
    }

    /// <summary>
    /// Cloud was saved on a DIFFERENT DAY (yesterday).
    /// Daily resets to 0 — pre-signin steps are NOT included.
    /// The player was walking on a different day; those steps don't count toward today.
    /// We reset appOpenDeviceSteps to deviceNow so CalcDailySteps returns
    /// (savedDailyBase=0) + (deviceNow - deviceNow) = 0, then grows from sign-in.
    /// </summary>
    private void ApplyCloudNewDay(int cloudBase, int gen)
    {
        savedDailyBase = 0; // Different day — daily starts fresh from 0

        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            if (IsStale(gen) || isLoggingOut) return;

            PlayerPrefs.SetInt(OverallOffsetKey, deviceNow);
            PlayerPrefs.Save();

            // Reset the app-open zero-point to RIGHT NOW so pre-signin steps
            // (walked before this sign-in moment) are not counted in today's daily.
            appOpenDeviceSteps = deviceNow;
            appOpenCaptured    = true;

            overallSteps            = cloudBase;
            overallStepsBeforeToday = cloudBase;
            beforeTodaySettled      = true;
            stepData.dailySteps     = CalcDailySteps(deviceNow); // = 0 + (deviceNow - deviceNow) = 0

            FinalizeCloudLoad();
        }).Execute();
    }

    /// <summary>
    /// Cloud was saved 2+ DAYS ago — accumulate missed days into overall.
    /// Daily starts fresh (different day — pre-signin steps NOT included).
    /// Same reset logic as ApplyCloudNewDay.
    /// </summary>
    private void ApplyCloudMultiDayGap(int cloudBase, DateTime cloudDate, int gen)
    {
        savedDailyBase = 0; // Different day — daily starts fresh from 0

        new StepCounterRequest().From(cloudDate).To(DateTime.Today).OnQuerySuccess((range) =>
        {
            if (IsStale(gen) || isLoggingOut) return;
            int accumulated = cloudBase + range;

            new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
            {
                if (IsStale(gen) || isLoggingOut) return;

                PlayerPrefs.SetInt(OverallOffsetKey, deviceNow);
                PlayerPrefs.Save();

                // Reset the zero-point to now so pre-signin steps are excluded from daily.
                appOpenDeviceSteps = deviceNow;
                appOpenCaptured    = true;

                overallSteps            = accumulated;
                overallStepsBeforeToday = accumulated;
                beforeTodaySettled      = true;
                stepData.dailySteps     = CalcDailySteps(deviceNow); // = 0

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
        PlayerPrefs.SetInt("HasEverSignedIn", 1); // Mark so LoadStepData restores from disk on next launch
        PlayerPrefs.DeleteKey("HasLoggedOut");
        PlayerPrefs.DeleteKey("SuppressStepQuery");
        PlayerPrefs.DeleteKey("SuppressCloudRestore");
        PlayerPrefs.Save();

        cloudLoaded         = true;
        waitingForCloudData = false;
        offsetRecalibrated  = true; // Cloud sets its own offset — skip generic recalibration

        onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);
        onLoaded?.Invoke();

        // Restart the refresh loop cleanly with the settled cloud values.
        StopRefreshCoroutine();
        refreshCoroutine = StartCoroutine(RefreshLoop());

        Debug.Log($"[CLOUD] Finalized — overall={overallSteps}, daily={stepData.dailySteps}");
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

    // ─────────────────────────────────────────────────────────
    //  Load — Local
    // ─────────────────────────────────────────────────────────

    public void LoadStepData()
    {
        if (PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 1)
        { ZeroState(); FireZero(); return; }

        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
        { ZeroState(); FireZero(); return; }

        // No saved file → start counting from 0.
        // File.Exists is the primary gate — if there's a file we always restore it.
        // HasEverSignedIn is a secondary check for edge cases where the file was
        // deleted externally but the session is otherwise intact.
        if (!File.Exists(stepDataJsonFilePath))
        {
            initializingFreshData = true;
            InitializeStepData();
            return;
        }

        // Normal restore from disk
        stepData = JsonUtility.FromJson<StepData>(File.ReadAllText(stepDataJsonFilePath)) ?? new StepData();

        overallSteps            = stepData.overallSteps != 0 ? stepData.overallSteps : stepData.numberOfSteps;
        overallStepsBeforeToday = stepData.numberOfSteps;

        // Seed savedDailyBase from disk so CalcDailySteps returns
        // disk_daily + stepsSinceAppOpen on the first poll, not just
        // stepsSinceAppOpen (which would be near-0 on a fresh open).
        // If it's a new day, leave savedDailyBase = 0 (daily resets at midnight).
        bool isNewDay = !string.IsNullOrEmpty(stepData.lastSaveTime) &&
                        DateTime.Parse(stepData.lastSaveTime).Date < DateTime.Today;
        savedDailyBase = isNewDay ? 0 : stepData.dailySteps;

        if (isNewDay)
        {
            // New day: daily starts fresh. Clear today's overall offset
            // so new steps accumulate cleanly on top of yesterday's total.
            overallStepsBeforeToday = stepData.numberOfSteps;
            beforeTodaySettled      = false; // will run one range query to settle
        }

        Debug.Log($"[LOAD] overall={overallSteps}, daily={stepData.dailySteps}, savedDailyBase={savedDailyBase}, newDay={isNewDay}");

        // Defer event to Start() so all subscribers are listening.
        pendingLocalFireOnStart = true;

        // Cloud will take over from Start() if signed in.
        // Don't start the loop here if cloud is pending.
        if (waitingForCloudData) return;

        bool cloudRestored = PlayerPrefs.GetInt("CloudRestored", 0) == 1;
        if (!cloudRestored && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
            GetOverallSteps();

        if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
            refreshCoroutine = StartCoroutine(RefreshLoop());
    }

    // ─────────────────────────────────────────────────────────
    //  Initialization
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// First-ever open: count from 0. Overall = 0, daily = 0.
    /// We ignore any steps on the device before the app was opened.
    /// </summary>
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

        // We still need appOpenDeviceSteps to be the zero-point,
        // but we don't display it — just let CaptureAppOpenSteps handle it.
        // Fire (0,0) immediately as the starting display.
        onStepsUpdated?.Invoke(0, 0);

        // Start the loop — as the player walks, CalcDailySteps returns
        // (0 + stepsSinceOpen) and CalcTodayNetSteps uses OverallOffsetKey=0.
        stepData.baselineSteps = 0;

        int gen = sessionGen;
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            if (IsStale(gen)) return;
            initializingFreshData = false;

            // Set overall offset so overallSteps starts at 0 for this player
            PlayerPrefs.SetInt(OverallOffsetKey, deviceNow);
            // Mark that this device has session data so LoadStepData restores
            // from disk on the next launch instead of re-initializing from scratch.
            // This applies to both offline players and players who haven't signed in yet.
            PlayerPrefs.SetInt("HasEverSignedIn", 1);
            PlayerPrefs.Save();

            beforeTodaySettled = true;

            onLoaded?.Invoke();

            if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
                refreshCoroutine = StartCoroutine(RefreshLoop());
        }).Execute();
    }

    /// <summary>
    /// Post-logout: show (0,0) immediately, capture device steps as in-memory
    /// baseline so only steps taken AFTER logout count for the new session.
    /// HasLoggedOut is cleared once the baseline is set.
    /// </summary>
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

            // Baseline = device steps at logout time (in-memory only, never to disk)
            stepData.baselineSteps = deviceNow;
            appOpenDeviceSteps     = deviceNow; // daily also starts from here
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

    // ─────────────────────────────────────────────────────────
    //  Full Reset  (called first by LogOutManager)
    // ─────────────────────────────────────────────────────────

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

        StopRefreshCoroutine();

        if (File.Exists(stepDataJsonFilePath)) File.Delete(stepDataJsonFilePath);

        PlayerPrefs.DeleteKey(OverallOffsetKey);
        PlayerPrefs.DeleteKey(DailyOffsetKey);
        PlayerPrefs.DeleteKey("SuppressStepQuery");
        PlayerPrefs.Save();

        onStepsUpdated?.Invoke(0, 0);
    }

    // ─────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────

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
            Debug.LogWarning($"[Steps] Device ({deviceNow}) < baseline ({stepData.baselineSteps}) — clearing.");
            stepData.baselineSteps = 0;
        }
    }

    /// <summary>
    /// Estimates today's steps already accumulated in the stored numberOfSteps.
    /// Used to separate overallStepsBeforeToday from today's portion.
    /// Only called when lastSaveTime == today (days == 0 path).
    /// </summary>
    private int GetEstimatedTodayStepsFromDisk()
    {
        // The stored dailySteps is our best estimate of today's contribution.
        return stepData.dailySteps;
    }

    /// <summary>
    /// Captures the raw device step count at the moment the app opens.
    /// This is the zero-point for daily steps and pre-signin daily counting.
    /// </summary>
    private void CaptureAppOpenSteps()
    {
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((n) =>
        {
            if (appOpenCaptured) return; // Already set (e.g. from post-logout path)
            appOpenDeviceSteps = n;
            appOpenCaptured    = true;
            // Seed lastKnownDeviceSteps immediately so CommitCurrentStateToDisk
            // has a valid reading even if no full poll cycle completes before exit.
            if (!lastKnownDeviceCaptured)
            {
                lastKnownDeviceSteps    = n;
                lastKnownDeviceCaptured = true;
            }
            Debug.Log($"[SESSION] App-open steps captured: {n}");
        }).Execute();
    }

    private void WriteToDisk()
    {
        if (isLoggingOut) return;
        File.WriteAllText(stepDataJsonFilePath, JsonUtility.ToJson(stepData));
        Debug.Log($"[DISK] Saved — overall={stepData.overallSteps}, daily={stepData.dailySteps}");
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