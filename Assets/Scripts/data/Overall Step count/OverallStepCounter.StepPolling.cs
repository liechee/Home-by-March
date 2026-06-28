using UnityEngine;
using System;
using System.Collections;
using Repforge.StepCounterPro;

public partial class OverallStepCounter
{
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
            readyToCount = true;
            queryInFlight = true;
            queryDispatchTime = Time.realtimeSinceStartup;
            QueryTodayAndUpdate(gen);
        }
        else if (days == 1)
        {
            savedDailyBase = 0;
            overallStepsBeforeToday = stepData.numberOfSteps;
            beforeTodaySettled = true;
            readyToCount = true;
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
                readyToCount = true;
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

            overallSteps = overallStepsBeforeToday + daily;
            overallSteps = Mathf.Max(overallSteps, prev);

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

            lastBroadcastDaily = daily;
            onStepsUpdated?.Invoke(overallSteps, daily);

            SaveStepData(deviceNow, gen, dailyOverride: daily);
        }).Execute();
    }

    private IEnumerator RefreshLoop()
    {
        while (true)
        {
            if (!waitingForCloudData && readyToCount)
                GetOverallSteps();
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, refreshInterval));

            if (queryInFlight && (Time.realtimeSinceStartup - queryDispatchTime) > QueryTimeout)
            {
                Debug.LogWarning("[StepCounter] Query timed out — clearing in-flight flag and retrying.");
                queryInFlight = false;
                if (!appOpenCaptured)
                {
                    appOpenTcs = new System.Threading.Tasks.TaskCompletionSource<int>();
                    CaptureAppOpenSteps();
                }
            }
        }
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
}
