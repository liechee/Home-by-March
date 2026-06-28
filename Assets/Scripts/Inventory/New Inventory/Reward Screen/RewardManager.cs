using UnityEngine;

public class RewardManager : MonoBehaviour
{
    public GameObject rewardPanel;
    private const string RewardClaimedKey = "RewardClaimed";
    private const string Scene1SignedInKey = "SignedInFromScene1Auth";

    void Start()
    {
        if (rewardPanel == null) return;

        // Only hide for real signed-in players who already claimed
        if (IsSignedInPlayer() && IsExistingPlayer())
        {
            rewardPanel.SetActive(false);
            return;
        }
    }

    public void ClaimReward()
    {
        // Only block if they're a real signed-in player who already claimed
        if (IsSignedInPlayer() && IsExistingPlayer())
        {
            if (rewardPanel != null) rewardPanel.SetActive(false);
            return;
        }

        PlayerPrefs.SetInt(RewardClaimedKey, 1);
        PlayerPrefs.SetInt("IsNewRegistration", 0);
        PlayerPrefs.Save();

        if (rewardPanel != null) rewardPanel.SetActive(false);

        Debug.Log("Reward granted.");
    }

    public bool IsExistingPlayer()
    {
        return PlayerPrefs.GetInt(RewardClaimedKey, 0) == 1;
    }

    private bool IsSignedInPlayer()
    {
        if (AuthManager.Instance == null) return false;
        bool isRealSignedIn = AuthManager.Instance.IsSignedIn && !AuthManager.Instance.IsGuest;
        bool isNewRegistration = PlayerPrefs.GetInt("IsNewRegistration", 0) == 1;
        bool isGuestUpgrade = PlayerPrefs.GetInt("IsGuestUpgrade", 0) == 1;

        // Guest upgrades are already signed in with claimed rewards — treat as returning player
        if (isGuestUpgrade) return isRealSignedIn;

        // Brand new registrants still need to see and claim the reward
        return isRealSignedIn && !isNewRegistration;
    }
}