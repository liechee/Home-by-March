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
    [SerializeField] private bool debugLogs = false;

    void Awake()
    {
        stepCounter = FindObjectOfType<OverallStepCounter>();

        if (stepCounter != null)
        {
            // Get initial value (may be 0 until OverallStepCounter finishes loading)
            overallSteps = stepCounter.overallSteps;
            if (debugLogs)
                Debug.Log($"RewardChecker: Initialized with {overallSteps} steps from OverallStepCounter");
        }
        else
        {
            Debug.LogWarning("OverallStepCounter not found! Defaulting to 0 steps.");
            overallSteps = 0;
        }

        UpdateButtonState();
    }

    void OnEnable()
    {
        if (stepCounter == null)
            stepCounter = FindObjectOfType<OverallStepCounter>();

        if (stepCounter == null) return;

        // Idempotent subscription to avoid duplicate listeners after repeated enable/sign-in cycles.
        OverallStepCounter.onStepsUpdated -= OnStepsUpdated;
        OverallStepCounter.onLoaded -= OnStepDataLoaded;
        OverallStepCounter.onStepsUpdated += OnStepsUpdated;
        OverallStepCounter.onLoaded += OnStepDataLoaded;
    }

    void OnDisable()
    {
        OverallStepCounter.onStepsUpdated -= OnStepsUpdated;
        OverallStepCounter.onLoaded -= OnStepDataLoaded;
    }

    void OnDestroy()
    {
        OverallStepCounter.onStepsUpdated -= OnStepsUpdated;
        OverallStepCounter.onLoaded -= OnStepDataLoaded;
    }

    // Event handler - called when OverallStepCounter finishes calculating steps
    void OnStepsUpdated(int newOverallSteps, int newDailySteps)
    {
        overallSteps = newOverallSteps;
        UpdateButtonState();
        if (debugLogs)
            Debug.Log($"RewardChecker: Steps updated via event to {overallSteps}");
    }

    void OnStepDataLoaded()
    {
        // Ensure we read the processed values after cloud/local load
        if (stepCounter != null)
        {
            overallSteps = stepCounter.overallSteps;
            UpdateButtonState();
            if (debugLogs)
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
