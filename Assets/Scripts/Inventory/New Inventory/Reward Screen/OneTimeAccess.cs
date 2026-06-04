using UnityEngine;

public class OneTimeAccess : MonoBehaviour
{
    public GameObject panel;
    public string itemClaimedKey = "ItemClaimed";
    private const string Scene1SignedInKey = "SignedInFromScene1Auth";

    void Start()
    {
        if (IsSignedInPlayer())
        {
            gameObject.SetActive(false);
            return;
        }

        if (PlayerPrefs.GetInt(itemClaimedKey, 0) == 1)
        {
            gameObject.SetActive(false);
        }
    }

    public void ShowPanel()
    {
        if (IsSignedInPlayer()) return;

        if (PlayerPrefs.GetInt(itemClaimedKey, 0) == 0)
        {
            panel.SetActive(true);
        }
    }

    public void ClaimItem()
    {
        PlayerPrefs.SetInt(itemClaimedKey, 1);
        PlayerPrefs.Save();

        panel.SetActive(false);

        Treasure[] treasures = FindObjectsOfType<Treasure>(true);
        foreach (Treasure treasure in treasures)
        {
            if (treasure != null && treasure.UsesClaimKey(itemClaimedKey))
                treasure.gameObject.SetActive(false);
        }

        gameObject.SetActive(false);
        Debug.Log("Item claimed and panel closed.");
    }

    // Only suppress for real signed-in accounts — guests still see the panel
    private bool IsSignedInPlayer()
    {
        if (AuthManager.Instance == null) return false;
        return AuthManager.Instance.IsSignedIn && !AuthManager.Instance.IsGuest;
    }
}