using System.Collections.Generic;
using UnityEngine;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using System.Threading.Tasks;
using System;
using Unity.Services.Authentication;
using System.Globalization;

[System.Serializable]
public class PlayerPrefsData2
{
    public List<string> keys = new();
    public List<string> values = new();
    public List<string> types = new(); 
}

public static class PlayerPrefsCloudSync2
{
    private const string CloudKey = "AllPlayerPrefsBackup";

    public static event Action onPlayerPrefsLoaded;

    private static readonly List<(string key, string type)> TrackedKeys = new()
    {
        // Rewards
        ("RewardClaimed", "int"),
        ("RewardClaimed_0", "int"),
        ("RewardClaimed_1", "int"),
        ("RewardClaimed_2", "int"),
        ("firstRewards", "int"),

        // Shards
        ("ShardCollected_0", "int"),
        ("ShardCollected_1", "int"),
        ("ShardCollected_2", "int"),
        ("ShardCollected_3", "int"),
        ("ShardCollected_4", "int"),
        ("ShardCollected_5", "int"),
        ("ShardCollected_6", "int"),
        ("ShardCollected_7", "int"),
        ("ShardCollected_8", "int"),

        // Dungeons
        ("DungeonComplete_0", "int"),
        ("DungeonComplete_1", "int"),
        ("DungeonComplete_2", "int"),
        ("DungeonComplete_3", "int"),
        ("DungeonComplete_4", "int"),
        ("DungeonComplete_5", "int"),
        ("DungeonComplete_6", "int"),
        ("DungeonComplete_7", "int"),
        ("DungeonComplete_8", "int"),

        // Player
        ("PlayerGold", "int"),
        ("PlayerLevel", "int"),
        ("ItemClaimed", "int"),

        // Story
        ("StoryCompleted_0", "int"),
        ("StoryCompleted_1", "int"),
        ("StoryCompleted_2", "int"),
        ("StoryCompleted_3", "int"),
        ("StoryCompleted_4", "int"),
        ("StoryCompleted_5", "int"),
        ("StoryCompleted_6", "int"),
        ("StoryCompleted_7", "int"),
        ("StoryCompleted_8", "int"),

        // Settings
        ("MusicVolume", "float"),
        ("SFXVolume", "float"),
    };


    public static async Task<int> SaveAllToCloud()
    {
        if (!IsSignedIn())
        {
            Debug.LogWarning("[PlayerPrefsCloudSync] SaveAllToCloud skipped — not signed in.");
            return 0;
        }

        var data = new PlayerPrefsData2();

        foreach (var (key, type) in TrackedKeys)
        {
            try
            {
                switch (type)
                {
                    case "int":
                        data.keys.Add(key);
                        data.values.Add(PlayerPrefs.GetInt(key, 0).ToString());
                        data.types.Add("int");
                        break;

                    case "float":
                        data.keys.Add(key);
                        data.values.Add(PlayerPrefs.GetFloat(key, 0f).ToString("R", CultureInfo.InvariantCulture));
                        data.types.Add("float");
                        break;

                    case "string":
                        string sVal = PlayerPrefs.GetString(key, "");
                        data.keys.Add(key);
                        data.values.Add(sVal);
                        data.types.Add("string");
                        break;

                    default:
                        Debug.LogWarning($"[PlayerPrefsCloudSync] Unknown type '{type}' for key '{key}' — skipped.");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerPrefsCloudSync] Could not read key '{key}': {e.Message}");
            }
        }

        if (data.keys.Count == 0)
        {
            return 0;
        }

        string json = JsonUtility.ToJson(data);
        var cloudData = new Dictionary<string, object> { { CloudKey, json } };

        try
        {
            await CloudSaveService.Instance.Data.Player.SaveAsync(cloudData);
            Debug.Log($"[PlayerPrefsCloudSync] Saved {data.keys.Count} keys to cloud.");
            return data.keys.Count;
        }
        catch (Exception e)
        {
            Debug.LogError("[PlayerPrefsCloudSync] Cloud save failed: " + e);
            return 0;
        }
    }


    public static async Task LoadAllFromCloud()
    {
        if (!IsSignedIn())
        {
            Debug.LogWarning("[PlayerPrefsCloudSync] LoadAllFromCloud skipped — not signed in.");
            return;
        }

        try
        {
            var cloudData = await CloudSaveService.Instance.Data.Player
                .LoadAsync(new HashSet<string> { CloudKey });

            if (!cloudData.TryGetValue(CloudKey, out var item))
            {
                Debug.LogWarning("[PlayerPrefsCloudSync] No saved PlayerPrefs found in cloud.");
                return;
            }

            string json = item.Value.GetAsString();
            PlayerPrefsData2 data = JsonUtility.FromJson<PlayerPrefsData2>(json);

            if (data == null || data.keys == null)
            {
                Debug.LogWarning("[PlayerPrefsCloudSync] Cloud data is null or malformed.");
                return;
            }

            // keys, values, and types must be parallel lists of the same length
            if (data.keys.Count != data.values.Count || data.keys.Count != data.types.Count)
            {
                Debug.LogError("[PlayerPrefsCloudSync] Corrupted cloud data — list length mismatch. Skipping load.");
                return;
            }

            for (int i = 0; i < data.keys.Count; i++)
            {
                string key = data.keys[i];
                string value = data.values[i];
                string type = data.types[i];

                try
                {
                    switch (type)
                    {
                        case "int":
                            PlayerPrefs.SetInt(key, int.Parse(value));
                            break;
                        case "float":
                            PlayerPrefs.SetFloat(key, float.Parse(value, CultureInfo.InvariantCulture));
                            break;
                        case "string":
                            PlayerPrefs.SetString(key, value);
                            break;
                        default:
                            Debug.LogWarning($"[PlayerPrefsCloudSync] Unknown type '{type}' for key '{key}' — skipped.");
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PlayerPrefsCloudSync] Failed to restore '{key}' ({type}): {e.Message}");
                }
            }

            PlayerPrefs.Save();
            Debug.Log($"[PlayerPrefsCloudSync] Loaded {data.keys.Count} keys from cloud.");

            // Notify subscribers so UI refreshes immediately with the new values.
            onPlayerPrefsLoaded?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError("[PlayerPrefsCloudSync] LoadAllFromCloud failed: " + e);
        }
    }

    private static bool IsSignedIn(){
        if (UnityServices.State == ServicesInitializationState.Initialized &&
            AuthenticationService.Instance != null &&
            AuthenticationService.Instance.IsSignedIn)
        {
            return true;
        }

        if (AuthManager.Instance != null)
        {
            return AuthManager.Instance.IsSignedIn;
        }

        return PlayerPrefs.GetInt("PlayerSignedIn", 0) == 1;
    }
}