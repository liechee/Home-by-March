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
    private bool cloudLoaded = false;
    public static event Action onLoaded;
    private Coroutine refreshStepsCoroutine;
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
            //ResetStepDataCompletely(); // Use a more thorough reset
            ResetStepDataCompletely(); // Use a more thorough reset
            // return; // Skip further initialization
            PlayerPrefs.DeleteKey("HasLoggedOut"); // Clear the flag
            InitializeStepData(); // Start fresh
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

        // if (refreshStepsCoroutine == null)
        // {
        //     refreshStepsCoroutine = StartCoroutine(RefreshStepsLoop());
        // }
    }

    // void Update(){
    //     GetOverallSteps();
    //     // overallStepsText.text = "Overall steps: " +  overallSteps;
    //     // overallStepsBeforeTodayText.text = "Overall steps before today: " + overallStepsBeforeToday;
    // }

    public void GetOverallSteps()
    {
        Debug.Log("[StepCounter] Running GetOverallSteps...");

        if (string.IsNullOrEmpty(stepData.registrationTime) || string.IsNullOrEmpty(stepData.lastSaveTime))
            return;

        StepCounterRequest request = new StepCounterRequest();

        DateTime registrationTime = DateTime.Parse(stepData.registrationTime).Date;
        DateTime lastSaveTime = DateTime.Parse(stepData.lastSaveTime).Date;

        int daysSinceLastSave = GetDaysSinceLastSave();

        if (registrationTime == lastSaveTime)
        {
            request.Since(DateTime.Today).OnQuerySuccess((stepCount) =>
            {
                overallSteps = stepCount;
                SaveStepData();
            }).Execute();
        }
        else
        {
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
                    overallSteps = overallStepsBeforeToday + stepCount;
                    SaveStepData();
                }).Execute();
            }
            else if (daysSinceLastSave >= 2 && daysSinceLastSave <= 10)
            {
                overallStepsBeforeToday = stepData.numberOfSteps;
                request.From(lastSaveTime).To(DateTime.Today).OnQuerySuccess((stepCount) =>
                {
                    overallStepsBeforeToday += stepCount;
                    request.Since(DateTime.Today).OnQuerySuccess((stepCountToday) =>
                    {
                        overallSteps = overallStepsBeforeToday + stepCountToday;
                        SaveStepData();
                    }).Execute();
                }).Execute();
            }
            else if (daysSinceLastSave >= 11)
            {
                overallStepsBeforeToday = stepData.numberOfSteps;
                request.From(DateTime.Today.AddDays(-daysSinceLastSave)).To(DateTime.Today).OnQuerySuccess((stepCount) =>
                {
                    overallStepsBeforeToday = stepCount;
                    request.Since(DateTime.Today).OnQuerySuccess((stepCountToday) =>
                    {
                        overallSteps = overallStepsBeforeToday + stepCountToday;
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
        string stepDataJson = JsonUtility.ToJson(stepData);
        File.WriteAllText(stepDataJsonFilePath, stepDataJson);
        Debug.Log("Step data saved to: " + stepDataJsonFilePath);
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
        string stepDataJson = await CloudSaver.LoadDataFromCloud("stepData");
        stepData = JsonUtility.FromJson<StepData>(stepDataJson);

        Debug.Log($"Cloud step data loaded. Steps: {stepData.numberOfSteps}, Last Save: {stepData.lastSaveTime}, Reg Time: {stepData.registrationTime}");

        //SaveStepData(); // Save to local

        StepCounterRequest request = new StepCounterRequest();
        request.Since(DateTime.Today).OnQuerySuccess((stepCountToday) =>
        {
            overallSteps = stepData.numberOfSteps + stepCountToday;
            overallStepsBeforeToday = stepData.numberOfSteps; // Store past steps
            //SaveStepData();

            // cloudLoaded = true;   // Now cloud data is active
            // GetOverallSteps();    // Run steps again using this cloud base
            // onLoaded?.Invoke();
            GetOverallSteps();    // Restart counting first
            cloudLoaded = true;   // Set loaded flag AFTER restarting
            // if (refreshStepsCoroutine == null)
            // {
            //     refreshStepsCoroutine = StartCoroutine(RefreshStepsLoop());
            // }
            onLoaded?.Invoke();   // Notify the rest of the app
        }).Execute();
    }
    // private IEnumerator RefreshStepsLoop()
    // {
    //     while (true)
    //     {
    //         GetOverallSteps();
    //         yield return new WaitForSeconds(10f); // refresh every 10 seconds
    //     }
    // }
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
        // Reset all in-memory data
        stepData = new StepData();
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

        // Initialize fresh data
        InitializeStepData();

        Debug.Log("OverallStepCounter data completely reset.");
    }

}