using UnityEngine;
using System.Collections;
using System.Threading.Tasks;

public partial class OverallStepCounter
{
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

        sessionGen++;
        beforeTodaySettled = false;
        offsetRecalibrated = false;
        StopRefreshCoroutine();

        Debug.Log("[StepCounter] OnAuthStateChanged — account sign-in confirmed, loading from cloud.");
        await LoadStepDataFromCloud();
    }

    public void PrepareForLogout()
    {
        StopRefreshCoroutine();
        sessionGen++;
        cloudLoaded = false;
        waitingForCloudData = false;
        beforeTodaySettled = false;
        readyToCount = false;
        offsetRecalibrated = false;
        signedInThisSession = false;
        appOpenCaptured = false;
        appOpenTcs = new System.Threading.Tasks.TaskCompletionSource<int>();

        overallSteps = 0;
        overallStepsBeforeToday = 0;
        savedDailyBase = 0;
        lastKnownDeviceSteps = 0;
        lastKnownDeviceCaptured = false;
        stepData = new StepData
        {
            registrationTime = System.DateTime.Today.ToString("yyyy-MM-dd"),
            lastSaveTime = System.DateTime.Today.ToString("yyyy-MM-dd")
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

        if (!isGuest && IsSignedIn() && !suppressCloud)
        {
            Debug.Log("[StepCounter] Signed-in account detected on start — loading from cloud.");
            if (!waitingForCloudData && !cloudLoaded)
                _ = LoadStepDataFromCloud();
            yield break;
        }

        if (pendingLocalFireOnStart)
        {
            pendingLocalFireOnStart = false;
            lastBroadcastDaily = stepData.dailySteps;
            onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);
            onLoaded?.Invoke();
        }

        if (!cloudLoaded)
            GetOverallSteps();

        if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
            refreshCoroutine = StartCoroutine(RefreshLoop());
    }
}
