using UnityEngine;
using System.Threading.Tasks;
using Unity.VisualScripting;
using System.IO;

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
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;

        DontDestroyOnLoad(this.gameObject);
        overallStepCounter = FindObjectOfType<OverallStepCounter>();
        if (overallStepCounter == null) Debug.LogWarning("OverallStepCounter not found!");

        playerData = FindObjectOfType<PlayerData>();
        if (playerData == null) Debug.LogWarning("PlayerData not found!");

        //dailyRewardsWindow = FindObjectOfType<CoppraGames.DailyRewardsWindow>();
        if (dailyRewardsWindow == null) Debug.LogWarning("DailyRewardsWindow not found!");

        // // dynamicInterface = FindObjectOfType<DynamicInterface>();
        // if (dynamicInterface == null) Debug.LogWarning("DynamicInterface not found!");

        // // // staticInterface = FindObjectOfType<StaticInterface>();
        // if (staticInterface == null) Debug.LogWarning("StaticInterface not found!");

        //inventory = FindObjectOfType<InventoryObject>();  
        if (inventory == null) Debug.LogWarning("Inventory not found!");
    }

    public async void SaveToCloud()
    {
        // await PlayerPrefsCloudSync.SaveAllToCloud();
        // await overallStepCounter.SaveStepDataToCloud();
        // await playerData.SavePlayerDataToCloud();
        // dailyRewardsWindow.SaveDailyQuestProgressToCloud();
        // dynamicInterface.SaveInventoryToCloudButton();
        // staticInterface.SaveInventoryToCloudButton();
        Debug.Log("[CloudSync] Starting SaveToCloud...");
        await PlayerPrefsCloudSync.SaveAllToCloud();
        Debug.Log("[CloudSync] PlayerPrefs saved to cloud.");
        if (overallStepCounter != null)
        {
            await overallStepCounter.SaveStepDataToCloud();
            Debug.Log("[CloudSync] Step data saved to cloud.");
        }
        if (playerData != null)
        {
            await playerData.SavePlayerDataToCloud();
            Debug.Log("[CloudSync] Player data saved to cloud.");
        }
        if (dailyRewardsWindow != null)
        {
            await dailyRewardsWindow.SaveDailyQuestProgressToCloud();
            Debug.Log("[CloudSync] Daily quest progress saved to cloud.");
        }
        // if (dynamicInterface != null)
        // {
        //    await dynamicInterface.SaveInventoryToCloudButton();
        //     Debug.Log("[CloudSync] Dynamic inventory saved to cloud.");
        // }
        // if (staticInterface != null)
        // {
        //   await staticInterface.SaveInventoryToCloudButton();
        //     Debug.Log("[CloudSync] Static inventory saved to cloud.");
        // }
        if (inventory != null)
        {
            await inventory.SaveInventoryToCloud("inventory_save.json");
            Debug.Log("[CloudSync] Inventory saved to cloud.");
        }
        Debug.Log("[CloudSync] SaveToCloud complete.");
    }

    public async void LoadFromCloud()
    {
        // await PlayerPrefsCloudSync.LoadAllFromCloud();
        // await overallStepCounter.LoadStepDataFromCloud();
        // //overallStepCounter = FindObjectOfType<OverallStepCounter>();
        // await playerData.LoadPlayerDataFromCloud();
        // dailyRewardsWindow.LoadPlayerDataFromCloud();
        // dynamicInterface.LoadInventoryFromCloudButton();
        // staticInterface.LoadInventoryFromCloudButton();
        Debug.Log("[CloudSync] Starting LoadFromCloud...");
        await PlayerPrefsCloudSync.LoadAllFromCloud();
        Debug.Log("[CloudSync] PlayerPrefs loaded from cloud.");

        if (overallStepCounter != null)
        {
            await overallStepCounter.LoadStepDataFromCloud();
            Debug.Log("[CloudSync] Step data loaded from cloud.");
        }
        if (playerData != null)
        {
            await playerData.LoadPlayerDataFromCloud();
            Debug.Log("[CloudSync] Player data loaded from cloud.");
        }
        if (dailyRewardsWindow != null)
        {
            await dailyRewardsWindow.LoadPlayerDataFromCloud();
            Debug.Log("[CloudSync] Daily quest progress loaded from cloud.");
        }
        // if (dynamicInterface != null)
        // {
        //    await dynamicInterface.LoadInventoryFromCloudButton();
        //     Debug.Log("[CloudSync] Dynamic inventory loaded from cloud.");
        // }
        // if (staticInterface != null)
        // {
        //    await staticInterface.LoadInventoryFromCloudButton();
        //     Debug.Log("[CloudSync] Static inventory loaded from cloud.");
        // }
        if (inventory != null)
        {
            await inventory.LoadInventoryFromCloud("inventory_save.json");
            Debug.Log("[CloudSync] Inventory loaded from cloud.");
        }
        Debug.Log("[CloudSync] LoadFromCloud complete.");

    }

}
