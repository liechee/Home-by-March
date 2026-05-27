// using UnityEngine;
// using System;
// using System.Collections;
// using System.Collections.Generic;
// using Unity.Services.CloudSave;
// using Unity.Services.Core;
// using Unity.Services.Authentication;
// using System.Threading.Tasks;

// public class CloudSaver : MonoBehaviour
// {
//     async void Start()
//     {
//         await UnityServices.InitializeAsync();
//     }

//     public async static Task SaveDataToCloud(string key, object saveData)
//     {
//         if (!HasMeaningfulPayload(saveData))
//         {
//             bool hasExisting = await HasMeaningfulCloudValue(key);
//             if (hasExisting)
//             {
//                 Debug.LogWarning($"[CloudSaver] Skip save for '{key}' because payload is null/empty and cloud already has data.");
//                 return;
//             }

//             Debug.LogWarning($"[CloudSaver] Skip save for '{key}' because payload is null/empty.");
//             return;
//         }

//         Dictionary<string, object> data = new Dictionary<string, object>
//         {
//             { key, saveData }
//         };

//         try
//         {
//             await CloudSaveService.Instance.Data.Player.SaveAsync(data);
//             Debug.Log("Saved data for player ID: " + AuthenticationService.Instance.PlayerId);
//         }
//         catch (Exception e)
//         {
//             Debug.Log($"Failed to save data for player ID: {AuthenticationService.Instance.PlayerId}. Error: {e.Message}");
//         }
//     }

//     public async static Task<string> LoadDataFromCloud(string key)
//     {
//         // Returns data from the cloud save
//         var savedData = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { key });

//         if (!savedData.TryGetValue(key, out var item))
//         {
//             Debug.LogWarning($"[CloudSaver] No cloud value found for key '{key}'.");
//             return string.Empty;
//         }

//         string dataString = item.Value.GetAsString();
//         if (IsNullLikePayload(dataString))
//         {
//             Debug.LogWarning($"[CloudSaver] Cloud value for key '{key}' is empty/null-like.");
//             return string.Empty;
//         }

//         Debug.Log("Loaded data for player ID: " + AuthenticationService.Instance.PlayerId);

//         // Be sure to use JsonUtility.FromJson<classhere>(variableName); to extract from data classes
//         // T data = JsonUtility.FromJson<T>(dataString);
//         return dataString;
//     }

//     private static async Task<bool> HasMeaningfulCloudValue(string key)
//     {
//         try
//         {
//             var savedData = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { key });
//             if (savedData == null) return false;
//             if (!savedData.TryGetValue(key, out var item)) return false;
//             string existingValue = item.Value.GetAsString();
//             return !IsNullLikePayload(existingValue);
//         }
//         catch (Exception e)
//         {
//             Debug.LogWarning($"[CloudSaver] Could not check existing cloud value for '{key}': {e.Message}");
//             return false;
//         }
//     }

//     private static bool HasMeaningfulPayload(object saveData)
//     {
//         if (saveData == null) return false;

//         if (saveData is string s)
//             return !IsNullLikePayload(s);

//         try
//         {
//             string json = JsonUtility.ToJson(saveData);
//             return !IsNullLikePayload(json);
//         }
//         catch
//         {
//             return true;
//         }
//     }

//     private static bool IsNullLikePayload(string payload)
//     {
//         if (string.IsNullOrWhiteSpace(payload)) return true;

//         string normalized = payload.Trim().ToLowerInvariant();
//         return normalized == "null" || normalized == "\"null\"" || normalized == "{}" || normalized == "[]";
//     }
// }