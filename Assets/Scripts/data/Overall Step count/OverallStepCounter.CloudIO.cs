using UnityEngine;
using System;
using System.IO;
using System.Threading.Tasks;
using Repforge.StepCounterPro;

public partial class OverallStepCounter
{
    // ─────────────────────────────────────────────────────────────────────────
    // Save to cloud
    // ─────────────────────────────────────────────────────────────────────────

    public async Task SaveStepDataToCloud()
    {
        if (isLoggingOut) return;
        CommitCurrentStateToDisk();
        await CloudSaver2.SaveData("stepData", stepData);
        Debug.Log($"[CLOUD SAVE] overall={stepData.overallSteps}, daily={stepData.dailySteps}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Load from cloud
    // ─────────────────────────────────────────────────────────────────────────

    public async Task LoadStepDataFromCloud()
    {
        if (isLoggingOut) return;

        if (waitingForCloudData)
        {
            Debug.Log("[CLOUD] Load already in progress — skipping duplicate call.");
            return;
        }
        if (!appOpenCaptured)
            CaptureAppOpenSteps();

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

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[CLOUD] Received null/empty JSON — aborting cloud load to protect local data.");
                waitingForCloudData = false;
                cloudLoaded = true;
                if (stepData != null) { lastBroadcastDaily = stepData.dailySteps; onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps); onLoaded?.Invoke(); }
                else LoadStepData();
                if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
                    refreshCoroutine = StartCoroutine(RefreshLoop());
                return;
            }

            int localOverallSnapshot = overallSteps;
            int localDailySnapshot = stepData?.dailySteps ?? 0;

            StepData parsedCloud = JsonUtility.FromJson<StepData>(json);

            if (parsedCloud == null ||
                (parsedCloud.overallSteps == 0 && parsedCloud.numberOfSteps == 0 && localOverallSnapshot > 0))
            {
                Debug.LogWarning($"[CLOUD] Parsed cloud data is zero/null but local has {localOverallSnapshot} steps — aborting to protect local data.");
                waitingForCloudData = false;
                cloudLoaded = true;
                lastBroadcastDaily = stepData?.dailySteps ?? 0;
                onStepsUpdated?.Invoke(overallSteps, lastBroadcastDaily);
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

            if (stepData != null) { lastBroadcastDaily = stepData.dailySteps; onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps); onLoaded?.Invoke(); }
            else LoadStepData();

            if (refreshCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
                refreshCoroutine = StartCoroutine(RefreshLoop());
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cloud apply paths
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyCloudSameDay(int cloudBase, int gen, int localDailyBeforeCloud)
    {
        int cloudSavedDaily = Mathf.Clamp(stepData.dailySteps, 0, cloudBase);

        new StepCounterRequest().Since(DateTime.Today).OnQuerySuccess((deviceNow) =>
        {
            if (IsStale(gen) || isLoggingOut) return;

            if (cloudBase == 0 && overallSteps > 0)
            {
                Debug.LogWarning($"[CLOUD SameDay] cloudBase=0 but local overallSteps={overallSteps} — keeping local data.");
                appOpenDeviceSteps = deviceNow;
                appOpenCaptured = true;
                FinalizeCloudLoad();
                return;
            }

            int stepsBeforeToday = stepData.stepsBeforeToday > 0
                ? stepData.stepsBeforeToday
                : Math.Max(0, cloudBase - cloudSavedDaily);

            if (!appOpenCaptured)
            {
                appOpenDeviceSteps = deviceNow;
                appOpenCaptured = true;
            }
            signInDeviceSteps = deviceNow;
            signedInThisSession = false;

            overallStepsBeforeToday = stepsBeforeToday;
            beforeTodaySettled = true;
            overallSteps = cloudBase;
            readyToCount = true;

            savedDailyBase = cloudSavedDaily;
            stepData.dailySteps = cloudSavedDaily;
            stepData.baselineSteps = 0;

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

            savedDailyBase = 0;
            stepData.dailySteps = 0;

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

                savedDailyBase = 0;
                stepData.dailySteps = 0;

                PlayerPrefs.SetInt(OverallOffsetKey, deviceNow);
                PlayerPrefs.Save();

                Debug.Log($"[CLOUD MultiDay] cloudBase={cloudBase}, range={range}, accumulated={accumulated}, deviceNow={deviceNow}, daily={stepData.dailySteps}");

                FinalizeCloudLoad();
            }).Execute();
        }).Execute();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Finalize
    // ─────────────────────────────────────────────────────────────────────────

    private void FinalizeCloudLoad()
    {
        if (isLoggingOut) return;

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

        int liveDaily = Mathf.Clamp(savedDailyBase, 0, overallSteps);
        lastBroadcastDaily = liveDaily;

        onStepsUpdated?.Invoke(overallSteps, liveDaily);
        onLoaded?.Invoke();

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
}
