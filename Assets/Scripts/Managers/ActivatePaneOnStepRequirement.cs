using UnityEngine;
using UnityEngine.UI; // Required for UI components
using System.Collections;
using TMPro; // For TextMeshPro text support

public class ActivatePaneOnLevelRequirement : MonoBehaviour
{
    [Header("Level Requirements")]
    public UserLevel userLevel; // Reference to the UserLevel script
    public int requiredLevel = 5; // Set the level requirement value in the inspector

    [Header("UI Elements")]
    public GameObject targetPane; // The pane or panel you want to deactivate (lock)
    public GameObject unlockInfoPanel; // Panel to show level required if not yet met
    public Button showInfoButton; // Button to show the required level panel
    public TMP_Text infoText; // Text component to display the required level

    [Header("Check Settings")]
    public float checkInterval = 1f; // Time interval (in seconds) between checks

    private bool paneUnlocked = false;

    void Start()
    {
        // Ensure proper initial states
        if (targetPane != null)
        {
            targetPane.SetActive(true); // Lock the pane initially
        }

        if (unlockInfoPanel != null)
        {
            unlockInfoPanel.SetActive(false); // Hide the info panel initially
        }

        if (showInfoButton != null)
        {
            showInfoButton.onClick.AddListener(ShowUnlockInfo); // Bind button click
        }

        // Start checking the level requirement every second
        StartCoroutine(CheckLevelRequirementPeriodically());
    }

    IEnumerator CheckLevelRequirementPeriodically()
    {
        while (!paneUnlocked)
        {
            CheckLevelRequirement(); // Check if the level requirement is met
            yield return new WaitForSeconds(checkInterval); // Wait for the specified interval (1 second)
        }
    }

    void CheckLevelRequirement()
    {
        if (userLevel != null && userLevel.playerData != null)
        {
            // Debugging the current level and comparing it
            Debug.Log("Current Level: " + userLevel.playerData.level);
            Debug.Log("Required Level: " + requiredLevel);

            if (userLevel.playerData.level >= requiredLevel)
            {
                UnlockPane(); // If level is met, unlock the pane
            }
        }
        else
        {
            Debug.LogError("UserLevel or PlayerData is not assigned in the inspector.");
        }
    }

    void UnlockPane()
    {
        if (targetPane != null && targetPane.activeSelf)
        {
            targetPane.SetActive(false); // Unlock the pane (turn it off)
            paneUnlocked = true; // Mark the pane as unlocked
            Debug.Log("Pane unlocked! Level requirement met: " + requiredLevel);

            // Hide the show info button once the level requirement is met
            if (showInfoButton != null)
            {
                showInfoButton.gameObject.SetActive(false); // Hide the button when level is met
            }
        }
    }

    void ShowUnlockInfo()
    {
        Debug.Log("ShowUnlockInfo called!"); // Confirm button click is registered

        if (userLevel != null && userLevel.playerData != null && unlockInfoPanel != null && infoText != null)
        {
            int currentLevel = userLevel.playerData.level;
            int remainingLevels = requiredLevel - currentLevel;

            // Debugging the current level vs required level for button click
            Debug.Log("Current Level: " + currentLevel);
            Debug.Log("Required Level: " + requiredLevel);

            if (currentLevel >= requiredLevel)
            {
                // If level is already met or exceeded, show the success message
                infoText.text = "Congratulations! The feature is already unlocked.";
                unlockInfoPanel.SetActive(true); // Show the info panel
                Debug.Log("Feature already unlocked.");

                // Hide the button since the feature is unlocked
                if (showInfoButton != null)
                {
                    showInfoButton.gameObject.SetActive(false);
                }
            }
            else
            {
                // Show how many levels are still needed
                infoText.text = $"You need to be level {requiredLevel} to unlock this feature!";
                unlockInfoPanel.SetActive(true); // Show the info panel
                Debug.Log($"Unlock Info Panel: {remainingLevels} levels remaining.");
            }
        }
        else
        {
            Debug.LogError("One or more references are not set in the Inspector.");
        }
    }
}