using TMPro;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine.Android;
using UnityEngine.InputSystem;
using Repforge.StepCounterPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

public class OverallStepCounter : MonoBehaviour
{

    public StepData stepData;
    public int overallSteps;
    public string stepDataJsonFilePath;
    public int overallStepsBeforeToday;
    public bool cloudLoaded = false;
    public static event Action onLoaded;
    private Coroutine refreshStepsCoroutine;
    [Tooltip("Polling interval (seconds) used to refresh step counts. Lower = more responsive, higher = less battery usage.")]
    public float refreshInterval = 0.5f;  // Default 0.5s for real-time feel
    [Tooltip("Enable verbose logging for step query results (for debugging only)")]
    public bool debugStepQueries = false;
    [Tooltip("Minimum step change threshold before firing update event. Prevents jitter from device noise.")]
    public int stepChangeThreshold = 5;  // Only update UI if change >= 5 steps
    private const string OverallStepOffsetKey = "OverallStepOffset";
    private const string DailyStepOffsetKey = "DailyStepOffset";
    public static event Action<int, int> onStepsUpdated; // overall, daily
    private bool baselineEstablished = false;
    private static OverallStepCounter instance;
    private bool waitingForCloudData = false;  // Flag to suppress local events if cloud load pending
    
    void Awake()
    {
        stepDataJsonFilePath = Application.persistentDataPath + "/stepData.json";
        
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(this.gameObject);

        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
        {
            ResetStepDataCompletely();
            PlayerPrefs.SetInt("SuppressStepQuery", 1);
            PlayerPrefs.Save();
            InitializeStepDataAfterLogout();
            return;
        }

        bool signedIn = IsServiceSignedIn();
        if (signedIn && PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 0)
            waitingForCloudData = true;

        LoadStepData();
    }
    void Start()
    {
        bool signedIn = IsServiceSignedIn();
        bool suppressCloudRestore = PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 1;

        if (signedIn && suppressCloudRestore)
        {
            PlayerPrefs.DeleteKey("SuppressCloudRestore");
            PlayerPrefs.Save();
        }

        if (signedIn && !suppressCloudRestore)
            _ = LoadStepDataFromCloud();

        if (!cloudLoaded)
            GetOverallSteps();

        if (refreshStepsCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
            refreshStepsCoroutine = StartCoroutine(RefreshStepsLoop());
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) return;

        bool signedIn = IsServiceSignedIn();
        if (signedIn && PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 0)
        {
            if (!cloudLoaded)
                _ = LoadStepDataFromCloud();
            else
                GetOverallSteps();
        }

        if (refreshStepsCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
            refreshStepsCoroutine = StartCoroutine(RefreshStepsLoop());
    }

    public void GetOverallSteps()
    {
        float queryStartTime = Time.realtimeSinceStartup;
        if (debugStepQueries) Debug.Log($"[StepCounter] Running GetOverallSteps at {queryStartTime:F2}s...");
        if (!baselineEstablished && stepData.baselineSteps > 0)
        {
            Debug.Log("[StepCounter] Waiting for baseline to be established...");
            return;
        }

        if (string.IsNullOrEmpty(stepData.registrationTime) || string.IsNullOrEmpty(stepData.lastSaveTime))
            return;

        StepCounterRequest request = new StepCounterRequest();
        DateTime registrationTime = DateTime.Parse(stepData.registrationTime).Date;
        DateTime lastSaveTime = DateTime.Parse(stepData.lastSaveTime).Date;
        int daysSinceLastSave = GetDaysSinceLastSave();

        if (registrationTime == lastSaveTime)
        {
            // Same day as registration/logout
            request.Since(DateTime.Today).OnQuerySuccess((stepCount) =>
            {

                // 🔴 CRITICAL BUG CHECK: If device steps < baseline, baseline is invalid (from old player)
                if (stepData.baselineSteps > 0 && stepCount < stepData.baselineSteps)
                {
                    Debug.LogError($"[GetOverallSteps] ⚠️ INVALID STATE: Device steps ({stepCount}) < Baseline ({stepData.baselineSteps})! " +
                        $"This means baseline is from old player. Resetting baseline to 0.");
                    stepData.baselineSteps = 0;
                    baselineEstablished = false;
                }

                int previousSteps = overallSteps;

                if (stepData.baselineSteps > 0)
                {
                    // Post-logout: Both daily and overall show steps taken after logout (SAME DAY ONLY)
                    overallSteps = Math.Max(0, stepCount - stepData.baselineSteps);
                    Debug.Log($"[GetOverallSteps] Post-logout SAME DAY - Both: {stepCount} - {stepData.baselineSteps} = {overallSteps}");
                }
                else
                {
                    // Normal mode: Apply offset
                    int offset = PlayerPrefs.GetInt(OverallStepOffsetKey, 0);
                    overallSteps = Mathf.Max(0, stepCount - offset);
                    Debug.Log($"[GetOverallSteps] Normal mode: {stepCount} - {offset} = {overallSteps}");
                }
                // Fire event only if steps changed by threshold (debounce jitter)
                int stepDelta = Math.Abs(overallSteps - previousSteps);
                if (stepDelta >= stepChangeThreshold)
                {
                    int dailyOffset = PlayerPrefs.GetInt(DailyStepOffsetKey, 0);
                    int dailySteps = stepData.baselineSteps > 0 ? Math.Max(0, stepCount - stepData.baselineSteps) : Math.Max(0, stepCount - dailyOffset);
                    if (debugStepQueries) Debug.Log($"[GetOverallSteps] Step change: {previousSteps} → {overallSteps} (delta: {stepDelta})");
                    onStepsUpdated?.Invoke(overallSteps, dailySteps);
                }
                else if (stepDelta > 0 && debugStepQueries)
                {
                    Debug.Log($"[GetOverallSteps] Ignoring small change: {previousSteps} → {overallSteps} (delta: {stepDelta} < threshold: {stepChangeThreshold})");
                }

                SaveStepData();
            }).Execute();
        }
        else
        {
            // Different day from registration/logout
            if (daysSinceLastSave == 0)
            {
                overallSteps = stepData.numberOfSteps;
                SaveStepData();
            }
            else if (daysSinceLastSave == 1)
            {
                overallStepsBeforeToday = stepData.numberOfSteps;
                request.Since(DateTime.Today).OnQuerySuccess((stepCount) =>
                {

                    // 🔴 CRITICAL BUG CHECK: If device steps < baseline, baseline is invalid
                    if (stepData.baselineSteps > 0 && stepCount < stepData.baselineSteps)
                    {
                        Debug.LogError($"[GetOverallSteps] ⚠️ INVALID STATE: Device steps ({stepCount}) < Baseline ({stepData.baselineSteps})! " +
                            $"Resetting baseline to 0.");
                        stepData.baselineSteps = 0;
                        baselineEstablished = false;
                    }

                    int todaySteps;
                    int previousSteps = overallSteps;
                    if (stepData.baselineSteps > 0)
                    {
                        // Post-logout multi-day: Baseline only applies to the FIRST day
                        // After that, daily steps are raw device steps, overall accumulates

                        // Check if today is the same day as logout
                        DateTime logoutDate = DateTime.Parse(stepData.registrationTime).Date;
                        if (DateTime.Today == logoutDate)
                        {
                            // Still on logout day - apply baseline
                            todaySteps = Math.Max(0, stepCount - stepData.baselineSteps);
                            Debug.Log($"[GetOverallSteps] Multi-day on logout day: {stepCount} - {stepData.baselineSteps} = {todaySteps}");
                        }
                        else
                        {
                            // Different day from logout - no baseline for daily steps
                            todaySteps = stepCount;
                            Debug.Log($"[GetOverallSteps] Multi-day after logout day: {todaySteps} (no baseline)");
                        }
                    }
                    else
                    {
                        // Normal mode
                        int offset = PlayerPrefs.GetInt(OverallStepOffsetKey, 0);
                        todaySteps = Math.Max(0, stepCount - offset);
                        Debug.Log($"[GetOverallSteps] Multi-day normal: {stepCount} - {offset} = {todaySteps}");
                    }

                    // ALWAYS accumulate for overall steps
                    overallSteps = overallStepsBeforeToday + todaySteps;
                    Debug.Log($"[GetOverallSteps] Overall accumulated: {overallStepsBeforeToday} + {todaySteps} = {overallSteps}");

                    // Fire event only if steps changed by threshold (debounce jitter)
                    int stepDelta = Math.Abs(overallSteps - previousSteps);
                    if (stepDelta >= stepChangeThreshold)
                    {
                        int dailyOffset = PlayerPrefs.GetInt(DailyStepOffsetKey, 0);
                        int dailySteps = stepData.baselineSteps > 0 ? Math.Max(0, stepCount - stepData.baselineSteps) : Math.Max(0, stepCount - dailyOffset);
                        if (debugStepQueries) Debug.Log($"[GetOverallSteps] Step change: {previousSteps} → {overallSteps} (delta: {stepDelta})");
                        onStepsUpdated?.Invoke(overallSteps, dailySteps);
                    }
                    else if (stepDelta > 0 && debugStepQueries)
                    {
                        Debug.Log($"[GetOverallSteps] Ignoring small change: {previousSteps} → {overallSteps} (delta: {stepDelta} < threshold: {stepChangeThreshold})");
                    }

                    SaveStepData();
                }).Execute();
            }
            else if (daysSinceLastSave >= 2 && daysSinceLastSave <= 10)
            {
                overallStepsBeforeToday = stepData.numberOfSteps;

                // For multi-day gaps, always accumulate normally
                request.From(lastSaveTime).To(DateTime.Today).OnQuerySuccess((stepCount) =>
                {
                    overallStepsBeforeToday += stepCount;
                    request.Since(DateTime.Today).OnQuerySuccess((stepCountToday) =>
                    {
                        if (debugStepQueries) Debug.Log($"[StepQuery] multi-day range returned stepCount={stepCount} stepCountToday={stepCountToday}");
                        // No baseline after the first day
                        int todaySteps = stepCountToday;

                        if (stepData.baselineSteps == 0)
                        {
                            // Normal mode - apply offset
                            int offset = PlayerPrefs.GetInt(OverallStepOffsetKey, 0);
                            todaySteps = Math.Max(0, stepCountToday - offset);
                            overallSteps = Mathf.Max(0, overallStepsBeforeToday + todaySteps - offset);
                        }
                        else
                        {
                            // Post-logout mode - but no baseline after first day
                            overallSteps = overallStepsBeforeToday + todaySteps;
                        }

                        Debug.Log($"[GetOverallSteps] Long multi-day: {overallStepsBeforeToday} + {todaySteps} = {overallSteps}");
                        int previousSteps = overallSteps;

                        // Fire event only if steps changed by threshold (debounce jitter)
                        int stepDelta = Math.Abs(overallSteps - previousSteps);
                        if (stepDelta >= stepChangeThreshold)
                        {
                            int dailyOffset = PlayerPrefs.GetInt(DailyStepOffsetKey, 0);
                            int dailySteps = stepData.baselineSteps > 0 ? Math.Max(0, stepCount - stepData.baselineSteps) : Math.Max(0, stepCount - dailyOffset);
                            if (debugStepQueries) Debug.Log($"[GetOverallSteps] Step change: {previousSteps} → {overallSteps} (delta: {stepDelta})");
                            onStepsUpdated?.Invoke(overallSteps, dailySteps);
                        }
                        else if (stepDelta > 0 && debugStepQueries)
                        {
                            Debug.Log($"[GetOverallSteps] Ignoring small change: {previousSteps} → {overallSteps} (delta: {stepDelta} < threshold: {stepChangeThreshold})");
                        }

                        SaveStepData();
                    }).Execute();
                }).Execute();
            }
            else if (daysSinceLastSave >= 11)
            {
                overallStepsBeforeToday = stepData.numberOfSteps;

                // For very long gaps, always accumulate normally
                request.From(DateTime.Today.AddDays(-daysSinceLastSave)).To(DateTime.Today).OnQuerySuccess((stepCount) =>
                {
                    overallStepsBeforeToday = stepCount;
                    request.Since(DateTime.Today).OnQuerySuccess((stepCountToday) =>
                    {
                        if (debugStepQueries) Debug.Log($"[StepQuery] long-gap returned stepCount={stepCount} stepCountToday={stepCountToday}");
                        // No baseline after the first day
                        int todaySteps = stepCountToday;

                        if (stepData.baselineSteps == 0)
                        {
                            // Normal mode - apply offset
                            int offset = PlayerPrefs.GetInt(OverallStepOffsetKey, 0);
                            overallSteps = Mathf.Max(0, overallStepsBeforeToday + todaySteps - offset);
                        }
                        else
                        {
                            // Post-logout mode - but no baseline after first day
                            overallSteps = overallStepsBeforeToday + todaySteps;
                        }

                        Debug.Log($"[GetOverallSteps] Very long multi-day: {overallStepsBeforeToday} + {todaySteps} = {overallSteps}");

                        int previousSteps = overallSteps;
                        // Fire event only if steps changed by threshold (debounce jitter)
                        int stepDelta = Math.Abs(overallSteps - previousSteps);
                        if (stepDelta >= stepChangeThreshold)
                        {
                            int dailyOffset = PlayerPrefs.GetInt(DailyStepOffsetKey, 0);
                            int dailySteps = stepData.baselineSteps > 0 ? Math.Max(0, stepCount - stepData.baselineSteps) : Math.Max(0, stepCount - dailyOffset);
                            if (debugStepQueries) Debug.Log($"[GetOverallSteps] Step change: {previousSteps} → {overallSteps} (delta: {stepDelta})");
                            onStepsUpdated?.Invoke(overallSteps, dailySteps);
                        }
                        else if (stepDelta > 0 && debugStepQueries)
                        {
                            Debug.Log($"[GetOverallSteps] Ignoring small change: {previousSteps} → {overallSteps} (delta: {stepDelta} < threshold: {stepChangeThreshold})");
                        }
                        SaveStepData();
                    }).Execute();
                }).Execute();
            }
        }
    }

    public int GetDaysSinceRegistration()
    {
        if (string.IsNullOrEmpty(stepData.registrationTime))
        {
            Debug.LogError("Registration time is null or empty.");
            return 0;
        }
        return (DateTime.Today - DateTime.Parse(stepData.registrationTime).Date).Days;
    }

    public int GetDaysSinceLastSave()
    {
        if (string.IsNullOrEmpty(stepData.lastSaveTime))
        {
            Debug.LogError("Last save time is null or empty.");
            return 0;
        }
        return (DateTime.Today - DateTime.Parse(stepData.lastSaveTime).Date).Days;
    }

    public void SaveStepData()
    {
        stepData.lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd");
        stepData.numberOfSteps = overallSteps;
        stepData.overallSteps = overallSteps;

        // Calculate daily steps SEPARATELY from overall steps
        StepCounterRequest request = new StepCounterRequest();
        request.Since(DateTime.Today).OnQuerySuccess((todaySteps) =>
        {
            int calculatedDailySteps;

            // Daily steps logic
            if (stepData.baselineSteps > 0)
            {
                // Check if today is the logout day
                DateTime logoutDate = DateTime.Parse(stepData.registrationTime).Date;
                if (DateTime.Today == logoutDate)
                {
                    // On logout day: apply baseline to daily steps
                    calculatedDailySteps = Math.Max(0, todaySteps - stepData.baselineSteps);
                    Debug.Log($"[SAVE] Logout day - Daily with baseline: {todaySteps} - {stepData.baselineSteps} = {calculatedDailySteps}");
                }
                else
                {
                    // After logout day: daily steps are just today's raw steps
                    // For post-logout days, use raw device steps (no baseline). If you want daily
                    // to respect the same daily offset as normal mode, change below to subtract DailyStepOffsetKey.
                    calculatedDailySteps = todaySteps;
                    Debug.Log($"[SAVE] After logout day - Daily raw: {calculatedDailySteps}");
                }
            }
            else
            {
                // Normal mode - apply daily offset so daily shows the same base as overall
                int dailyOffset = PlayerPrefs.GetInt(DailyStepOffsetKey, 0);
                calculatedDailySteps = Math.Max(0, todaySteps - dailyOffset);
                Debug.Log($"[SAVE] Normal mode - Daily: {todaySteps} - offset {dailyOffset} = {calculatedDailySteps}");
            }

            stepData.dailySteps = calculatedDailySteps;

            // Safety clamp: daily should not exceed overall stored value
            if (stepData.dailySteps > stepData.overallSteps)
            {
                stepData.dailySteps = stepData.overallSteps;
            }

            // Save baseline to disk ONLY if we're on a post-logout day OR if no baseline exists
            // On logout day with baseline in memory, keep baseline at 0 in the file
            bool shouldSaveBaseline = stepData.baselineSteps > 0 && DateTime.Today != DateTime.Parse(stepData.registrationTime).Date;
            int baselineToSave = shouldSaveBaseline ? stepData.baselineSteps : 0;
            int originalBaseline = stepData.baselineSteps; // Preserve in-memory baseline

            stepData.baselineSteps = baselineToSave; // Temporarily set for save
            string stepDataJson = JsonUtility.ToJson(stepData);
            File.WriteAllText(stepDataJsonFilePath, stepDataJson);
            stepData.baselineSteps = originalBaseline; // Restore in-memory baseline

            Debug.Log($"[PERSISTENCE] Step data saved - Overall: {overallSteps}, Daily: {stepData.dailySteps}, " +
                $"OverallBeforeToday: {overallStepsBeforeToday}, Baseline (in-memory): {originalBaseline}, Baseline (saved to disk): {baselineToSave}");

            // NOTE: Do NOT fire onStepsUpdated here! SaveStepData is called from GetOverallSteps callbacks,
            // and GetOverallSteps already fires the event before SaveStepData is called.
            // Double-firing causes UI oscillation and race conditions.
        }).Execute();
    }

    // Awaitable version of SaveStepData to ensure the StepCounterRequest has completed
    public async Task SaveStepDataAsync()
    {
        stepData.lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd");
        stepData.numberOfSteps = overallSteps;
        stepData.overallSteps = overallSteps;

        var tcs = new TaskCompletionSource<bool>();

        StepCounterRequest request = new StepCounterRequest();
        request.Since(DateTime.Today).OnQuerySuccess((todaySteps) =>
        {
            int calculatedDailySteps;

            if (stepData.baselineSteps > 0)
            {
                DateTime logoutDate = DateTime.Parse(stepData.registrationTime).Date;
                if (DateTime.Today == logoutDate)
                {
                    calculatedDailySteps = Math.Max(0, todaySteps - stepData.baselineSteps);
                }
                else
                {
                    calculatedDailySteps = todaySteps;
                }
            }
            else
            {
                int dailyOffset = PlayerPrefs.GetInt(DailyStepOffsetKey, 0);
                calculatedDailySteps = Math.Max(0, todaySteps - dailyOffset);
            }

            stepData.dailySteps = calculatedDailySteps;

            if (stepData.dailySteps > stepData.overallSteps)
            {
                stepData.dailySteps = stepData.overallSteps;
            }

            // Save baseline to disk ONLY if we're on a post-logout day OR if no baseline exists
            // On logout day with baseline in memory, keep baseline at 0 in the file
            bool shouldSaveBaseline = stepData.baselineSteps > 0 && DateTime.Today != DateTime.Parse(stepData.registrationTime).Date;
            int baselineToSave = shouldSaveBaseline ? stepData.baselineSteps : 0;
            int originalBaseline = stepData.baselineSteps; // Preserve in-memory baseline

            stepData.baselineSteps = baselineToSave; // Temporarily set for save
            string stepDataJson = JsonUtility.ToJson(stepData);
            File.WriteAllText(stepDataJsonFilePath, stepDataJson);
            stepData.baselineSteps = originalBaseline; // Restore in-memory baseline

            Debug.Log($"[PERSISTENCE] Step data saved (async) - Overall: {overallSteps}, Daily: {stepData.dailySteps}, Baseline (in-memory): {originalBaseline}, Baseline (saved to disk): {baselineToSave}");

            // NOTE: Do NOT fire onStepsUpdated here! SaveStepDataAsync is called from GetOverallSteps callbacks,
            // and GetOverallSteps already fires the event before SaveStepDataAsync is called.
            // Double-firing causes UI oscillation and race conditions.

            tcs.SetResult(true);
        }).Execute();

        await tcs.Task;
    }

    public void InitializeStepData()
    {
        Debug.Log("[INIT DATA] ========== InitializeStepData() called (fresh startup) ==========");
        stepData = new StepData(); // Create fresh step data
        Debug.Log("[INIT DATA] Fresh StepData created - all values at 0");

        StepCounterRequest request = new StepCounterRequest();
        request.Since(DateTime.Today).OnQuerySuccess((stepCount) =>
        {
            overallSteps = stepCount;
            stepData.registrationTime = DateTime.Today.ToString("yyyy-MM-dd");
            stepData.lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd");
            stepData.numberOfSteps = overallSteps;
            Debug.Log($"[INIT DATA] Device query returned: {stepCount} steps - registrationTime set to today");
            SaveStepData();
        }).Execute();
    }
    public void InitializeStepDataAfterLogout()
    {
        stepData = new StepData();

        // Set EVERYTHING to 0 immediately for display
        overallSteps = 0;
        stepData.registrationTime = DateTime.Today.ToString("yyyy-MM-dd");
        stepData.lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd");
        stepData.numberOfSteps = 0;
        stepData.overallSteps = 0;
        stepData.dailySteps = 0;

        // FIRE EVENT IMMEDIATELY with BOTH as 0
        onStepsUpdated?.Invoke(0, 0);
        Debug.Log("[LOGOUT INIT] Fired immediate event - BOTH daily and overall set to 0");

        // Get current device steps as baseline ONLY for the logout day
        StepCounterRequest request = new StepCounterRequest();
        request.Since(DateTime.Today).OnQuerySuccess((currentDeviceSteps) =>
        {
            // Keep baseline in-memory ONLY. Do NOT persist baseline to disk while logged out,
            // otherwise the app may later restore previous counts unexpectedly.
            stepData.baselineSteps = currentDeviceSteps; // in-memory only
            baselineEstablished = true;

            Debug.Log($"[LOGOUT INIT] In-memory baseline established: {currentDeviceSteps} device steps for logout day only (not saved to disk)");

            // Clear the suppression flag now that baseline is set, so real-time updates can begin
            PlayerPrefs.DeleteKey("SuppressStepQuery");
            PlayerPrefs.Save();
            Debug.Log("[LOGOUT INIT] Cleared SuppressStepQuery flag - real-time updates enabled");

            // Start real-time step counting after baseline is set
            if (refreshStepsCoroutine == null)
            {
                refreshStepsCoroutine = StartCoroutine(RefreshStepsLoop());
                Debug.Log("[LOGOUT INIT] Started refresh coroutine for real-time counting");
            }
        }).Execute();
    }

    public void LoadStepData()
    {
        Debug.Log("[PERSISTENCE] ========== LoadStepData() called ==========");
        Debug.Log($"[PERSISTENCE] File exists: {File.Exists(stepDataJsonFilePath)}");

        // Check if this is a brand new player who has never signed in
        bool hasEverSignedIn = PlayerPrefs.GetInt("HasEverSignedIn", 0) == 1;
        Debug.Log($"[PERSISTENCE] HasEverSignedIn: {hasEverSignedIn}");

        // If logout suppression is set, or if services are initialized and the user is not signed in,
        // treat this as a logged-out state: show zeros and do NOT restore local/cloud data.
        if (PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 1)
        {
            Debug.Log("[PERSISTENCE] SuppressCloudRestore is set — initializing in-memory steps to 0 (logged out state)");
            stepData = new StepData();
            overallSteps = 0;
            stepData.dailySteps = 0;
            stepData.overallSteps = 0;
            overallStepsBeforeToday = 0;
            baselineEstablished = false;
            // Ensure coroutines don't start while suppressed
            if (refreshStepsCoroutine != null)
            {
                StopCoroutine(RefreshStepsLoop());
                refreshStepsCoroutine = null;
            }

            onStepsUpdated?.Invoke(0, 0);
            onLoaded?.Invoke();
            return;
        }

        // If this is a brand new user who has NEVER signed in, start fresh (don't restore local data)
        if (!hasEverSignedIn)
        {
            Debug.Log("[PERSISTENCE] Brand new user (never signed in) — starting fresh with device query");
            InitializeStepData();
            return;
        }

        // If services are initialized and user NOT signed in, we still allow local restore.
        // Only full reset happens when HasLoggedOut/SuppressCloudRestore are set above.
        // This keeps offline/local steps intact across app restarts while signed-out.
        if (File.Exists(stepDataJsonFilePath))
        {
            string stepDataJson = File.ReadAllText(stepDataJsonFilePath);
            stepData = JsonUtility.FromJson<StepData>(stepDataJson);
            Debug.Log($"[PERSISTENCE] Local step data file loaded - Full JSON: {stepDataJson}");

            if (stepData == null)
            {
                Debug.LogWarning("Loaded stepData was null - creating new StepData instance.");
                stepData = new StepData();
            }

            // ⚠️ CRITICAL SAFETY CHECK: Never restore local file data if user just logged out
            // After logout, ONLY cloud data should be restored (if user signs back in)
            bool userJustLoggedOut = PlayerPrefs.GetInt("HasLoggedOut", 0) == 1;
            if (userJustLoggedOut)
            {
                Debug.LogError("[PERSISTENCE] ⚠️ CRITICAL: User just logged out but local file still has data! Ignoring file data.");
                stepData = new StepData();
                overallSteps = 0;
                overallStepsBeforeToday = 0;
                baselineEstablished = false;
                onStepsUpdated?.Invoke(0, 0);
                onLoaded?.Invoke();
                return;
            }

            // Restore in-memory values from saved data so UI and consumers see the correct counts
            overallSteps = stepData.overallSteps != 0 ? stepData.overallSteps : stepData.numberOfSteps;
            overallStepsBeforeToday = stepData.numberOfSteps;
            baselineEstablished = (stepData.baselineSteps > 0);

            Debug.Log($"[PERSISTENCE] Loaded file contents: numberOfSteps={stepData.numberOfSteps}, overallSteps={stepData.overallSteps}, " +
                $"dailySteps={stepData.dailySteps}, baselineSteps={stepData.baselineSteps}, registrationTime={stepData.registrationTime}");
            Debug.Log($"[PERSISTENCE] Restored in-memory: overallSteps={overallSteps}, overallStepsBeforeToday={overallStepsBeforeToday}, baseline={stepData.baselineSteps}");

            // ⚠️ SAFETY CHECK: If baseline exists but registration time is old, baseline is stale
            if (stepData.baselineSteps > 0)
            {
                DateTime regTime = DateTime.Parse(stepData.registrationTime).Date;
                int daysSinceReg = (DateTime.Today - regTime).Days;
                if (daysSinceReg > 1)
                {
                    Debug.LogWarning($"[PERSISTENCE] ⚠️ WARNING: Baseline ({stepData.baselineSteps}) exists but registration was {daysSinceReg} days ago. Clearing baseline.");
                    stepData.baselineSteps = 0;
                    baselineEstablished = false;
                }
            }

            // Only fire events immediately if NOT waiting for cloud data
            // If cloud load is pending, it will fire events when it completes
            if (!waitingForCloudData)
            {
                Debug.Log("[PERSISTENCE] Firing events from local data (not waiting for cloud)");
                onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);
                onLoaded?.Invoke();
            }
            else
            {
                Debug.Log("[PERSISTENCE] Suppressing events - waiting for cloud data to load");
            }

            // Check if this data came from a cloud restore
            bool isCloudRestored = PlayerPrefs.GetInt("CloudRestored", 0) == 1;
            bool hasLoggedOut = PlayerPrefs.GetInt("HasLoggedOut", 0) == 1;
            Debug.Log($"[PERSISTENCE] Flags: CloudRestored={isCloudRestored}, HasLoggedOut={hasLoggedOut}");

            if (isCloudRestored && !hasLoggedOut)
            {
                Debug.Log("[PERSISTENCE] ⚠️ WARNING - Cloud data is marked as restored but player hasn't logged out. This might cause old data to load.");
            }

            // If cloud data was restored, skip the immediate recalculation to preserve cloud values
            // The cloud load already queried device and computed accurate values
            if (isCloudRestored)
            {
                Debug.Log("[PERSISTENCE] Cloud-restored data detected — skipping immediate GetOverallSteps() to preserve cloud values.");

                // Still start the refresh coroutine for future updates
                if (refreshStepsCoroutine == null && PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
                {
                    refreshStepsCoroutine = StartCoroutine(RefreshStepsLoop());
                }
            }
            else
            {
                // Regular local data - update immediately and start refresh loop
                if (PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
                {
                    Debug.Log("[StepCounter] Performing immediate GetOverallSteps() after local load to update UI quickly.");
                    GetOverallSteps();
                }
                else
                {
                    Debug.Log("[StepCounter] Suppressed immediate step query after load (SuppressStepQuery=1).");
                }

                // Ensure the refresh coroutine runs unless step queries are suppressed
                if (refreshStepsCoroutine == null)
                {
                    if (PlayerPrefs.GetInt("SuppressStepQuery", 0) == 0)
                    {
                        refreshStepsCoroutine = StartCoroutine(RefreshStepsLoop());
                    }
                    else
                    {
                        Debug.Log("[StepCounter] Not starting refresh coroutine because step queries are suppressed.");
                    }
                }
            }
        }
        else
        {
            Debug.Log("[PERSISTENCE] Step data file DOES NOT EXIST - creating fresh data");
            Debug.Log("[PERSISTENCE] Calling InitializeStepData() to create new step data");
            InitializeStepData();
        }
    }

    public async Task SaveStepDataToCloud()
    {
        await CloudSaver.SaveDataToCloud("stepData", stepData);
    }

    public async Task LoadStepDataFromCloud()
    {
        Debug.Log("[CLOUD LOAD DEBUG] ========== LoadStepDataFromCloud() called ==========");
        Debug.Log($"[CLOUD LOAD DEBUG] SuppressCloudRestore: {PlayerPrefs.GetInt("SuppressCloudRestore", 0)}");
        Debug.Log($"[CLOUD LOAD DEBUG] HasLoggedOut: {PlayerPrefs.GetInt("HasLoggedOut", 0)}");

        // Respect an explicit suppression set by logout flow to avoid restoring previous counts
        if (PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 1)
        {
            // If the user is signed in, treat this as an intentional request to restore and clear suppression
            if (Unity.Services.Core.UnityServices.State == Unity.Services.Core.ServicesInitializationState.Initialized &&
                Unity.Services.Authentication.AuthenticationService.Instance != null &&
                Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("[CLOUD LOAD DEBUG] Suppression present but user is signed in — clearing suppression and proceeding with cloud load.");
                PlayerPrefs.DeleteKey("SuppressCloudRestore");
                PlayerPrefs.Save();
            }
            else
            {
                Debug.LogWarning("[CLOUD LOAD DEBUG] Skipped loading from cloud — cloud restore suppressed after logout.");
                return;
            }
        }

        try
        {
            string stepDataJson = await CloudSaver.LoadDataFromCloud("stepData");
            Debug.Log($"[CLOUD LOAD DEBUG] ========== Cloud data retrieved ==========");
            Debug.Log($"[CLOUD LOAD DEBUG] Raw cloud JSON received: {stepDataJson}");

            stepData = JsonUtility.FromJson<StepData>(stepDataJson);

            Debug.Log($"[CLOUD LOAD DEBUG] Parsed cloud data:" +
                $"\n  numberOfSteps: {stepData.numberOfSteps}" +
                $"\n  overallSteps: {stepData.overallSteps}" +
                $"\n  dailySteps: {stepData.dailySteps}" +
                $"\n  baselineSteps: {stepData.baselineSteps}" +
                $"\n  lastSaveTime: {stepData.lastSaveTime}" +
                $"\n  registrationTime: {stepData.registrationTime}");

            // WARNING: Check if cloud has old player data
            if ((stepData.overallSteps > 0 || stepData.numberOfSteps > 0) && PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
            {
                Debug.LogError($"[CLOUD LOAD DEBUG] ⚠️ CRITICAL: Cloud contains OLD PLAYER data (overall: {stepData.overallSteps}, numberOfSteps: {stepData.numberOfSteps}) " +
                    $"but HasLoggedOut flag is set! This is OLD PLAYER DATA being loaded for NEW PLAYER!");
            }

            // CRITICAL: Need to accumulate days between cloud save and today
            DateTime cloudLastSave = DateTime.Parse(stepData.lastSaveTime).Date;
            int daysSinceCloudSave = (DateTime.Today - cloudLastSave).Days;

            Debug.Log($"[CLOUD LOAD DEBUG] Cloud last saved on {cloudLastSave:yyyy-MM-dd}, today is {DateTime.Today:yyyy-MM-dd}, daysSince={daysSinceCloudSave}");

            // Start with cloud base value
            int cloudBaseOverall = (stepData.overallSteps != 0) ? stepData.overallSteps : stepData.numberOfSteps;

            if (daysSinceCloudSave == 0)
            {
                // Same day as cloud save - just use cloud value and add today's new steps
                StepCounterRequest request = new StepCounterRequest();
                request.Since(DateTime.Today).OnQuerySuccess((stepCountToday) =>
                {
                    Debug.Log($"[CLOUD LOAD DEBUG] Same-day load: stepCountToday={stepCountToday}");

                    overallSteps = cloudBaseOverall;
                    overallStepsBeforeToday = cloudBaseOverall;

                    // Compute daily
                    int dailyOffset = PlayerPrefs.GetInt(DailyStepOffsetKey, 0);
                    stepData.dailySteps = Math.Max(0, stepCountToday - dailyOffset);

                    Debug.Log($"[CLOUD LOAD DEBUG] Same-day: overall={overallSteps}, daily={stepData.dailySteps}");

                    stepData.lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd");
                    stepData.numberOfSteps = overallSteps;
                    stepData.overallSteps = overallSteps;

                    string processedJson = JsonUtility.ToJson(stepData);
                    File.WriteAllText(stepDataJsonFilePath, processedJson);

                    Debug.Log($"[CLOUD LOAD DEBUG] Saved to local: {processedJson}");
                    Debug.Log($"[CLOUD LOAD DEBUG] Firing onStepsUpdated(overall={overallSteps}, daily={stepData.dailySteps})");

                    onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);

                    PlayerPrefs.SetInt("CloudRestored", 1);
                    PlayerPrefs.DeleteKey("SuppressStepQuery");
                    PlayerPrefs.DeleteKey("SuppressCloudRestore");
                    PlayerPrefs.Save();

                    cloudLoaded = true;
                    waitingForCloudData = false;  // Cloud data received, clear waiting flag
                    onLoaded?.Invoke();
                }).Execute();
            }
            else if (daysSinceCloudSave == 1)
            {
                // 1 day since cloud save - accumulate yesterday + today
                StepCounterRequest request = new StepCounterRequest();
                request.Since(DateTime.Today).OnQuerySuccess((stepCountToday) =>
                {
                    Debug.Log($"[CLOUD LOAD DEBUG] 1-day gap: stepCountToday={stepCountToday}");

                    overallSteps = cloudBaseOverall + stepCountToday;
                    overallStepsBeforeToday = cloudBaseOverall;

                    int dailyOffset = PlayerPrefs.GetInt(DailyStepOffsetKey, 0);
                    stepData.dailySteps = Math.Max(0, stepCountToday - dailyOffset);

                    Debug.Log($"[CLOUD LOAD DEBUG] 1-day gap: cloudBase={cloudBaseOverall}, today={stepCountToday}, overall={overallSteps}, daily={stepData.dailySteps}");

                    stepData.lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd");
                    stepData.numberOfSteps = overallSteps;
                    stepData.overallSteps = overallSteps;

                    string processedJson = JsonUtility.ToJson(stepData);
                    File.WriteAllText(stepDataJsonFilePath, processedJson);

                    Debug.Log($"[CLOUD LOAD DEBUG] Saved to local: {processedJson}");
                    Debug.Log($"[CLOUD LOAD DEBUG] Firing onStepsUpdated(overall={overallSteps}, daily={stepData.dailySteps})");

                    onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);

                    PlayerPrefs.SetInt("CloudRestored", 1);
                    PlayerPrefs.DeleteKey("SuppressStepQuery");
                    PlayerPrefs.DeleteKey("SuppressCloudRestore");
                    PlayerPrefs.Save();

                    cloudLoaded = true;
                    waitingForCloudData = false;  // Cloud data received, clear waiting flag
                    onLoaded?.Invoke();
                }).Execute();
            }
            else if (daysSinceCloudSave >= 2)
            {
                // Multiple days gap - query range then add today
                StepCounterRequest request = new StepCounterRequest();
                request.From(cloudLastSave).To(DateTime.Today).OnQuerySuccess((stepCountRange) =>
                {
                    int accumulatedSteps = cloudBaseOverall + stepCountRange;

                    StepCounterRequest requestToday = new StepCounterRequest();
                    requestToday.Since(DateTime.Today).OnQuerySuccess((stepCountToday) =>
                    {
                        Debug.Log($"[CLOUD LOAD DEBUG] Multi-day gap ({daysSinceCloudSave} days): rangeSteps={stepCountRange}, todaySteps={stepCountToday}");

                        overallSteps = accumulatedSteps;
                        overallStepsBeforeToday = cloudBaseOverall + stepCountRange;

                        int dailyOffset = PlayerPrefs.GetInt(DailyStepOffsetKey, 0);
                        stepData.dailySteps = Math.Max(0, stepCountToday - dailyOffset);

                        Debug.Log($"[CLOUD LOAD DEBUG] Multi-day: cloudBase={cloudBaseOverall}, range={stepCountRange}, overall={overallSteps}, daily={stepData.dailySteps}");

                        stepData.lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd");
                        stepData.numberOfSteps = overallSteps;
                        stepData.overallSteps = overallSteps;

                        string processedJson = JsonUtility.ToJson(stepData);
                        File.WriteAllText(stepDataJsonFilePath, processedJson);

                        Debug.Log($"[CLOUD LOAD DEBUG] Saved to local: {processedJson}");
                        Debug.Log($"[CLOUD LOAD DEBUG] Firing onStepsUpdated(overall={overallSteps}, daily={stepData.dailySteps})");

                        onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);

                        PlayerPrefs.SetInt("CloudRestored", 1);
                        PlayerPrefs.DeleteKey("SuppressStepQuery");
                        PlayerPrefs.DeleteKey("SuppressCloudRestore");
                        PlayerPrefs.Save();

                        cloudLoaded = true;
                        waitingForCloudData = false;  // Cloud data received, clear waiting flag
                        onLoaded?.Invoke();
                    }).Execute();
                }).Execute();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load step data from cloud: {e.Message}");
            waitingForCloudData = false;  // Cloud load failed, clear waiting flag
            
            // If we were waiting for cloud but it failed, fire events from local data now
            if (stepData != null && overallSteps >= 0)
            {
                Debug.Log("[CLOUD LOAD] Cloud failed - firing events from local data as fallback");
                onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);
                onLoaded?.Invoke();
            }
            else
            {
                // Fall back to local data load
                LoadStepData();
            }
        }
    }

