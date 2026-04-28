using UnityEngine;

public class RewardManager : MonoBehaviour
{
    public GameObject rewardPanel; // The reward panel to show or hide
    private const string RewardClaimedKey = "RewardClaimed"; // Key for PlayerPrefs to store the claim status
    private const string Scene1SignedInKey = "SignedInFromScene1Auth";

    void Start()
    {
        if (ShouldSkipForScene1SignIn())
        {
            if (rewardPanel != null) rewardPanel.SetActive(false);
            return;
        }

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

    private bool ShouldSkipForScene1SignIn()
    {
        if (PlayerPrefs.GetInt(Scene1SignedInKey, 0) != 1)
            return false;

        PlayerPrefs.DeleteKey(Scene1SignedInKey);
        PlayerPrefs.Save();
        return true;
    }
}
