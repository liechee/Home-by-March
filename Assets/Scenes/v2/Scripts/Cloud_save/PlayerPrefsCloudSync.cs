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
    public List<string> types = new(); // "int", "float", "string"
}

public static class PlayerPrefsCloudSync
{
    private const string CloudKey = "AllPlayerPrefsBackup";

    // 🔁 Keys to track (customize this list!)
    private static readonly List<string> TrackedKeys = new()
    {
        "RewardClaimed_0",
        "RewardClaimed_1",
        "RewardClaimed_2",
        "ShardCollected_0",
        "ShardCollected_1",
        "ShardCollected_2",
        "DungeonComplete_0",
        "DungeonComplete_1",
        "DungeonComplete_2",
        "PlayerGold",
        "PlayerLevel",
        "ItemClaimed",
        "StoryCompleted_0",
        "StoryCompleted_1",
        "StoryCompleted_2",
        "StoryCompleted_3",
        // Add any other PlayerPrefs keys you want to persist
    };

    public static async Task SaveAllToCloud()
    {
        // Ensure Unity Services and Authentication are initialized
        await Unity.Services.Core.UnityServices.InitializeAsync();
        if (!Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
            await Unity.Services.Authentication.AuthenticationService.Instance.SignInAnonymouslyAsync();

        PlayerPrefsData data = new();

        foreach (var key in TrackedKeys)
        {
            if (PlayerPrefs.HasKey(key))
            {
                data.keys.Add(key);

                // Try to detect type by attempting to parse as int, float, or fallback to string
                // Try int
                int iVal = PlayerPrefs.GetInt(key, int.MinValue);
                if (iVal != int.MinValue)
                {
                    data.values.Add(iVal.ToString());
                    data.types.Add("int");
                    continue;
                }

                // Try float
                float fVal = PlayerPrefs.GetFloat(key, float.NaN);
                if (!float.IsNaN(fVal))
                {
                    data.values.Add(fVal.ToString("R"));
                    data.types.Add("float");
                    continue;
                }

                // Fallback to string
                string sVal = PlayerPrefs.GetString(key, null);
                if (!string.IsNullOrEmpty(sVal))
                {
                    data.values.Add(sVal);
                    data.types.Add("string");
                }
            }
        }

        string json = JsonUtility.ToJson(data);
        var cloudData = new Dictionary<string, object> { { CloudKey, json } };
        try
        {
            await CloudSaveService.Instance.Data.Player.SaveAsync(cloudData);
            Debug.Log("[PlayerPrefsCloudSync] ✅ All PlayerPrefs saved to cloud.");
        }
        catch (Exception e)
        {
            Debug.LogError("[PlayerPrefsCloudSync] Cloud save failed: " + e);
        }
    }

    public static async Task LoadAllFromCloud()
    {
        // await Unity.Services.Core.UnityServices.InitializeAsync();
        // if (!Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
        //     await Unity.Services.Authentication.AuthenticationService.Instance.SignInAnonymouslyAsync();

        // var cloudData = await CloudSaveService.Instance.Data.LoadAsync(new HashSet<string> { CloudKey });

        // if (cloudData.TryGetValue(CloudKey, out var record))
        // {
        //     PlayerPrefsData data = JsonUtility.FromJson<PlayerPrefsData>(record.ToString());

        //     for (int i = 0; i < data.keys.Count; i++)
        //     {
        //         string key = data.keys[i];
        //         string value = data.values[i];
        //         string type = data.types[i];

        //         try
        //         {
        //             switch (type)
        //             {
        //                 case "int":
        //                     PlayerPrefs.SetInt(key, int.Parse(value));
        //                     break;
        //                 case "float":
        //                     PlayerPrefs.SetFloat(key, float.Parse(value));
        //                     break;
        //                 case "string":
        //                     PlayerPrefs.SetString(key, value);
        //                     break;
        //             }
        //         }
        //         catch (Exception e)
        //         {
        //             Debug.LogWarning($"[PlayerPrefsCloudSync] Failed to restore key '{key}' of type '{type}': {e.Message}");
        //         }
        //     }

        //     PlayerPrefs.Save();
        //     Debug.Log("[PlayerPrefsCloudSync] ✅ All PlayerPrefs loaded from cloud.");
        // }
        // else
        // {
        //     Debug.LogWarning("[PlayerPrefsCloudSync] No saved PlayerPrefs found in cloud.");
        // }\
        await Unity.Services.Core.UnityServices.InitializeAsync();
        if (!Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
            await Unity.Services.Authentication.AuthenticationService.Instance.SignInAnonymouslyAsync();

        // Use the new API:
        var cloudData = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { CloudKey });

        if (cloudData.TryGetValue(CloudKey, out var item))
        {
            // Use GetAsString() to get the JSON string
            string json = item.Value.GetAsString();
            PlayerPrefsData data = JsonUtility.FromJson<PlayerPrefsData>(json);

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
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PlayerPrefsCloudSync] Failed to restore key '{key}' of type '{type}': {e.Message}");
                }
            }

            PlayerPrefs.Save();
            Debug.Log("[PlayerPrefsCloudSync] ✅ All PlayerPrefs loaded from cloud.");
        }
        else
        {
            Debug.LogWarning("[PlayerPrefsCloudSync] No saved PlayerPrefs found in cloud.");
        }
    }
}
