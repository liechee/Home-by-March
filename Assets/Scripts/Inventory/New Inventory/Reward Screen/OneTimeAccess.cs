using UnityEngine;

public class OneTimePanelAccess : MonoBehaviour
{
    public GameObject panel;
    public string itemClaimedKey = "ItemClaimed";
    private const string Scene1SignedInKey = "SignedInFromScene1Auth";

    void Start()
    {
        if (ShouldSkipForScene1SignIn())
        {
            gameObject.SetActive(false);
            return;
        }

        // If already claimed, disable this object so it can't trigger again
        if (PlayerPrefs.GetInt(itemClaimedKey, 0) == 1)
        {
            gameObject.SetActive(false);
        }
    }

    public void ShowPanel()
    {
        if (ShouldSkipForScene1SignIn())
            return;

        if (PlayerPrefs.GetInt(itemClaimedKey, 0) == 0)
        {
            panel.SetActive(true);
        }
    }

    public void ClaimItem()
    {
        // Persist the claim
        PlayerPrefs.SetInt(itemClaimedKey, 1);
        PlayerPrefs.Save();

        // Close the panel
        panel.SetActive(false);

        // Find and hide all matching treasure chests in the scene
        Treasure[] treasures = FindObjectsOfType<Treasure>(true);
        foreach (Treasure treasure in treasures)
        {
            if (treasure != null && treasure.UsesClaimKey(itemClaimedKey))
            {
                treasure.gameObject.SetActive(false);
            }
        }

        // Disable self so this panel can never trigger again this session
        gameObject.SetActive(false);

        Debug.Log("Item claimed and panel closed.");
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