using UnityEngine;
using System.Threading.Tasks;
using Unity.Services.Authentication;

public class PlayerPrefsCloudSyncButton2 : MonoBehaviour
{
    private OverallStepCounter                      overallStepCounter;
    private PlayerData                              playerData;
    [SerializeField] CoppraGames.DailyRewardsWindow dailyRewardsWindow;
    [SerializeField] private InventoryObject        inventory;
    [SerializeField] private InventoryObject        inventory2;

    // Cache for player ID to avoid repeated lookups
    private string cachedPlayerId;
    private string cachedPlayerIdKey = "CachedPlayerId";

    void Awake()
    {
        overallStepCounter = FindObjectOfType<OverallStepCounter>();
        playerData         = FindObjectOfType<PlayerData>();

        if (overallStepCounter == null) Debug.LogWarning("[CloudSync] OverallStepCounter not found!");
        if (playerData         == null) Debug.LogWarning("[CloudSync] PlayerData not found!");
        if (dailyRewardsWindow == null) Debug.LogWarning("[CloudSync] DailyRewardsWindow not found!");
        if (inventory          == null) Debug.LogWarning("[CloudSync] Inventory not found!");
        if (inventory2         == null) Debug.LogWarning("[CloudSync] Inventory2 not found!");
        
        // Subscribe to AuthManager events
        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.OnStateChanged += OnAuthStateChanged;
        }
    }

    private void OnDestroy()
    {
        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.OnStateChanged -= OnAuthStateChanged;
        }
    }

    private void OnAuthStateChanged()
    {
        // Clear cached player ID when auth state changes
        cachedPlayerId = null;
        
        // If signed in, cache the new player ID
        if (IsPlayerSignedIn())
        {
            GetCurrentPlayerId();
        }
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
        await LoadFromCloudAsync();
    }

    public async System.Threading.Tasks.Task LoadFromCloudAsync()
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

    /// <summary>
    /// Gets the current player ID from AuthManager or AuthenticationService
    /// </summary>
    private string GetCurrentPlayerId()
    {
        // Return cached ID if available
        if (!string.IsNullOrEmpty(cachedPlayerId))
            return cachedPlayerId;

        // Try to get from AuthManager first
        if (AuthManager.Instance != null)
        {
            if (AuthManager.Instance.IsSignedIn)
            {
                // Get PlayerId from AuthenticationService
                if (AuthenticationService.Instance != null && !string.IsNullOrEmpty(AuthenticationService.Instance.PlayerId))
                {
                    cachedPlayerId = AuthenticationService.Instance.PlayerId;
                    PlayerPrefs.SetString(cachedPlayerIdKey, cachedPlayerId);
                    PlayerPrefs.Save();
                    Debug.Log($"[CloudSync] Player ID from AuthenticationService: {cachedPlayerId}");
                    return cachedPlayerId;
                }
                
                // Try to get from CurrentPlayerData
                if (AuthManager.Instance.CurrentPlayerData != null && !string.IsNullOrEmpty(AuthManager.Instance.CurrentPlayerData.playerName))
                {
                    cachedPlayerId = AuthManager.Instance.GetPlayerId();
                    PlayerPrefs.SetString(cachedPlayerIdKey, cachedPlayerId);
                    PlayerPrefs.Save();
                    Debug.Log($"[CloudSync] Player ID from CurrentPlayerData: {cachedPlayerId}");
                    return cachedPlayerId;
                }
            }
            else if (AuthManager.Instance.IsGuest && !string.IsNullOrEmpty(AuthManager.Instance.GuestName))
            {
                // For guest players, use guest name as identifier
                cachedPlayerId = $"guest_{AuthManager.Instance.GuestName}";
                Debug.Log($"[CloudSync] Guest identifier: {cachedPlayerId}");
                return cachedPlayerId;
            }
        }

        // Fallback to cached PlayerPrefs
        if (PlayerPrefs.HasKey(cachedPlayerIdKey))
        {
            cachedPlayerId = PlayerPrefs.GetString(cachedPlayerIdKey);
            Debug.Log($"[CloudSync] Using cached Player ID: {cachedPlayerId}");
            return cachedPlayerId;
        }

        // Last resort - generate a device-specific ID
        cachedPlayerId = GetDeviceUniqueId();
        Debug.Log($"[CloudSync] Using device ID: {cachedPlayerId}");
        return cachedPlayerId;
    }

    /// <summary>
    /// Gets a unique device ID as fallback
    /// </summary>
    private string GetDeviceUniqueId()
    {
        // Try to get system device ID
        string deviceId = SystemInfo.deviceUniqueIdentifier;
        
        if (!string.IsNullOrEmpty(deviceId) && deviceId != "Unknown")
        {
            return $"device_{deviceId}";
        }
        
        // Generate a random ID if needed
        if (PlayerPrefs.HasKey("DeviceUniqueId"))
        {
            return PlayerPrefs.GetString("DeviceUniqueId");
        }
        
        string newId = $"device_{System.Guid.NewGuid().ToString()}";
        PlayerPrefs.SetString("DeviceUniqueId", newId);
        PlayerPrefs.Save();
        return newId;
    }

    /// <summary>
    /// Checks if player is signed in (either account or guest)
    /// </summary>
    private bool IsPlayerSignedIn()
    {
        if (AuthManager.Instance != null)
        {
            return AuthManager.Instance.IsSignedIn || AuthManager.Instance.IsGuest;
        }
        
        // Fallback to PlayerPrefs
        return PlayerPrefs.GetInt("PlayerSignedIn", 0) == 1;
    }

    private async Task SaveNonStepDataToCloud()
    {
        if (!IsPlayerSignedIn())
        {
            Debug.LogWarning("[CloudSync] SaveNonStepDataToCloud skipped — no active session.");
            return;
        }

        string playerId = GetCurrentPlayerId();
        Debug.Log($"[CloudSync] Saving data for Player ID: {playerId}");

        await PlayerPrefsCloudSync2.SaveAllToCloud();
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
            await inventory.SaveInventoryToCloud($"inventory_save_{playerId}.json");
            Debug.Log("[CloudSync] Inventory saved.");
        }

        if (inventory2 != null)
        {
            await inventory2.SaveInventoryToCloud($"inventory2_save_{playerId}.json");
            Debug.Log("[CloudSync] Inventory2 saved.");
        }
    }

    private async Task LoadNonStepDataFromCloud()
    {
        if (!IsSafeToProceed("LoadNonStepDataFromCloud")) return;
        
        if (!IsPlayerSignedIn())
        {
            Debug.LogWarning("[CloudSync] LoadNonStepDataFromCloud skipped — no active session.");
            return;
        }

        string playerId = GetCurrentPlayerId();
        Debug.Log($"[CloudSync] Loading data for Player ID: {playerId}");

        Debug.Log("[CloudSync] ── LoadNonStepDataFromCloud ───────────────────");

        await PlayerPrefsCloudSync2.LoadAllFromCloud();
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
            await inventory.LoadInventoryFromCloud($"inventory_save_{playerId}.json");
            Debug.Log("[CloudSync] Inventory loaded.");
        }

        if (inventory2 != null)
        {
            await inventory2.LoadInventoryFromCloud($"inventory2_save_{playerId}.json");
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