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
            
            // Get initial value
            overallSteps = stepCounter.overallSteps;
            
            Debug.Log($"RewardChecker: Got {overallSteps} steps from OverallStepCounter, subscribed to events");
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
        }
    }

    // Event handler - called when OverallStepCounter finishes calculating steps
    void OnStepsUpdated(int newOverallSteps, int newDailySteps)
    {
        overallSteps = newOverallSteps;
        UpdateButtonState();
        Debug.Log($"RewardChecker: Steps updated via event to {overallSteps}");
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
