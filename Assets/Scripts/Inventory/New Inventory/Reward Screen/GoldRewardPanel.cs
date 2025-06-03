using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class GoldRewardPanel : MonoBehaviour
{
    [Header("UI Components")]
    public TextMeshProUGUI goldText; // Reference to the gold display text.
    public Button rewardButton;      // Reference to the button.

    [Header("Player Data")]
    public PlayerData playerData;    // Reference to PlayerData script.

    [Header("Reward Settings")]
    public int goldToGain = 100;     // Amount of gold to reward.

    private void Start()
    {
        // Ensure the button is set to trigger the reward when clicked.
        if (rewardButton != null)
        {
            rewardButton.onClick.AddListener(GrantGoldReward);
        }

        // Update the gold text at the start.
        UpdateGoldText();
    }

    // Function to grant gold reward.
    private void GrantGoldReward()
    {
        if (playerData != null)
        {
            playerData.AddGold(goldToGain); // Increase gold in PlayerData.
            UpdateGoldText();               // Refresh the gold text.
        }
        else
        {
            Debug.LogWarning("PlayerData reference is missing.");
        }
    }

    // Function to update the gold text display.
    private void UpdateGoldText()
    {
        if (goldText != null && playerData != null)
        {
            goldText.text = "Gold: " + goldToGain;
        }
        else
        {
            Debug.LogWarning("Gold text or PlayerData reference is missing.");
        }
    }
}
