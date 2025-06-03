using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PanelManager : MonoBehaviour
{
    public Player player;
    public float loadDelay = 0.5f; // Delay in seconds

    public void OnEnable()
    {
        gameObject.SetActive(true);
        if (player != null)
        {
            Debug.Log($"{gameObject.name} panel is now active.");
            StartCoroutine(LoadAfterDelay());
        }
    }

    public void OnDisable()
    {   
        gameObject.SetActive(false);
        if (player != null)
        {
            Debug.Log($"{gameObject.name} panel is now inactive.");
            player.inventory.Save();
            player.equipment.Save();
        }
    }

    private IEnumerator LoadAfterDelay()
    {
        yield return new WaitForSeconds(loadDelay); // Wait for the specified delay
        player.inventory.Load();
        player.equipment.Load();
        Debug.Log("Player data loaded after delay.");
    }

    public void SaveInventoryToCloud(){
        player.inventory.SaveInventoryToCloud("inventory");
        player.equipment.SaveInventoryToCloud("equipment");
        Debug.Log("Player data saved to cloud.");
    }

    public void LoadInventoryFromCloud(){
        player.inventory.LoadInventoryFromCloud("inventory");
        player.equipment.LoadInventoryFromCloud("equipment");
        Debug.Log("Player data loaded from cloud.");
    }
}
