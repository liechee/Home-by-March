using UnityEngine;
using System.Threading.Tasks;
using System.IO;

/// <summary>
/// UI button handler for manual cloud sync.
/// Save is blocked during logout via isLoggingOut on OverallStepCounter,
/// and by LogOutManager disabling this component before the wipe runs.
/// Load is blocked while HasLoggedOut is set (post-logout, pre-new-sign-in).
/// </summary>
public class PlayerPrefsCloudSyncButton : MonoBehaviour
{
    private OverallStepCounter overallStepCounter;
    private PlayerData playerData;
    [SerializeField] CoppraGames.DailyRewardsWindow dailyRewardsWindow;
    // [SerializeField] private DynamicInterface dynamicInterface;
    // [SerializeField] private StaticInterface staticInterface;

    private InventoryObject inventory;
    private static PlayerPrefsCloudSyncButton instance;

    void Awake()
    {
        overallStepCounter = FindObjectOfType<OverallStepCounter>();
        playerData         = FindObjectOfType<PlayerData>();

        if (overallStepCounter == null) Debug.LogWarning("[CloudSync] OverallStepCounter not found!");
        if (playerData         == null) Debug.LogWarning("[CloudSync] PlayerData not found!");
        if (dailyRewardsWindow == null) Debug.LogWarning("[CloudSync] DailyRewardsWindow not found!");
        if (inventory          == null) Debug.LogWarning("[CloudSync] Inventory not found!");
    }

    // ─────────────────────────────────────────────────────────
    //  Save
    // ─────────────────────────────────────────────────────────

    public async void SaveToCloud()
    {
        // Guard: LogOutManager disables this component before the wipe,
        // but double-check isLoggingOut in case of race conditions.
        if (overallStepCounter != null && overallStepCounter.isLoggingOut)
        {
            Debug.LogWarning("[CloudSync] SaveToCloud blocked — logout in progress.");
            return;
        }

        Debug.Log("[CloudSync] ── SaveToCloud ────────────────────────────────────");

        await PlayerPrefsCloudSync.SaveAllToCloud();
        Debug.Log("[CloudSync] PlayerPrefs saved.");

        if (overallStepCounter != null)
        {
            await overallStepCounter.SaveStepDataToCloud();
            Debug.Log("[CloudSync] Step data saved.");
        }

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

        if (inventory != null)
        {
            await inventory.SaveInventoryToCloud("inventory_save.json");
            await inventory.SaveInventoryToCloud("New Inventory");
            Debug.Log("[CloudSync] Inventory saved.");
        }

        Debug.Log("[CloudSync] ── SaveToCloud complete ───────────────────────────");
    }

    // ─────────────────────────────────────────────────────────
    //  Load
    // ─────────────────────────────────────────────────────────

    public async void LoadFromCloud()
    {
        // Block cloud load while in post-logout state.
        // HasLoggedOut is set by LogOutManager and cleared by
        // OverallStepCounter.InitializeStepDataAfterLogout() once the
        // new session baseline is established.
        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
        {
            Debug.LogWarning("[CloudSync] LoadFromCloud blocked — post-logout state.");
            return;
        }

        // Also block if logout is actively in progress
        if (overallStepCounter != null && overallStepCounter.isLoggingOut)
        {
            Debug.LogWarning("[CloudSync] LoadFromCloud blocked — logout in progress.");
            return;
        }

        Debug.Log("[CloudSync] ── LoadFromCloud ───────────────────────────────────");

        await PlayerPrefsCloudSync.LoadAllFromCloud();
        Debug.Log("[CloudSync] PlayerPrefs loaded.");

        if (overallStepCounter != null)
        {
            await overallStepCounter.LoadStepDataFromCloud();
            ForceStepRefresh();
            Debug.Log("[CloudSync] Step data loaded.");
        }

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
            await inventory.LoadInventoryFromCloud("New Inventory");
            Debug.Log("[CloudSync] Inventory loaded.");
        }

        Debug.Log("[CloudSync] ── LoadFromCloud complete ──────────────────────────");
    }

    // ─────────────────────────────────────────────────────────
    //  Utility
    // ─────────────────────────────────────────────────────────

    public void ForceStepRefresh()
    {
        if (overallStepCounter == null) return;
        Debug.Log("[CloudSync] Forcing step refresh...");
        overallStepCounter.GetOverallSteps();
    }
}