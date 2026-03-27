using UnityEngine;

public class Treasure : NPC
{
    [SerializeField] private string itemClaimedKey = "ItemClaimed";

    private void Start()
    {
        // Hide chest on scene load if already claimed
        if (PlayerPrefs.GetInt(itemClaimedKey, 0) == 1)
        {
            gameObject.SetActive(false);
        }
    }

    public bool UsesClaimKey(string key)
    {
        return itemClaimedKey == key;
    }

    public override void Interact()
    {
        // Don't interact if already claimed
        if (PlayerPrefs.GetInt(itemClaimedKey, 0) == 1)
        {
            gameObject.SetActive(false);
            return;
        }

        // Otherwise, let the base NPC interaction run (dialogue, etc.)
    }
}