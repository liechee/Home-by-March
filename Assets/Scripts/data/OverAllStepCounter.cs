using UnityEngine;
using System;
using System.Collections;
using System.IO;
using Repforge.StepCounterPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

/// <summary>
/// Tracks overall and daily step counts with cloud sync.
///
/// TWO MODES:
///   Normal exit  — OnApplicationQuit/Pause save current steps to disk + cloud
///                  so they are restored exactly on next launch (even when signed in).
///   Logout       — LogOutManager sets isLoggingOut = true FIRST, which makes
///                  every save path a silent no-op. The wipe clears disk + cloud
///                  so the next player starts from zero.
/// </summary>
public class OverallStepCounter : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────

    [Tooltip("Seconds between automatic step polls.")]
    public float refreshInterval = 0.5f;
    [Tooltip("Minimum step delta before firing onStepsUpdated. Suppresses sensor jitter.")]
    public int stepChangeThreshold = 1;
    [Tooltip("Log every StepCounterRequest result.")]
    public bool debugStepQueries = false;

    // ─────────────────────────────────────────────────────────
    //  Public State
    // ─────────────────────────────────────────────────────────

    public StepData stepData;
    public int overallSteps;
    public int overallStepsBeforeToday;
    public bool cloudLoaded = false;
    public string stepDataJsonFilePath;

    /// <summary>
    /// Set TRUE by LogOutManager as the very first action of logout.
    /// Every save method checks this and returns immediately while true.
    /// This ensures no old-player data can be written to disk or cloud
    /// after the wipe — even from in-flight async callbacks.
    /// Reset to false once the new session is initialized in Awake.
    /// </summary>
    public bool isLoggingOut = false;

    // ─────────────────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────────────────

    public static event Action onLoaded;
    public static event Action<int, int> onStepsUpdated; // (overall, daily)

    // ─────────────────────────────────────────────────────────
    //  Private State
    // ─────────────────────────────────────────────────────────

    private static OverallStepCounter instance;
    private Coroutine refreshCoroutine;
    private bool baselineEstablished = false;
    private bool waitingForCloudData = false;

    /// <summary>
    /// Incremented on every full reset. Every StepCounterRequest callback
    /// captures the generation at dispatch time and self-discards if stale.
    /// This instantly kills all in-flight callbacks from the previous session.
    /// </summary>
    private int sessionGen = 0;

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

        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
        {
            // Post-logout: zero everything and capture a fresh baseline.
            // isLoggingOut is set by LogOutManager before we reach here,
            // so we explicitly clear it now — the new session starts clean.
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
        // Post-logout is fully handled in Awake. Returning here avoids
        // Start() triggering a cloud load with stale credentials.
        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1) return;

        bool signedIn      = IsSignedIn();
        bool suppressCloud = PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 1;

        if (signedIn && !suppressCloud)
            _ = LoadStepDataFromCloud();

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

        if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
            refreshCoroutine = StartCoroutine(RefreshLoop());
    }

    // ─────────────────────────────────────────────────────────
    //  App Lifecycle
    //
    //  NORMAL EXIT  → saves to disk AND cloud so the player sees
    //                 exactly their last-known steps on next launch,
    //                 whether they were signed in or not.
    //
    //  LOGOUT EXIT  → isLoggingOut = true; both methods below are
    //                 no-ops. LogOutManager already wiped disk + cloud.
    // ─────────────────────────────────────────────────────────

    async void OnApplicationQuit()
    {
        if (isLoggingOut) return;
        await SaveStepDataAsync();
        await SaveStepDataToCloud();
    }

    async void OnApplicationPause(bool isPaused)
    {
        if (!isPaused || isLoggingOut) return;
        await SaveStepDataAsync();
    }

    // ─────────────────────────────────────────────────────────
    //  Step Querying
    // ─────────────────────────────────────────────────────────

    public void GetOverallSteps()
    {
        if (isLoggingOut) return;
        if (!baselineEstablished && stepData.baselineSteps > 0)
        {
            if (debugStepQueries) Debug.Log("[Steps] Waiting for baseline...");
            return;
        }
        if (string.IsNullOrEmpty(stepData?.registrationTime) || string.IsNullOrEmpty(stepData?.lastSaveTime))
            return;

        int      gen     = sessionGen;
        DateTime regDate = DateTime.Parse(stepData.registrationTime).Date;
        DateTime lastSave = DateTime.Parse(stepData.lastSaveTime).Date;
        int      days    = GetDaysSinceLastSave();

        if (regDate == lastSave)
        {
            new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((n) =>
            {
                if (IsStale(gen)) return;
                ValidateBaseline(n);
                int prev = overallSteps;
                overallSteps = stepData.baselineSteps > 0
                    ? Math.Max(0, n - stepData.baselineSteps)
                    : Math.Max(0, n - PlayerPrefs.GetInt(OverallOffsetKey, 0));
                FireIfChanged(prev, n);
                SaveStepData(gen);
            }).Execute();
        }
        else if (days == 0)
        {
            overallSteps = stepData.numberOfSteps;
            SaveStepData(gen);
        }
        else if (days == 1)
        {
            overallStepsBeforeToday = stepData.numberOfSteps;
            new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((n) =>
            {
                if (IsStale(gen)) return;
                ValidateBaseline(n);
                int prev = overallSteps;
                overallSteps = overallStepsBeforeToday + ResolveTodaySteps(n);
                FireIfChanged(prev, n);
                SaveStepData(gen);
            }).Execute();
        }
        else if (days <= 10)
        {
            overallStepsBeforeToday = stepData.numberOfSteps;
            new StepCounterRequest().From(lastSave).To(DateTime.Today).OnQuerySuccess((range) =>
            {
                if (IsStale(gen)) return;
                overallStepsBeforeToday += range;
                new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((n) =>
                {
                    if (IsStale(gen)) return;
                    int prev = overallSteps;
                    overallSteps = stepData.baselineSteps == 0
                        ? Mathf.Max(0, overallStepsBeforeToday + ResolveTodaySteps(n) - PlayerPrefs.GetInt(OverallOffsetKey, 0))
                        : overallStepsBeforeToday + n;
                    FireIfChanged(prev, n);
                    SaveStepData(gen);
                }).Execute();
            }).Execute();
        }
        else
        {
            overallStepsBeforeToday = stepData.numberOfSteps;
            new StepCounterRequest().From(DateTime.Today.AddDays(-days)).To(DateTime.Today).OnQuerySuccess((range) =>
            {
                if (IsStale(gen)) return;
                overallStepsBeforeToday = range;
                new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((n) =>
                {
                    if (IsStale(gen)) return;
                    int prev = overallSteps;
                    overallSteps = stepData.baselineSteps == 0
                        ? Mathf.Max(0, overallStepsBeforeToday + ResolveTodaySteps(n) - PlayerPrefs.GetInt(OverallOffsetKey, 0))
                        : overallStepsBeforeToday + n;
                    FireIfChanged(prev, n);
                    SaveStepData(gen);
                }).Execute();
            }).Execute();
        }
    }

    private IEnumerator RefreshLoop()
    {
        // Fire immediately on start so there is no wait for the first tick.
        // This eliminates the refreshInterval delay on every scene load and
        // after the post-logout baseline is established.
        GetOverallSteps();
        while (true)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, refreshInterval));
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
    //  Save — Local + Cloud
    // ─────────────────────────────────────────────────────────

    public void SaveStepData(int gen = -1)
    {
        if (isLoggingOut) return;
        if (gen >= 0 && IsStale(gen)) return;

        stepData.lastSaveTime  = DateTime.Today.ToString("yyyy-MM-dd");
        stepData.numberOfSteps = overallSteps;
        stepData.overallSteps  = overallSteps;

        int capturedGen = sessionGen;
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((todaySteps) =>
        {
            if (isLoggingOut || IsStale(capturedGen)) return;
            stepData.dailySteps = CalcDailySteps(todaySteps);
            if (stepData.dailySteps > stepData.overallSteps)
                stepData.dailySteps = stepData.overallSteps;
            WriteToDisk();
        }).Execute();
    }

    public async Task SaveStepDataAsync()
    {
        if (isLoggingOut) return;

        stepData.lastSaveTime  = DateTime.Today.ToString("yyyy-MM-dd");
        stepData.numberOfSteps = overallSteps;
        stepData.overallSteps  = overallSteps;

        int capturedGen = sessionGen;
        var tcs = new TaskCompletionSource<bool>();
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((todaySteps) =>
        {
            if (!isLoggingOut && !IsStale(capturedGen))
            {
                stepData.dailySteps = CalcDailySteps(todaySteps);
                if (stepData.dailySteps > stepData.overallSteps)
                    stepData.dailySteps = stepData.overallSteps;
                WriteToDisk();
            }
            tcs.SetResult(true);
        }).Execute();

        await tcs.Task;
    }

    /// <summary>
    /// Called by PlayerPrefsCloudSyncButton.SaveToCloud() and OnApplicationQuit.
    /// No-op during logout — cloud was already wiped by LogOutManager.
    /// </summary>
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

        // SuppressCloudRestore is set at logout and must stay until
        // HasLoggedOut is cleared (done in InitializeStepDataAfterLogout).
        // This prevents old-player cloud data loading during the sign-out window.
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

        int gen = sessionGen;
        try
        {
            string json = await CloudSaver.LoadDataFromCloud("stepData");
            if (IsStale(gen) || isLoggingOut) return;

            stepData = JsonUtility.FromJson<StepData>(json);
            Debug.Log($"[CLOUD] Loaded: overall={stepData.overallSteps}, daily={stepData.dailySteps}, last={stepData.lastSaveTime}");

            DateTime cloudDate = DateTime.Parse(stepData.lastSaveTime).Date;
            int      daysSince = (DateTime.Today - cloudDate).Days;
            int      cloudBase = stepData.overallSteps != 0 ? stepData.overallSteps : stepData.numberOfSteps;

            if      (daysSince == 0) ApplyCloudSameDay(cloudBase, gen);
            else if (daysSince == 1) ApplyCloudOneDayGap(cloudBase, gen);
            else                     ApplyCloudMultiDayGap(cloudBase, cloudDate, gen);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CLOUD] Load failed: {e.Message}");
            waitingForCloudData = false;
            if (stepData != null) { onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps); onLoaded?.Invoke(); }
            else LoadStepData();
        }
    }

    private void ApplyCloudSameDay(int cloudBase, int gen)
    {
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((today) =>
        {
            if (IsStale(gen) || isLoggingOut) return;
            overallSteps            = cloudBase;
            overallStepsBeforeToday = cloudBase;
            stepData.dailySteps     = Math.Max(0, today - PlayerPrefs.GetInt(DailyOffsetKey, 0));
            FinalizeCloudLoad();
        }).Execute();
    }

    private void ApplyCloudOneDayGap(int cloudBase, int gen)
    {
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((today) =>
        {
            if (IsStale(gen) || isLoggingOut) return;
            overallSteps            = cloudBase + today;
            overallStepsBeforeToday = cloudBase;
            stepData.dailySteps     = Math.Max(0, today - PlayerPrefs.GetInt(DailyOffsetKey, 0));
            FinalizeCloudLoad();
        }).Execute();
    }

    private void ApplyCloudMultiDayGap(int cloudBase, DateTime cloudDate, int gen)
    {
        new StepCounterRequest().From(cloudDate).To(DateTime.Today).OnQuerySuccess((range) =>
        {
            if (IsStale(gen) || isLoggingOut) return;
            int acc = cloudBase + range;
            new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((today) =>
            {
                if (IsStale(gen) || isLoggingOut) return;
                overallSteps            = acc + today;
                overallStepsBeforeToday = acc;
                stepData.dailySteps     = Math.Max(0, today - PlayerPrefs.GetInt(DailyOffsetKey, 0));
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

        onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);

        // Clear all logout flags — new player's session is now live.
        PlayerPrefs.SetInt("CloudRestored", 1);
        PlayerPrefs.DeleteKey("HasLoggedOut");
        PlayerPrefs.DeleteKey("SuppressStepQuery");
        PlayerPrefs.DeleteKey("SuppressCloudRestore");
        PlayerPrefs.Save();

        cloudLoaded         = true;
        waitingForCloudData = false;
        onLoaded?.Invoke();

        Debug.Log($"[CLOUD] Session established — Overall: {overallSteps}, Daily: {stepData.dailySteps}");
    }

    // ─────────────────────────────────────────────────────────
    //  Load — Local
    // ─────────────────────────────────────────────────────────

    public void LoadStepData()
    {
        // Logout suppression active: show zeros until new player signs in
        if (PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 1)
        {
            ZeroState(); onStepsUpdated?.Invoke(0, 0); onLoaded?.Invoke(); return;
        }

        // HasLoggedOut without SuppressCloudRestore means partial wipe — stay safe
        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
        {
            ZeroState(); onStepsUpdated?.Invoke(0, 0); onLoaded?.Invoke(); return;
        }

        // Brand-new device, never signed in
        if (PlayerPrefs.GetInt("HasEverSignedIn", 0) == 0)
        {
            InitializeStepData(); return;
        }

        // No local file
        if (!File.Exists(stepDataJsonFilePath))
        {
            InitializeStepData(); return;
        }

        // ── Normal path: restore last saved state ─────────────────────────────
        // This handles both:
        //   • Normal exit while signed in  → restores cloud-synced steps
        //   • Normal exit while offline    → restores locally saved steps
        stepData = JsonUtility.FromJson<StepData>(File.ReadAllText(stepDataJsonFilePath)) ?? new StepData();

        overallSteps            = stepData.overallSteps != 0 ? stepData.overallSteps : stepData.numberOfSteps;
        overallStepsBeforeToday = stepData.numberOfSteps;
        baselineEstablished     = stepData.baselineSteps > 0;

        // Discard baseline if older than 1 day
        if (stepData.baselineSteps > 0 && !string.IsNullOrEmpty(stepData.registrationTime))
        {
            int age = (DateTime.Today - DateTime.Parse(stepData.registrationTime).Date).Days;
            if (age > 1) { stepData.baselineSteps = 0; baselineEstablished = false; }
        }

        // Always fire immediately from local file so the UI is never blank.
        // If cloud data is pending it will fire onStepsUpdated again when it arrives,
        // which simply overwrites these values. Local file is always the correct
        // last-known state — it was saved on the previous app exit.
        onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);
        onLoaded?.Invoke();

        bool cloudRestored = PlayerPrefs.GetInt("CloudRestored", 0) == 1;
        if (!cloudRestored && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
            GetOverallSteps();

        if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
            refreshCoroutine = StartCoroutine(RefreshLoop());
    }

    // ─────────────────────────────────────────────────────────
    //  Initialization
    // ─────────────────────────────────────────────────────────

    public void InitializeStepData()
    {
        stepData = new StepData();
        int gen  = sessionGen;
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((n) =>
        {
            if (IsStale(gen)) return;
            overallSteps              = n;
            stepData.registrationTime = DateTime.Today.ToString("yyyy-MM-dd");
            stepData.lastSaveTime     = DateTime.Today.ToString("yyyy-MM-dd");
            stepData.numberOfSteps    = overallSteps;
            SaveStepData(gen);
        }).Execute();
    }

    /// <summary>
    /// Post-logout initialization. Fires (0,0) immediately so UI shows zeros,
    /// then captures current device steps as an in-memory baseline so only steps
    /// taken AFTER logout count for the new session. HasLoggedOut is cleared once
    /// the baseline is confirmed. SuppressCloudRestore stays until the new player
    /// signs in and FinalizeCloudLoad runs.
    /// </summary>
    public void InitializeStepDataAfterLogout()
    {
        stepData = new StepData
        {
            registrationTime = DateTime.Today.ToString("yyyy-MM-dd"),
            lastSaveTime     = DateTime.Today.ToString("yyyy-MM-dd")
        };
        overallSteps = 0;
        onStepsUpdated?.Invoke(0, 0);

        int gen = sessionGen;
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceSteps) =>
        {
            if (IsStale(gen)) return;

            stepData.baselineSteps = deviceSteps; // in-memory ONLY — never written to disk
            baselineEstablished    = true;

            PlayerPrefs.DeleteKey("HasLoggedOut");
            PlayerPrefs.DeleteKey("SuppressStepQuery");
            PlayerPrefs.Save();

            Debug.Log($"[LOGOUT INIT] Baseline {deviceSteps} set (in-memory). HasLoggedOut cleared.");

            // Fire immediately so the UI shows 0 without waiting for RefreshLoop's first tick.
            // The new player genuinely has 0 net steps at this point (baseline just set),
            // so this is accurate — not a placeholder.
            onStepsUpdated?.Invoke(0, 0);

            // Start refresh loop — all subsequent real-time updates come from here.
            // The first GetOverallSteps() inside the loop will correctly subtract the
            // baseline and show any steps taken since logout began.
            if (refreshCoroutine == null)
                refreshCoroutine = StartCoroutine(RefreshLoop());
        }).Execute();
    }

    // ─────────────────────────────────────────────────────────
    //  Full Reset  (called by LogOutManager)
    // ─────────────────────────────────────────────────────────

    public void ResetStepDataCompletely()
    {
        sessionGen++;
        Debug.Log($"[RESET] Session gen → {sessionGen}. All in-flight callbacks invalidated.");

        stepData                = new StepData();
        overallSteps            = 0;
        overallStepsBeforeToday = 0;
        cloudLoaded             = false;
        baselineEstablished     = false;
        waitingForCloudData     = false;

        StopRefreshCoroutine();

        if (File.Exists(stepDataJsonFilePath))
            File.Delete(stepDataJsonFilePath);

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

    private bool IsStale(int gen) => gen != sessionGen;

    private void ValidateBaseline(int deviceSteps)
    {
        if (stepData.baselineSteps > 0 && deviceSteps < stepData.baselineSteps)
        {
            Debug.LogWarning($"[Steps] Device ({deviceSteps}) < baseline ({stepData.baselineSteps}) — resetting.");
            stepData.baselineSteps = 0;
            baselineEstablished    = false;
        }
    }

    private int ResolveTodaySteps(int deviceSteps)
    {
        if (stepData.baselineSteps > 0 && !string.IsNullOrEmpty(stepData.registrationTime))
            if (DateTime.Today == DateTime.Parse(stepData.registrationTime).Date)
                return Math.Max(0, deviceSteps - stepData.baselineSteps);
        return Math.Max(0, deviceSteps - PlayerPrefs.GetInt(OverallOffsetKey, 0));
    }

    private int CalcDailySteps(int deviceSteps)
    {
        if (stepData.baselineSteps > 0 && !string.IsNullOrEmpty(stepData.registrationTime))
        {
            bool onLogoutDay = DateTime.Today == DateTime.Parse(stepData.registrationTime).Date;
            return onLogoutDay ? Math.Max(0, deviceSteps - stepData.baselineSteps) : deviceSteps;
        }
        return Math.Max(0, deviceSteps - PlayerPrefs.GetInt(DailyOffsetKey, 0));
    }

    private void FireIfChanged(int previousSteps, int deviceSteps)
    {
        int delta = Math.Abs(overallSteps - previousSteps);
        if (delta < stepChangeThreshold)
        {
            if (delta > 0 && debugStepQueries)
                Debug.Log($"[Steps] Delta {delta} below threshold ({stepChangeThreshold}) — suppressed.");
            return;
        }
        onStepsUpdated?.Invoke(overallSteps, CalcDailySteps(deviceSteps));
    }

    private void WriteToDisk()
    {
        if (isLoggingOut) return;

        int  originalBaseline = stepData.baselineSteps;
        bool onLogoutDay = stepData.baselineSteps > 0 &&
                           !string.IsNullOrEmpty(stepData.registrationTime) &&
                           DateTime.Today == DateTime.Parse(stepData.registrationTime).Date;

        stepData.baselineSteps = onLogoutDay ? 0 : originalBaseline;
        File.WriteAllText(stepDataJsonFilePath, JsonUtility.ToJson(stepData));
        stepData.baselineSteps = originalBaseline;
    }

    private void ZeroState()
    {
        stepData                = new StepData();
        overallSteps            = 0;
        overallStepsBeforeToday = 0;
        baselineEstablished     = false;
        waitingForCloudData     = false;
        StopRefreshCoroutine();
    }

    private void StopRefreshCoroutine()
    {
        if (refreshCoroutine == null) return;
        StopCoroutine(refreshCoroutine);
        refreshCoroutine = null;
    }
}