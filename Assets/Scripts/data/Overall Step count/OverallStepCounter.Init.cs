using UnityEngine;
using System;
using System.Collections;
using System.IO;
using Repforge.StepCounterPro;

public partial class OverallStepCounter
{
    // ─────────────────────────────────────────────────────────────────────────
    // Local load
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

        if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0 && !waitingForCloudData)
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
        lastBroadcastDaily = 0;
        onStepsUpdated?.Invoke(0, 0);

        int gen = sessionGen;
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            if (IsStale(gen)) return;
            initializingFreshData = false;

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

    // ─────────────────────────────────────────────────────────────────────────
    // New game
    // ─────────────────────────────────────────────────────────────────────────

    public void ReInitializeForNewGame()
    {
        Debug.Log("[NewGame] Re-initializing OverallStepCounter for new session...");

        sessionGen++;
        StopRefreshCoroutine();
        if (delayedGuestStartCoroutine != null)
        {
            StopCoroutine(delayedGuestStartCoroutine);
            delayedGuestStartCoroutine = null;
        }

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
        appOpenTcs = new System.Threading.Tasks.TaskCompletionSource<int>();

        stepData = new StepData
        {
            registrationTime = DateTime.Today.ToString("yyyy-MM-dd"),
            lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd"),
            baselineSteps = 0,
            overallSteps = 0,
            numberOfSteps = 0,
            dailySteps = 0
        };

        lastBroadcastDaily = 0;
        onStepsUpdated?.Invoke(0, 0);

        int gen = sessionGen;
        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            queryInFlight = false;
            if (IsStale(gen)) return;
            if (isGuestLoginPending) return;

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

            refreshCoroutine = StartCoroutine(RefreshLoop());
        }).Execute();

        StartCoroutine(NewGameBaselineTimeout(gen));
    }

    private IEnumerator NewGameBaselineTimeout(int gen)
    {
        yield return new WaitForSeconds(5f);

        if (IsStale(gen)) yield break;
        if (appOpenCaptured) yield break;

        Debug.LogWarning("[NewGame] Baseline query timed out — starting RefreshLoop with zero baseline.");
        beforeTodaySettled = true;

        lastBroadcastDaily = 0;
        onStepsUpdated?.Invoke(0, 0);
        onLoaded?.Invoke();

        if (refreshCoroutine == null)
            refreshCoroutine = StartCoroutine(RefreshLoop());
    }

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
        lastBroadcastDaily = stepData.dailySteps;
        onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);

        Debug.Log($"[NewGame] Step counter reset: overall={overallSteps}, daily={stepData.dailySteps}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Logout / reset
    // ─────────────────────────────────────────────────────────────────────────

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

        lastBroadcastDaily = stepData.dailySteps;
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
        appOpenTcs = new System.Threading.Tasks.TaskCompletionSource<int>();

        StopRefreshCoroutine();

        if (File.Exists(stepDataJsonFilePath)) File.Delete(stepDataJsonFilePath);

        PlayerPrefs.DeleteKey(OverallOffsetKey);
        PlayerPrefs.DeleteKey(DailyOffsetKey);
        PlayerPrefs.DeleteKey("SuppressStepQuery");
        PlayerPrefs.Save();

        lastBroadcastDaily = 0;
        onStepsUpdated?.Invoke(0, 0);
    }

    public void RestartAsNewSession()
    {
        Debug.Log("[StepCounter] Restarting as new session...");

        sessionGen++;
        StopRefreshCoroutine();

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
        appOpenTcs = new System.Threading.Tasks.TaskCompletionSource<int>();

        stepData = new StepData
        {
            registrationTime = DateTime.Today.ToString("yyyy-MM-dd"),
            lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd")
        };

        EnsureBackgroundStepCollection();
        CaptureAppOpenSteps();

        StartCoroutine(WaitForAuthThenLoad());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Guest login
    // ─────────────────────────────────────────────────────────────────────────

    public void PrepareForGuestLogin()
    {
        Debug.Log("[StepCounter] Preparing for guest login - will start counting when main screen loads");

        PlayerPrefs.SetInt("IsGuestSession", 1);
        PlayerPrefs.SetInt("SuppressCloudRestore", 1);
        PlayerPrefs.SetInt("GuestLoginPending", 1);
        PlayerPrefs.SetString("LastLoginMethod", "Guest");
        PlayerPrefs.Save();

        isGuestLoginPending = true;
    }

    public void InitializeForGuestLogin()
    {
        PlayerPrefs.SetInt("SuppressCloudRestore", 1);
        PlayerPrefs.SetInt("IsGuestSession", 1);
        PlayerPrefs.SetString("LastLoginMethod", "Guest");
        PlayerPrefs.DeleteKey("HasLoggedOut");
        PlayerPrefs.SetInt("SuppressStepQuery", 0);
        PlayerPrefs.SetInt("StepCountingActive", 1);
        PlayerPrefs.SetInt("GuestLoginStepCountingStarted", 1);
        PlayerPrefs.DeleteKey("GuestLoginPending");
        PlayerPrefs.Save();

        isLoggingOut = false;
        queryInFlight = false;
        waitingForCloudData = false;
        isGuestLoginPending = false;

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
        appOpenTcs = new System.Threading.Tasks.TaskCompletionSource<int>();

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

        beforeTodaySettled = true;
        int gen = ++sessionGen;

        Debug.Log("[StepCounter] Executing StepCounterRequest for guest login...");

        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            if (IsStale(gen)) return;

            appOpenDeviceSteps = deviceNow;
            appOpenCaptured = true;

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
            lastBroadcastDaily = 0;

            onStepsUpdated?.Invoke(0, 0);
            onLoaded?.Invoke();

            if (refreshCoroutine == null)
                refreshCoroutine = StartCoroutine(RefreshLoop());

            isGuestLoginPending = false;

            Debug.Log($"[GuestLogin] Baseline anchored at {deviceNow}. Guest starts at 0.");
        }).Execute();

        StartCoroutine(GuestLoginTimeoutFallback(gen));
    }

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

        yield return new WaitForSeconds(0.5f);

        InitializeForGuestLogin();

        delayedGuestStartCoroutine = null;
    }

    private IEnumerator GuestLoginTimeoutFallback(int gen)
    {
        yield return new WaitForSeconds(5f);

        if (isGuestLoginPending && !IsStale(gen))
        {
            Debug.LogWarning("[StepCounter] Guest login step query timeout - setting default values");

            overallSteps = 0;
            stepData.dailySteps = 0;
            lastBroadcastDaily = 0;
            beforeTodaySettled = true;
            readyToCount = true;

            onStepsUpdated?.Invoke(0, 0);

            if (refreshCoroutine == null)
                refreshCoroutine = StartCoroutine(RefreshLoop());

            isGuestLoginPending = false;
        }
    }
}
