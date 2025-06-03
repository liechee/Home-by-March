using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;

public class RewardChecker : MonoBehaviour
{
    public Button unlockButton;
    public TMP_Text buttonText;
    public TMP_Text progressText; // New text field to show step progress
    public int requiredSteps;

    private int overallSteps;
    private string stepJsonFilePath;
    private string stepCountData;

    void Awake()
    {
        stepJsonFilePath = Application.persistentDataPath + "/stepData.json";

        // Check if the step data file exists before trying to read it
        if (File.Exists(stepJsonFilePath))
        {
            stepCountData = File.ReadAllText(stepJsonFilePath);
            StepData data = JsonUtility.FromJson<StepData>(stepCountData);
            overallSteps = data.overallSteps;
        }
        else
        {
            Debug.LogWarning("Step data file not found. Creating a new file with default data.");
            File.WriteAllText(stepJsonFilePath, JsonUtility.ToJson(new StepData()));
            overallSteps = 0;
        }

        UpdateButtonState();
    }

    void Update()
    {
        // Continuously update the overallSteps and button state
        UpdateStepData();
        UpdateButtonState();
    }
    private void UpdateStepData()
    {
        // Re-read the step data file to get the latest steps
        if (File.Exists(stepJsonFilePath))
        {
            stepCountData = File.ReadAllText(stepJsonFilePath);
            StepData data = JsonUtility.FromJson<StepData>(stepCountData);
            overallSteps = data.overallSteps; // Update the overallSteps dynamically
        }
        else
        {
            Debug.LogWarning("Step data file not found during update.");
        }
    }

    private void UpdateButtonState()
    {
        string colorTagOpen = "<color=#FFEE00>"; // red
        string colorTagClose = "</color>";
        if (overallSteps >= requiredSteps)
        {
            // Make the button interactable if the steps are sufficient
            unlockButton.interactable = true;

        }
        else
        {
            // Keep the button non-interactable if the steps are insufficient
            unlockButton.interactable = false;
            // buttonText.text = $"Need {requiredSteps - overallSteps} more steps";
        }

        // Update the progress text to show overall steps and required steps
        progressText.text = $"{colorTagOpen}{overallSteps}{colorTagClose}/{requiredSteps}";
    }

    public void OnUnlockButtonClicked()
    {
        if (unlockButton.interactable)
        {
            Debug.Log("Unlock button clicked!");
            // Add your unlock logic here
        }
    }


}
