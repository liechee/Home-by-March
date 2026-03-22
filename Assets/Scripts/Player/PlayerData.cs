using System;
using UnityEngine;
using System.IO;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

[System.Serializable]
public class PlayerData : MonoBehaviour
{
    public string playerName;
    public int level;
    public float health;
    public int attack;
    public int defense;
    public float cooldown;
    public float healthBuff;
    public int attackBuff;
    public int defenseBuff;
    public float cooldownBuff;
    public float movementSpeed;
    public float movementSpeedBuff;
    public int gold;
    public double attackSpeed;

    public int currentAttack;
    public int currentDefense;
    public float currentHealth;
    public float currentCooldown;
    public float currentMovementSpeed;

    public int lastSavedLevel;

    public PlayerDataSaver data;
    public Player          playerAttributes;

    /// <summary>
    /// Set true by LogOutManager before the wipe begins.
    /// Blocks SavePlayerDataToCloud so wiped data is never
    /// re-uploaded during the logout sequence.
    /// </summary>
    [HideInInspector] public bool isLoggingOut = false;

    private string playerDataJsonFilePath;

    // ─────────────────────────────────────────────────────────
    //  Defaults
    // ─────────────────────────────────────────────────────────

    public PlayerData()
    {
        playerName    = "New Player";
        level         = 1;
        health        = 100;
        attack        = 10;
        defense       = 5;
        gold          = 0;
        attackSpeed   = 2;
        movementSpeed = 6;
    }

    // ─────────────────────────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────────────────────────

    void Awake()
    {
        playerDataJsonFilePath = Application.persistentDataPath + "/playerData.json";

        LoadPlayerData();
        Debug.Log($"[PlayerData] Loaded — name='{playerName}', level={level}");

        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
        {
            // Preserve the player name across logout so the UI can show it,
            // but reset all gameplay stats to Level 1 defaults.
            string savedName = playerName;
            bool   isNew     = string.IsNullOrEmpty(savedName) || savedName == "New Player";

            Reset();
            playerName = isNew ? "New Player" : savedName;

            SavePlayerData();
            UpdateCurrentStats();
            return;
        }

        if (lastSavedLevel == 0) lastSavedLevel = level;
        UpdateCurrentStats();
    }

    public void OnApplicationClose() => SavePlayerData();

    // ─────────────────────────────────────────────────────────
    //  Stats helpers
    // ─────────────────────────────────────────────────────────

    public void UpdateCurrentStats()
    {
        currentAttack        = attack        + attackBuff;
        currentDefense       = defense       + defenseBuff;
        currentHealth        = health        + healthBuff;
        currentCooldown      = cooldown      + cooldownBuff;
        currentMovementSpeed = movementSpeed + movementSpeedBuff;
    }

    public void LevelUp()
    {
        health        = 100 + (10 * level);
        attack        = 5  * level;
        defense       = 3  * level;
        attackSpeed   = attackSpeed / 0.995;
        cooldown      = (float)Math.Round(level / 0.995f);
        movementSpeed = movementSpeed / 0.995f;

        UpdateCurrentStats();
        SavePlayerData();
    }

    public void AddGold(int amount)      { gold += amount; SavePlayerData(); }
    public void SubtractGold(int amount) { gold -= amount; SavePlayerData(); }
    public void GainGold()               => AddGold(1000);

    public void ChangePlayerName(string name)
    {
        playerName = name;
        SavePlayerData();
        Debug.Log($"[PlayerData] Name changed to '{playerName}'.");
    }

    public void Reset()
    {
        level         = 1;
        health        = 100;
        attack        = 10;
        defense       = 5;
        gold          = 0;
        attackSpeed   = 2;
        movementSpeed = 6;

        healthBuff        = 0;
        attackBuff        = 0;
        defenseBuff       = 0;
        cooldownBuff      = 0;
        movementSpeedBuff = 0;

        lastSavedLevel = level;
    }

