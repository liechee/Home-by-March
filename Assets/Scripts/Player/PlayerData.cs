using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
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


    private string playerDataJsonFilePath;
    public PlayerDataSaver data;

    public Player playerAttributes;

    public int lastSavedLevel;

    public PlayerData()
    {
        // Default values for a new player
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
        //LoadPlayerData();

        // Initialize lastSavedLevel to the current level if not already set
        // if (lastSavedLevel == 0)
        // {
        //     lastSavedLevel = level;
        // }
        // // Load from file as fallback
        // if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
        // {
        //     Debug.Log("Fresh logout detected. Skipping step data load.");
        //     Reset();
        // }
        // If HasLoggedOut is set, reset all data and skip loading old data
        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
        {
            Debug.Log("Fresh logout detected. Resetting player data.");
            // Reset player data to safe defaults so UI shows Level 1 after logout
            Reset();
            // Persist the reset player data locally so startup state is consistent
            SavePlayerData();
            UpdateCurrentStats();
            return;
        }

        LoadPlayerData();

        // Initialize lastSavedLevel to the current level if not already set
        if (lastSavedLevel == 0)
        {
            lastSavedLevel = level;
        }


        UpdateCurrentStats();

    }

    public void UpdateCurrentStats()
    {
        // Update the current stats based on the base stats and buffs
        currentAttack = attack + attackBuff;
        currentDefense = defense + defenseBuff;
        currentHealth = health + healthBuff;
        currentCooldown = cooldown + cooldownBuff;
        currentMovementSpeed = movementSpeed + movementSpeedBuff;

        // Log the updated stats for debugging purposes
        Debug.Log("Current Stats Updated:");
        Debug.Log(attack + attackBuff);
        Debug.Log($"Current Attack: {currentAttack}, Current Defense: {currentDefense}, Current Health: {currentHealth}, Current Cooldown: {currentCooldown}, Current Movement Speed: {currentMovementSpeed}");
    }
    // void Update()
    // {
    //     attack += attackBuff;
    // }



    public void OnApplicationClose()
    {
        SavePlayerData();
    }

    // Method to level up the player
    public void LevelUp()
    {
        Debug.Log("Leveling up...");
        Debug.Log("Current health: " + health);

        health = 100 + (10 * level);
        attack = 5 * level;
        defense = 3 * level;
        attackSpeed = attackSpeed / 0.995;
        cooldown = (float)Math.Round(level / 0.995f);
        movementSpeed = movementSpeed / 0.995f;

        Debug.Log("New health: " + health);

        // Call UpdateCurrentStats to update currentCooldown and other stats
        UpdateCurrentStats();

        SavePlayerData();
    }


    public void AddGold(int amount)
    {
        gold += amount;
        SavePlayerData();
    }

    public void SubtractGold(int amount)
    {
        gold -= amount;
        SavePlayerData();
    }

    public void GainGold()
    {
        AddGold(1000);
    }


    public void SavePlayerData()
    {

        data = new PlayerDataSaver();
        data.playerName = playerName;
        data.level = level;
        data.health = health;
        data.attack = attack;
        data.defense = defense;
        data.cooldown = cooldown;
        data.movementSpeed = movementSpeed;
        data.gold = gold;
        data.attackSpeed = attackSpeed;


        string playerDataJson = JsonUtility.ToJson(data);
        System.IO.File.WriteAllText(playerDataJsonFilePath, playerDataJson);

        // Debug.Log("Saved Player Data" + data.playerName);


    }

    public void LoadPlayerData()
    {

        if (System.IO.File.Exists(playerDataJsonFilePath))
        {
            string playerDataJson = System.IO.File.ReadAllText(playerDataJsonFilePath);
            data = JsonUtility.FromJson<PlayerDataSaver>(playerDataJson);

            SetPlayerStats(data);

            Debug.Log("Player Data Loaded");


        }

    }

    public async Task SavePlayerDataToCloud()
    {

        await CloudSaver.SaveDataToCloud("playerData", data);

    }

    public async Task LoadPlayerDataFromCloud()
    {
        // string playerDataJson = await CloudSaver.LoadDataFromCloud("playerData");
        // data = JsonUtility.FromJson<PlayerDataSaver>(playerDataJson);
        // Debug.Log(data.playerName);


        // SetPlayerStats(data);
        // SavePlayerData();
        try
        {
            string playerDataJson = await CloudSaver.LoadDataFromCloud("playerData");

            if (!string.IsNullOrEmpty(playerDataJson))
            {
                data = JsonUtility.FromJson<PlayerDataSaver>(playerDataJson);
                SetPlayerStats(data);
                SavePlayerData(); // Save locally after loading from cloud
                Debug.Log("Cloud player data loaded successfully.");
            }
            else
            {
                Debug.LogWarning("No cloud data found. Falling back to local.");
                LoadPlayerData(); // fallback to local
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load from cloud: {e.Message}. Falling back to local.");
            LoadPlayerData(); // fallback
        }

    }

    public void SetPlayerStats(PlayerDataSaver playerData)
    {
        playerName = playerData.playerName;
        level = playerData.level;
        health = playerData.health;
        attack = playerData.attack;
        defense = playerData.defense;
        gold = playerData.gold;
        attackSpeed = playerData.attackSpeed;
        UpdateCurrentStats();

        Debug.Log("player stats set!");
    }

    public void ChangePlayerName(string name)
    {
        playerName = name;
        SavePlayerData();
    }
    public void Reset()
    {
        // Reset all relevant fields
        level = 1;
        health = 100;
        attack = 10;
        defense = 5;
        gold = 0;
        attackSpeed = 2;
        movementSpeed = 6;

        // Reset buffs
        healthBuff = 0;
        attackBuff = 0;
        defenseBuff = 0;
        cooldownBuff = 0;
        movementSpeedBuff = 0;

        lastSavedLevel = level;
    }


}