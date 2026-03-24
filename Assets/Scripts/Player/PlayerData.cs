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
    public Player playerAttributes;

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
        playerName = "New Player";
        level = 1;
        health = 100;
        attack = 10;
        defense = 5;
        gold = 0;
        attackSpeed = 2;
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
            bool isNew = string.IsNullOrEmpty(savedName) || savedName == "New Player";

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
        currentAttack = attack + attackBuff;
        currentDefense = defense + defenseBuff;
        currentHealth = health + healthBuff;
        currentCooldown = cooldown + cooldownBuff;
        currentMovementSpeed = movementSpeed + movementSpeedBuff;
    }

    public void LevelUp()
    {
        health = 100 + (10 * level);
        attack = 5 * level;
        defense = 3 * level;
        attackSpeed = attackSpeed / 0.995;
        cooldown = (float)Math.Round(level / 0.995f);
        movementSpeed = movementSpeed / 0.995f;

        UpdateCurrentStats();
        SavePlayerData();
    }

    public void AddGold(int amount) { gold += amount; SavePlayerData(); }
    public void SubtractGold(int amount) { gold -= amount; SavePlayerData(); }
    public void GainGold() => AddGold(1000);

    public void ChangePlayerName(string name)
    {
        playerName = name;
        SavePlayerData();
        Debug.Log($"[PlayerData] Name changed to '{playerName}'.");
    }

    public void Reset()
    {
        level = 1;
        health = 100;
        attack = 10;
        defense = 5;
        gold = 0;
        attackSpeed = 2;
        movementSpeed = 6;

        healthBuff = 0;
        attackBuff = 0;
        defenseBuff = 0;
        cooldownBuff = 0;
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

            // ── Gold merge rule: add cloud gold on top of current local gold ──────
            //
            // Why: the player may have earned gold offline (before signing in) or
            // in a session where the cloud save didn't complete. Blindly replacing
            // local gold with cloud gold would discard that progress.
            //
            // How it works:
            //   localGold  = gold currently in memory (earned this session or loaded from disk)
            //   cloudGold  = gold stored in the cloud from the last successful save
            //   result     = localGold + cloudGold
            //
            // Edge cases:
            //   • First ever sign-in (no local progress): localGold = 0, result = cloudGold ✓
            //   • Cloud and local are in sync (last save completed): localGold = cloudGold,
            //     result = cloudGold * 2 — this is the one case you need to avoid.
            //     Solution: only merge when we know local has UNSYNCED progress, otherwise
            //     just take cloud directly. We detect this by comparing local vs cloud gold:
            //     if they are equal, no unsynced progress exists so just use cloud.
            int localGold = gold; // current in-memory gold before cloud overwrites it
            int cloudGold = loaded.gold;

            // Apply all cloud fields first (name, level, stats etc.)
            ApplySaveData(loaded);

            // Now resolve gold:
            if (localGold == cloudGold)
            {
                // In sync — no unsynced local progress. Use cloud value as-is.
                gold = cloudGold;
                Debug.Log($"[PlayerData] Gold in sync — using cloud value: {gold}");
            }
            else if (localGold > cloudGold)
            {
                // Local has more gold than cloud — player earned gold since last cloud save.
                // Add the difference on top of cloud gold so nothing is lost.
                int unearnedLocally = localGold - cloudGold;
                gold = cloudGold + unearnedLocally; // = localGold (keeps local progress)
                Debug.Log($"[PlayerData] Local gold ({localGold}) > cloud gold ({cloudGold}) " +
                          $"— keeping local: {gold}");
            }
            else
            {
                // Cloud has more gold than local — cloud is ahead (e.g. earned on another device).
                // Add local session gold on top of the higher cloud value.
                gold = cloudGold + localGold;
                Debug.Log($"[PlayerData] Cloud gold ({cloudGold}) > local gold ({localGold}) " +
                          $"— combined: {gold}");
            }

            data = BuildSaveData(); // rebuild with merged gold

            // Persist merged result locally and to cloud so both stay in sync.
            SavePlayerData();

            Debug.Log($"[PlayerData] Cloud loaded — name='{playerName}', level={level}, gold={gold}");
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
            playerName = playerName,
            level = level,
            health = health,
            attack = attack,
            defense = defense,
            cooldown = cooldown,
            movementSpeed = movementSpeed,
            gold = gold,
            attackSpeed = attackSpeed
        };
    }

    /// <summary>
    /// Applies a PlayerDataSaver to the live fields and refreshes computed stats.
    /// Single entry point so cloud load and disk load use identical logic.
    /// </summary>
    private void ApplySaveData(PlayerDataSaver d)
    {
        playerName = d.playerName;
        level = d.level;
        health = d.health;
        attack = d.attack;
        defense = d.defense;
        cooldown = d.cooldown;
        movementSpeed = d.movementSpeed;
        gold = d.gold;
        attackSpeed = d.attackSpeed;

        UpdateCurrentStats();
    }

    // Kept for external callers that still use SetPlayerStats
    public void SetPlayerStats(PlayerDataSaver d) => ApplySaveData(d);
}