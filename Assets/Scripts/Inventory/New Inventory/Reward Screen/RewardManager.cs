using UnityEngine;

public class RewardManager : MonoBehaviour
{
    public GameObject rewardPanel; // The reward panel to show or hide
    private const string RewardClaimedKey = "RewardClaimed"; // Key for PlayerPrefs to store the claim status

    void Start()
    {
        // Check if the reward has already been claimed
        if (PlayerPrefs.GetInt(RewardClaimedKey, 0) == 1)
        {
            // If the reward has been claimed, set the panel inactive
            rewardPanel.SetActive(false);
        }
        else
        {
            // If not claimed, show the reward panel
            rewardPanel.SetActive(true);
        }
    }

    // Call this method when the player claims the reward
    public void ClaimReward()
    {
        // Mark the reward as claimed in PlayerPrefs
        PlayerPrefs.SetInt(RewardClaimedKey, 1);
        PlayerPrefs.Save();

        // Set the reward panel inactive after claiming
        rewardPanel.SetActive(false);
    }
}
