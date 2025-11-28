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
    private const string OverallStepOffsetKey = "OverallStepOffset";
    private const string DailyStepOffsetKey = "DailyStepOffset";
    public static event Action<int, int> onStepsUpdated; // overall, daily
    private bool baselineEstablished = false;

    // //for debug purposes
    // public TMP_Text overallStepsText;
    // public TMP_Text overallStepsBeforeTodayText;

    private static OverallStepCounter instance;
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
            Debug.Log("Fresh logout detected in OverallStepCounter. Resetting step data.");

            ResetStepDataCompletely(); //reset
            PlayerPrefs.DeleteKey("HasLoggedOut"); // Clear the flag

            //InitializeStepData(); // Start fresh
            // Set flag to suppress step query and show 0
            PlayerPrefs.SetInt("SuppressStepQuery", 1);
            PlayerPrefs.Save();

            InitializeStepDataAfterLogout(); // Use special initialization
            return;
        }


        LoadStepData();
    }
    void Start()
    {
        if (!cloudLoaded)
        {
            GetOverallSteps(); // Delay actual step count until after potential cloud load
        }

        if (refreshStepsCoroutine == null)
        {
            refreshStepsCoroutine = StartCoroutine(RefreshStepsLoop());
        }
    }

    // void Update(){
    //     GetOverallSteps();
    //     // overallStepsText.text = "Overall steps: " +  overallSteps;
    //     // overallStepsBeforeTodayText.text = "Overall steps before today: " + overallStepsBeforeToday;
    // }

    public void GetOverallSteps()
    {
        Debug.Log("[StepCounter] Running GetOverallSteps...");
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
                Debug.Log($"[GetOverallSteps] Device steps: {stepCount}, Baseline: {stepData.baselineSteps}");

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
                // Fire event immediately if steps changed
                if (overallSteps != previousSteps)
                {
                    int dailySteps = stepData.baselineSteps > 0 ? Math.Max(0, stepCount - stepData.baselineSteps) : stepCount;
                    onStepsUpdated?.Invoke(overallSteps, dailySteps);
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
                    Debug.Log($"[GetOverallSteps] Multi-day: Device steps: {stepCount}, Baseline: {stepData.baselineSteps}");

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

                    // Fire event immediately if steps changed
                    if (overallSteps != previousSteps)
                    {
                        int dailySteps = stepData.baselineSteps > 0 ? Math.Max(0, stepCount - stepData.baselineSteps) : stepCount;
                        onStepsUpdated?.Invoke(overallSteps, dailySteps);
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

                        // Fire event immediately if steps changed
                        if (overallSteps != previousSteps)
                        {
                            int dailySteps = stepData.baselineSteps > 0 ? Math.Max(0, stepCount - stepData.baselineSteps) : stepCount;
                            onStepsUpdated?.Invoke(overallSteps, dailySteps);
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
                        // Fire event immediately if steps changed
                        if (overallSteps != previousSteps)
                        {
                            int dailySteps = stepData.baselineSteps > 0 ? Math.Max(0, stepCount - stepData.baselineSteps) : stepCount;
                            onStepsUpdated?.Invoke(overallSteps, dailySteps);
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
                    calculatedDailySteps = todaySteps;
                    Debug.Log($"[SAVE] After logout day - Daily raw: {calculatedDailySteps}");
                }
            }
            else
            {
                // Normal mode - daily steps are just today's steps
                calculatedDailySteps = todaySteps;
                Debug.Log($"[SAVE] Normal mode - Daily: {calculatedDailySteps}");
            }

            stepData.dailySteps = calculatedDailySteps;

            string stepDataJson = JsonUtility.ToJson(stepData);
            File.WriteAllText(stepDataJsonFilePath, stepDataJson);

            Debug.Log($"[PERSISTENCE] Step data saved - Overall: {overallSteps}, Daily: {stepData.dailySteps}, Baseline: {stepData.baselineSteps}");

            // Fire event with potentially DIFFERENT values
            onStepsUpdated?.Invoke(overallSteps, stepData.dailySteps);
        }).Execute();
    }

    public void InitializeStepData()
    {
        stepData = new StepData(); // Create fresh step data
        StepCounterRequest request = new StepCounterRequest();
        request.Since(DateTime.Today).OnQuerySuccess((stepCount) =>
        {
            overallSteps = stepCount;
            stepData.registrationTime = DateTime.Today.ToString("yyyy-MM-dd");
            stepData.lastSaveTime = DateTime.Today.ToString("yyyy-MM-dd");
            stepData.numberOfSteps = overallSteps;
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
            stepData.baselineSteps = currentDeviceSteps; // Store as baseline for first day only
            baselineEstablished = true;

            string stepDataJson = JsonUtility.ToJson(stepData);
            File.WriteAllText(stepDataJsonFilePath, stepDataJson);

            Debug.Log($"[LOGOUT INIT] Baseline established: {currentDeviceSteps} device steps for logout day only");
            Debug.Log($"[LOGOUT INIT] Saved stepData with baseline: {stepDataJson}");
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
        if (File.Exists(stepDataJsonFilePath))
        {
            string stepDataJson = File.ReadAllText(stepDataJsonFilePath);
            stepData = JsonUtility.FromJson<StepData>(stepDataJson);
            Debug.Log("Local step data loaded.");
        }
        else
        {
            InitializeStepData();
        }
    }

    public async Task SaveStepDataToCloud()
    {
        await CloudSaver.SaveDataToCloud("stepData", stepData);
    }

    public async Task LoadStepDataFromCloud()
    {
        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
        {
            Debug.LogWarning("[CloudSync] Skipped loading from cloud — user has logged out.");
            return;
        }

        try
        {
            string stepDataJson = await CloudSaver.LoadDataFromCloud("stepData");
            stepData = JsonUtility.FromJson<StepData>(stepDataJson);

            Debug.Log($"Cloud step data loaded. Steps: {stepData.numberOfSteps}, Last Save: {stepData.lastSaveTime}, Reg Time: {stepData.registrationTime}");

            // CRITICAL: Calculate and save the final processed steps, not raw cloud data
            StepCounterRequest request = new StepCounterRequest();
            request.Since(DateTime.Today).OnQuerySuccess((stepCountToday) =>
            {
                // Calculate overall steps with today's steps
                int rawOverallSteps = stepData.numberOfSteps + stepCountToday;

                // Apply offset to get the displayed value
                int offset = PlayerPrefs.GetInt(OverallStepOffsetKey, 0);
                overallSteps = Mathf.Max(0, rawOverallSteps - offset);

                overallStepsBeforeToday = stepData.numberOfSteps;

                // Update stepData with the PROCESSED values for persistence
                stepData.numberOfSteps = overallSteps; // Save processed value, not raw
                stepData.overallSteps = overallSteps;  // If this field exists
                stepData.dailySteps = stepCountToday;   // Save today's steps

                // Save the processed data to local file
                SaveStepData();

                Debug.Log($"[CLOUD PERSISTENCE] Saved processed data - Overall: {overallSteps}, Daily: {stepCountToday}, Offset: {offset}");

                GetOverallSteps();    // Restart counting first
                cloudLoaded = true;   // Set loaded flag AFTER restarting
                onLoaded?.Invoke();   // Notify the rest of the app
            }).Execute();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load step data from cloud: {e.Message}");
            // Fall back to local data
            LoadStepData();
        }
    }
    private IEnumerator RefreshStepsLoop()
    {
        while (true)
        {
            GetOverallSteps();
            yield return new WaitForSeconds(5f); // refresh every 5 seconds
        }
    }
    async void OnApplicationQuit()
    {
        SaveStepData();
        await SaveStepDataToCloud();
    }

    void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
        {
            SaveStepData();
        }
    }

    public void ResetStepDataCompletely()
    {
        Debug.Log("[RESET] Complete reset - clearing ALL step data");

        // Reset all in-memory data
        stepData = new StepData();
        stepData.baselineSteps = 0; // Clear baseline
        overallSteps = 0;
        overallStepsBeforeToday = 0;
        cloudLoaded = false;

        // Stop any running coroutines
        if (refreshStepsCoroutine != null)
        {
            StopCoroutine(refreshStepsCoroutine);
            refreshStepsCoroutine = null;
        }

        // Delete the file completely
        if (File.Exists(stepDataJsonFilePath))
        {
            File.Delete(stepDataJsonFilePath);
            Debug.Log("Step data file deleted completely.");
        }

        // Clear ALL PlayerPrefs related to steps
        PlayerPrefs.DeleteKey(OverallStepOffsetKey);
        PlayerPrefs.DeleteKey(DailyStepOffsetKey);
        PlayerPrefs.DeleteKey("SuppressStepQuery");
        PlayerPrefs.Save();

        // FIRE EVENT with BOTH as 0
        onStepsUpdated?.Invoke(0, 0);
        Debug.Log("[RESET] Fired event - BOTH daily and overall set to 0");

        Debug.Log("OverallStepCounter data completely reset to 0.");
    }

}