using UnityEngine;
using System;
using System.Collections;
using System.IO;
using Repforge.StepCounterPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

public partial class OverallStepCounter : MonoBehaviour
{
    [Tooltip("Seconds between step polls.")]
    public float refreshInterval = 0.1f;

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
    private const float QueryTimeout = 1f;

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
    public int lastBroadcastDaily { get; private set; } = 0;

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
        stepData.dailySteps = Mathf.Clamp(stepData.dailySteps, 0, overallSteps);

        WriteToDisk();
        Debug.Log($"[EXIT] Committed — overall={overallSteps}, daily={stepData.dailySteps}, stepsBeforeToday={overallStepsBeforeToday}");
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
            lastBroadcastDaily = daily;
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
        lastBroadcastDaily = 0;
        onStepsUpdated?.Invoke(0, 0);
        onLoaded?.Invoke();
    }

    private void StopRefreshCoroutine()
    {
        if (refreshCoroutine != null) { StopCoroutine(refreshCoroutine); refreshCoroutine = null; }
        if (delayedGuestStartCoroutine != null) { StopCoroutine(delayedGuestStartCoroutine); delayedGuestStartCoroutine = null; }
    }

    public void SetRefreshInterval(float seconds)
    {
        refreshInterval = Mathf.Max(0.1f, seconds);
        StopRefreshCoroutine();
        refreshCoroutine = StartCoroutine(RefreshLoop());
    }

    public void SetStepsToValue(int newOverallSteps, int newDailySteps)
    {
        overallSteps = Mathf.Max(0, newOverallSteps);
        stepData.dailySteps = Mathf.Clamp(newDailySteps, 0, overallSteps);
        stepData.numberOfSteps = overallSteps;
        stepData.overallSteps = overallSteps;

        WriteToDisk();
        lastBroadcastDaily = stepData.dailySteps;
        onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);

        Debug.Log($"[NewGame] Steps set to: overall={overallSteps}, daily={stepData.dailySteps}");
    }
}
