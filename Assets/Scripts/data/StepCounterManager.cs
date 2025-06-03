using System;
using UnityEngine;
using System.IO;
using Repforge.StepCounterPro;
using TMPro;


public class StepCounterManager : MonoBehaviour
{
    [Header("Step Data")]
    public StepData stepData;

    [Header("UI for Debug (Optional)")]
    public TMP_Text dailyStepsText;
    public TMP_Text overallStepsText;

    private string stepDataJsonFilePath;
    private int overallSteps;
    private int dailySteps;

    void Awake()
    {
        stepDataJsonFilePath = Application.persistentDataPath + "/stepData.json";
        LoadStepData();
        UpdateSteps();
    }

    void Update()
    {
        UpdateSteps();
        DisplayDebugSteps();
    }

    // Function to get and update overall steps and daily steps
    private void UpdateSteps()
    {
        StepCounterRequest request = new StepCounterRequest();
        DateTime lastSaveTime = DateTime.Parse(stepData.lastSaveTime).Date;

        if (GetDaysSinceLastSave() > 0)
        {
            // New day: Reset daily steps, calculate overall steps
            request.Since(DateTime.Today).OnQuerySuccess((stepCount) =>
            {
                dailySteps = stepCount;
                overallSteps = stepData.numberOfSteps + stepCount;
            }).Execute();
        }
        else
        {
            // Same day: Continue counting steps
            request.Since(lastSaveTime).OnQuerySuccess((stepCount) =>
            {
                dailySteps = stepCount;
                overallSteps = stepData.numberOfSteps;
            }).Execute();
        }
        SaveStepData();
    }

    // Returns daily steps
    public int GetDailySteps()
    {
        return dailySteps;
    }

    // Returns overall steps
    public int GetOverallSteps()
    {
        return overallSteps;
    }

    private void DisplayDebugSteps()
    {
        if (dailyStepsText != null)
            dailyStepsText.text = "Daily Steps: " + dailySteps;

        if (overallStepsText != null)
            overallStepsText.text = "Overall Steps: " + overallSteps;
    }

    private int GetDaysSinceLastSave()
    {
        DateTime today = DateTime.Today;
        DateTime lastSaveTime = DateTime.Parse(stepData.lastSaveTime).Date;
        return (today - lastSaveTime).Days;
    }

    private void SaveStepData()
    {
        stepData.lastSaveTime = DateTime.Today.ToString();
        stepData.numberOfSteps = overallSteps;
        string stepDataJson = JsonUtility.ToJson(stepData);
        File.WriteAllText(stepDataJsonFilePath, stepDataJson);
    }

    private void LoadStepData()
    {
        if (File.Exists(stepDataJsonFilePath))
        {
            string stepDataJson = File.ReadAllText(stepDataJsonFilePath);
            stepData = JsonUtility.FromJson<StepData>(stepDataJson);
        }
        else
        {
            InitializeStepData();
        }
    }

    private void InitializeStepData()
    {
        StepCounterRequest request = new StepCounterRequest();
        request.Since(DateTime.Today).OnQuerySuccess((stepCount) =>
        {
            dailySteps = stepCount;
            overallSteps = stepCount;
            stepData = new StepData
            {
                registrationTime = DateTime.Today.ToString(),
                lastSaveTime = DateTime.Today.ToString(),
                numberOfSteps = stepCount
            };
        }).Execute();
        SaveStepData();
    }
}
