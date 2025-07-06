using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    public async Task SaveInventoryToCloud(){
        await player.inventory.SaveInventoryToCloud("inventory");
        await player.equipment.SaveInventoryToCloud("equipment");
        Debug.Log("Player data saved to cloud.");
    }

    public async Task LoadInventoryFromCloud(){
        await player.inventory.LoadInventoryFromCloud("inventory");
        await player.equipment.LoadInventoryFromCloud("equipment");
        Debug.Log("Player data loaded from cloud.");
    }
}