    // ─────────────────────────────────────────────────────────
    //  Local Save / Load
    // ─────────────────────────────────────────────────────────

    public void SavePlayerData()
    {
        if (isLoggingOut) return;

        // Always rebuild data from live fields before writing.
        // This ensures cloud and local saves are never stale.
        data = BuildSaveData();

        string json = JsonUtility.ToJson(data);
        File.WriteAllText(playerDataJsonFilePath, json);

        Debug.Log($"[PlayerData] Saved — name='{data.playerName}', level={data.level}, gold={data.gold}");
    }

    public void LoadPlayerData()
    {
        if (!File.Exists(playerDataJsonFilePath)) return;

        string json = File.ReadAllText(playerDataJsonFilePath);
        data = JsonUtility.FromJson<PlayerDataSaver>(json);

        if (data == null)
        {
            Debug.LogWarning("[PlayerData] File exists but deserialized to null — skipping.");
            return;
        }

        ApplySaveData(data);
        Debug.Log($"[PlayerData] Loaded from disk — name='{playerName}', level={level}");
    }

    // ─────────────────────────────────────────────────────────
    //  Cloud Save / Load
    // ─────────────────────────────────────────────────────────

    public async Task SavePlayerDataToCloud()
    {
        if (isLoggingOut) return;

        // Always rebuild from live fields so cloud receives current state,
        // not whatever data was last written by SavePlayerData().
        PlayerDataSaver snapshot = BuildSaveData();
        await CloudSaver.SaveDataToCloud("playerData", snapshot);

        Debug.Log($"[PlayerData] Cloud saved — name='{snapshot.playerName}', level={snapshot.level}");
    }

    public async Task LoadPlayerDataFromCloud()
    {
        try
        {
            string json = await CloudSaver.LoadDataFromCloud("playerData");

            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[PlayerData] No cloud data found — falling back to local.");
                LoadPlayerData();
                return;
            }

            PlayerDataSaver loaded = JsonUtility.FromJson<PlayerDataSaver>(json);
            if (loaded == null)
            {
                Debug.LogWarning("[PlayerData] Cloud JSON deserialized to null — falling back to local.");
                LoadPlayerData();
                return;
            }

            ApplySaveData(loaded);
            data = loaded;

            // Persist cloud data locally so next launch restores from disk
            // without needing a cloud fetch.
            SavePlayerData();

            Debug.Log($"[PlayerData] Cloud loaded — name='{playerName}', level={level}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerData] Cloud load failed: {e.Message} — falling back to local.");
            LoadPlayerData();
        }
    }

    // ─────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a PlayerDataSaver snapshot from the current live field values.
    /// Always call this before saving to cloud or disk — never send the cached
    /// `data` field directly, as it may be stale from a previous save cycle.
    /// </summary>
    private PlayerDataSaver BuildSaveData()
    {
        return new PlayerDataSaver
        {
            playerName    = playerName,
            level         = level,
            health        = health,
            attack        = attack,
            defense       = defense,
            cooldown      = cooldown,
            movementSpeed = movementSpeed,
            gold          = gold,
            attackSpeed   = attackSpeed
        };
    }

    /// <summary>
    /// Applies a PlayerDataSaver to the live fields and refreshes computed stats.
    /// Single entry point so cloud load and disk load use identical logic.
    /// </summary>
    private void ApplySaveData(PlayerDataSaver d)
    {
        playerName    = d.playerName;
        level         = d.level;
        health        = d.health;
        attack        = d.attack;
        defense       = d.defense;
        cooldown      = d.cooldown;
        movementSpeed = d.movementSpeed;
        gold          = d.gold;
        attackSpeed   = d.attackSpeed;

        UpdateCurrentStats();
    }

    // Kept for external callers that still use SetPlayerStats
    public void SetPlayerStats(PlayerDataSaver d) => ApplySaveData(d);
}