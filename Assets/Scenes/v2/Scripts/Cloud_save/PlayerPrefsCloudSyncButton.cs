using UnityEngine;
using System.Threading.Tasks;

public class PlayerPrefsCloudSyncButton : MonoBehaviour
{
    private OverallStepCounter                      overallStepCounter;
    private PlayerData                              playerData;
    [SerializeField] CoppraGames.DailyRewardsWindow dailyRewardsWindow;
    [SerializeField] private InventoryObject        inventory;
    [SerializeField] private InventoryObject        inventory2;

    void Awake()
    {
        overallStepCounter = FindObjectOfType<OverallStepCounter>();
        playerData         = FindObjectOfType<PlayerData>();

        if (overallStepCounter == null) Debug.LogWarning("[CloudSync] OverallStepCounter not found!");
        if (playerData         == null) Debug.LogWarning("[CloudSync] PlayerData not found!");
        if (dailyRewardsWindow == null) Debug.LogWarning("[CloudSync] DailyRewardsWindow not found!");
        if (inventory          == null) Debug.LogWarning("[CloudSync] Inventory not found!");
        if (inventory2         == null) Debug.LogWarning("[CloudSync] Inventory2 not found!");
    }

    async void Start()
    {
 
        await LoadNonStepDataFromCloud();
    }


    async void OnApplicationQuit()
    {
        if (!IsSafeToProceed("OnApplicationQuit")) return;
        await SaveNonStepDataToCloud();
    }

    async void OnApplicationPause(bool isPaused)
    {
        if (!isPaused || !IsSafeToProceed("OnApplicationPause")) return;
        await SaveNonStepDataToCloud();
    }


    public async void SaveToCloud()
    {
        if (!IsSafeToProceed("SaveToCloud")) return;

        Debug.Log("[CloudSync] ── SaveToCloud ──────────────────────────────────");

        // Step data: delegated to OverallStepCounter (owns its own state + guards)
        if (overallStepCounter != null)
        {
            await overallStepCounter.SaveStepDataToCloud();
            Debug.Log("[CloudSync] Step data saved.");
        }

        await SaveNonStepDataToCloud();

        Debug.Log("[CloudSync] ── SaveToCloud complete ─────────────────────────");
    }


    public async void LoadFromCloud()
    {
        if (!IsSafeToProceed("LoadFromCloud")) return;

        Debug.Log("[CloudSync] ── LoadFromCloud (manual) ──────────────────────");

        await LoadNonStepDataFromCloud();

        if (overallStepCounter != null)
        {
            await overallStepCounter.LoadStepDataFromCloud();
            Debug.Log("[CloudSync] Step data load requested.");
        }

        Debug.Log("[CloudSync] ── LoadFromCloud complete ──────────────────────");
    }

    private async Task SaveNonStepDataToCloud()
    {
        await PlayerPrefsCloudSync.SaveAllToCloud();
        Debug.Log("[CloudSync] PlayerPrefs saved.");

        if (playerData != null)
        {
            await playerData.SavePlayerDataToCloud();
            Debug.Log("[CloudSync] Player data saved.");
        }

        if (dailyRewardsWindow != null)
        {
            await dailyRewardsWindow.SaveDailyQuestProgressToCloud();
            Debug.Log("[CloudSync] Daily quest progress saved.");
        }

        // Inventory.Save() writes to local binary file.
        // SaveInventoryToCloud uploads the current in-memory Container as JSON.
        if (inventory != null)
        {
            await inventory.SaveInventoryToCloud("inventory_save.json");
            Debug.Log("[CloudSync] Inventory saved.");
        }

        if (inventory2 != null)
        {
            await inventory2.SaveInventoryToCloud("inventory2_save.json");
            Debug.Log("[CloudSync] Inventory2 saved.");
        }
    }


    private async Task LoadNonStepDataFromCloud()
    {
        if (!IsSafeToProceed("LoadNonStepDataFromCloud")) return;

        Debug.Log("[CloudSync] ── LoadNonStepDataFromCloud ───────────────────");

        await PlayerPrefsCloudSync.LoadAllFromCloud();
        Debug.Log("[CloudSync] PlayerPrefs loaded.");

        if (playerData != null)
        {
            await playerData.LoadPlayerDataFromCloud();
            Debug.Log("[CloudSync] Player data loaded.");
        }

        if (dailyRewardsWindow != null)
        {
            await dailyRewardsWindow.LoadPlayerDataFromCloud();
            Debug.Log("[CloudSync] Daily quest progress loaded.");
        }

        if (inventory != null)
        {
            await inventory.LoadInventoryFromCloud("inventory_save.json");
            Debug.Log("[CloudSync] Inventory loaded.");
        }

        if (inventory2 != null)
        {
            await inventory2.LoadInventoryFromCloud("inventory2_save.json");
            Debug.Log("[CloudSync] Inventory2 loaded.");
        }

        Debug.Log("[CloudSync] ── LoadNonStepDataFromCloud complete ──────────");
    }

    // ─────────────────────────────────────────────────────────
    //  Guard
    // ─────────────────────────────────────────────────────────

    private bool IsSafeToProceed(string caller)
    {
        if (overallStepCounter != null && overallStepCounter.isLoggingOut)
        {
            Debug.LogWarning($"[CloudSync] {caller} blocked — logout in progress.");
            return false;
        }
        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
        {
            Debug.LogWarning($"[CloudSync] {caller} blocked — post-logout state.");
            return false;
        }
        if (PlayerPrefs.GetInt("SuppressCloudRestore", 0) == 1)
        {
            Debug.LogWarning($"[CloudSync] {caller} blocked — cloud restore suppressed.");
            return false;
        }
        return true;
    }

    public void ForceStepRefresh()
    {
        if (overallStepCounter == null) return;
        overallStepCounter.GetOverallSteps();
    }
}