    // UI / button-friendly method to trigger a cloud load on demand
    public async void LoadFromCloudButton()
    {
        Debug.Log("[CloudSync] Manual cloud load requested via UI button.");

        if (PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 1)
        {
            Debug.LogWarning("[CloudSync] Cloud restore is suppressed (user logged out). Clear suppression by signing in to load cloud data.");
            return;
        }

        // Ensure Unity Services are initialized before attempting cloud operations
        if (Unity.Services.Core.UnityServices.State != Unity.Services.Core.ServicesInitializationState.Initialized)
        {
            try
            {
                Debug.Log("[CloudSync] Initializing Unity Services before cloud load...");
                await Unity.Services.Core.UnityServices.InitializeAsync();
            }
            catch (Exception e)
            {
                Debug.LogError($"[CloudSync] Failed to initialize Unity Services: {e.Message}");
                return;
            }
        }

        if (Unity.Services.Authentication.AuthenticationService.Instance == null || !Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
        {
            Debug.LogWarning("[CloudSync] Cannot load cloud data: user is not signed in.");
            return;
        }

        try
        {
            await LoadStepDataFromCloud();
        }
        catch (Exception e)
        {
            Debug.LogError($"[CloudSync] Manual cloud load failed: {e.Message}");
        }
    }
    private IEnumerator RefreshStepsLoop()
    {
        while (true)
        {
            GetOverallSteps();
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, refreshInterval));
        }
    }

    // Runtime method to adjust refresh interval (useful for testing responsiveness)
    public void SetRefreshInterval(float seconds)
    {
        refreshInterval = Mathf.Max(0.1f, seconds);
        if (refreshStepsCoroutine != null)
        {
            StopCoroutine(refreshStepsCoroutine);
            refreshStepsCoroutine = StartCoroutine(RefreshStepsLoop());
        }
    }
    async void OnApplicationQuit()
    {
        // Ensure local save finishes (including today's device query) before pushing to cloud
        await SaveStepDataAsync();
        await SaveStepDataToCloud();
    }

    async void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
        {
            await SaveStepDataAsync();
        }
    }

    public void ResetStepDataCompletely()
    {
        stepData = new StepData();
        stepData.baselineSteps = 0;
        overallSteps = 0;
        overallStepsBeforeToday = 0;
        cloudLoaded = false;

        if (refreshStepsCoroutine != null)
        {
            StopCoroutine(refreshStepsCoroutine);
            refreshStepsCoroutine = null;
        }

        if (File.Exists(stepDataJsonFilePath))
            File.Delete(stepDataJsonFilePath);

        PlayerPrefs.DeleteKey(OverallStepOffsetKey);
        PlayerPrefs.DeleteKey(DailyStepOffsetKey);
        PlayerPrefs.DeleteKey("SuppressStepQuery");
        PlayerPrefs.Save();

        onStepsUpdated?.Invoke(0, 0);
    }

    private bool IsServiceSignedIn()
    {
        return Unity.Services.Core.UnityServices.State == Unity.Services.Core.ServicesInitializationState.Initialized &&
               Unity.Services.Authentication.AuthenticationService.Instance != null &&
               Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn;
    }

}