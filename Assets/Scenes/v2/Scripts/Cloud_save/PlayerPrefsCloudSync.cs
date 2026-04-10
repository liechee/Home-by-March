using System.Collections.Generic;
using UnityEngine;
using Unity.Services.CloudSave;
using System.Threading.Tasks;
using System;

[System.Serializable]
public class PlayerPrefsData
{
    public List<string> keys = new();
    public List<string> values = new();
    public List<string> types = new(); 
}

public static class PlayerPrefsCloudSync
{
    private const string CloudKey = "AllPlayerPrefsBackup";

    public static event Action onPlayerPrefsLoaded;

    private static readonly List<(string key, string type)> TrackedKeys = new()
    {
        // Rewards
        ("RewardClaimed_0", "int"),
        ("RewardClaimed_1", "int"),
        ("RewardClaimed_2", "int"),

        // Shards
        ("ShardCollected_0","int"),
        ("ShardCollected_1", "int"),
        ("ShardCollected_2", "int"),

        // Dungeons
        ("DungeonComplete_0","int"),
        ("DungeonComplete_1","int"),
        ("DungeonComplete_2","int"),

        // Player
        ("PlayerGold", "int"),
        ("PlayerLevel", "int"),
        ("ItemClaimed",  "int"),

        // Story
        ("StoryCompleted_0", "int"),
        ("StoryCompleted_1", "int"),
        ("StoryCompleted_2", "int"),
        ("StoryCompleted_3", "int"),
    };


    public static async Task SaveAllToCloud()
    {
        if (!IsSignedIn())
        {
            Debug.LogWarning("[PlayerPrefsCloudSync] SaveAllToCloud skipped — not signed in.");
            return;
        }

        var data = new PlayerPrefsData();

        foreach (var (key, type) in TrackedKeys)
        {
            if (!PlayerPrefs.HasKey(key)) continue;

            try
            {
                switch (type)
                {
                    case "int":
                        data.keys.Add(key);
                        data.values.Add(PlayerPrefs.GetInt(key).ToString());
                        data.types.Add("int");
                        break;

                    case "float":
                        data.keys.Add(key);
                        data.values.Add(PlayerPrefs.GetFloat(key).ToString("R"));
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
            Debug.LogWarning("[PlayerPrefsCloudSync] SaveAllToCloud skipped — payload is empty (no tracked PlayerPrefs keys).");
            return;
        }

        string json = JsonUtility.ToJson(data);
        var cloudData = new Dictionary<string, object> { { CloudKey, json } };

        try
        {
            await CloudSaveService.Instance.Data.Player.SaveAsync(cloudData);
            Debug.Log($"[PlayerPrefsCloudSync] Saved {data.keys.Count} keys to cloud.");
        }
        catch (Exception e)
        {
            Debug.LogError("[PlayerPrefsCloudSync] Cloud save failed: " + e);
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
            PlayerPrefsData data = JsonUtility.FromJson<PlayerPrefsData>(json);

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
                            PlayerPrefs.SetFloat(key, float.Parse(value));
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

    private static bool IsSignedIn() =>
        Unity.Services.Core.UnityServices.State ==
            Unity.Services.Core.ServicesInitializationState.Initialized &&
        Unity.Services.Authentication.AuthenticationService.Instance != null &&
        Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn;
}