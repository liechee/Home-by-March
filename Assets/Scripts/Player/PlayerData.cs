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

    [HideInInspector] public bool isLoggingOut = false;

    private string playerDataJsonFilePath;

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

    void Awake()
    {
        playerDataJsonFilePath = Application.persistentDataPath + "/playerData.json";

        LoadPlayerData();
        Debug.Log($"[PlayerData] Loaded — name='{playerName}', level={level}");

        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
        {
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

    public void OnApplicationClose() => SavePlayerDataAndSyncCloud();


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
        SavePlayerDataAndSyncCloud();
    }

    public void AddGold(int amount) { gold += amount; SavePlayerDataAndSyncCloud(); }
    public void SubtractGold(int amount) { gold -= amount; SavePlayerDataAndSyncCloud(); }
    public void GainGold() => AddGold(1000);

    public void ChangePlayerName(string name)
    {
        playerName = name;
        SavePlayerDataAndSyncCloud();
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

    public void SavePlayerData()
    {
        if (isLoggingOut) return;

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

    public async Task SavePlayerDataToCloud()
    {
        if (isLoggingOut) return;
        if (UnityServices.State != ServicesInitializationState.Initialized) return;
        if (!AuthenticationService.Instance.IsSignedIn) return;

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

            // Persist authoritative cloud result locally so both stores align.
            SavePlayerData();

            Debug.Log($"[PlayerData] Cloud loaded — name='{playerName}', level={level}, gold={gold}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerData] Cloud load failed: {e.Message} — falling back to local.");
            LoadPlayerData();
        }
    }

    private void SavePlayerDataAndSyncCloud()
    {
        SavePlayerData();

        if (isLoggingOut) return;
        if (UnityServices.State != ServicesInitializationState.Initialized) return;
        if (!AuthenticationService.Instance.IsSignedIn) return;

        _ = SavePlayerDataToCloud();
    }

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

    public void SetPlayerStats(PlayerDataSaver d) => ApplySaveData(d);
}