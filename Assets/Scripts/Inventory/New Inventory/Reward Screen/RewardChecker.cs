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
    public TMP_Text progressText;
    public int requiredSteps;

    private int overallSteps;
    private OverallStepCounter stepCounter;

    void Awake()
    {
        stepCounter = FindObjectOfType<OverallStepCounter>();
        
        if (stepCounter != null)
        {
            // Subscribe to step update events
            OverallStepCounter.onStepsUpdated += OnStepsUpdated;
            OverallStepCounter.onLoaded += OnStepDataLoaded;

            // Get initial value (may be 0 until OverallStepCounter finishes loading)
            overallSteps = stepCounter.overallSteps;

            Debug.Log($"RewardChecker: Initialized with {overallSteps} steps from OverallStepCounter, subscribed to events");
        }
        else
        {
            Debug.LogWarning("OverallStepCounter not found! Defaulting to 0 steps.");
            overallSteps = 0;
        }

        UpdateButtonState();
    }

    void OnDestroy()
    {
        // IMPORTANT: Unsubscribe from events to prevent memory leaks
        if (stepCounter != null)
        {
            OverallStepCounter.onStepsUpdated -= OnStepsUpdated;
            OverallStepCounter.onLoaded -= OnStepDataLoaded;
        }
    }

    // Event handler - called when OverallStepCounter finishes calculating steps
    void OnStepsUpdated(int newOverallSteps, int newDailySteps)
    {
        overallSteps = newOverallSteps;
        UpdateButtonState();
        Debug.Log($"RewardChecker: Steps updated via event to {overallSteps}");
    }

    void OnStepDataLoaded()
    {
        // Ensure we read the processed values after cloud/local load
        if (stepCounter != null)
        {
            overallSteps = stepCounter.overallSteps;
            UpdateButtonState();
            Debug.Log($"RewardChecker: Step data loaded - overallSteps set to {overallSteps}");
        }
    }

    private void UpdateButtonState()
    {
        string colorTagOpen = "<color=#FFEE00>";
        string colorTagClose = "</color>";
        
        if (overallSteps >= requiredSteps)
        {
            unlockButton.interactable = true;
        }
        else
        {
            unlockButton.interactable = false;
        }

        progressText.text = $"{colorTagOpen}{overallSteps}{colorTagClose}/{requiredSteps}";
    }

    public void OnUnlockButtonClicked()
    {
        if (unlockButton.interactable)
        {
            Debug.Log($"Unlock button clicked! Steps: {overallSteps} >= Required: {requiredSteps}");
            // Add your unlock logic here
        }
        else
        {
            Debug.Log($"Unlock button clicked but not enough steps! Steps: {overallSteps} < Required: {requiredSteps}");
        }
    }
}
