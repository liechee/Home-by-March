using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

public class CloudSaver2 : MonoBehaviour
{
    private static bool isInitialized = false;
    private const int SignInWaitTimeoutMs = 10000;

    async void Start()
    {
        await InitializeCloudServices();
    }

    private static async Task InitializeCloudServices()
    {
        if (isInitialized) return;

        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
                Debug.Log("[CloudSaver2] Unity Services initialized");
            }
            isInitialized = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[CloudSaver2] Failed to initialize: {e.Message}");
        }
    }

    private static async Task<bool> WaitForSignInAsync(int timeoutMs = SignInWaitTimeoutMs)
    {
        if (AuthenticationService.Instance.IsSignedIn)
            return true;

        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(250);

            if (AuthenticationService.Instance.IsSignedIn)
                return true;
        }

        return AuthenticationService.Instance.IsSignedIn;
    }

    /// <summary>
    /// Simple save - just use a simple key name (no player ID needed!)
    /// Cloud Save automatically associates data with the signed-in player
    /// </summary>
    public static async Task SaveData(string key, object data)
    {
        await InitializeCloudServices();

        if (data == null || string.IsNullOrEmpty(data.ToString()))
        {
            Debug.LogWarning($"[CloudSaver2] Skip save for '{key}' - data is empty");
            return;
        }

        // Keep key simple - no special characters, no player ID
        string cleanKey = key.Replace(" ", "_").Replace(".", "_").Replace("-", "_");
        
        var saveData = new Dictionary<string, object>
        {
            { cleanKey, data }
        };

        try
        {
            Debug.Log($"[CloudSaver2] Save request for '{cleanKey}' | UnityServices={UnityServices.State} | SignedIn={AuthenticationService.Instance.IsSignedIn} | PlayerId='{AuthenticationService.Instance.PlayerId}'");

            if (!await WaitForSignInAsync())
            {
                Debug.LogWarning($"[CloudSaver2] Cannot save '{cleanKey}' - player not signed in after waiting {SignInWaitTimeoutMs}ms");
                return;
            }

            await CloudSaveService.Instance.Data.Player.SaveAsync(saveData);
            Debug.Log($"[CloudSaver2] Saved: {cleanKey}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[CloudSaver2] Save failed for '{cleanKey}': {e.Message}");
        }
    }

    /// <summary>
    /// Simple load
    /// </summary>
    public static async Task<string> LoadData(string key)
    {
        await InitializeCloudServices();

        string cleanKey = key.Replace(" ", "_").Replace(".", "_").Replace("-", "_");

        try
        {
            Debug.Log($"[CloudSaver2] Load request for '{cleanKey}' | UnityServices={UnityServices.State} | SignedIn={AuthenticationService.Instance.IsSignedIn} | PlayerId='{AuthenticationService.Instance.PlayerId}'");

            if (!await WaitForSignInAsync())
            {
                Debug.LogWarning($"[CloudSaver2] Cannot load '{cleanKey}' - player not signed in after waiting {SignInWaitTimeoutMs}ms");
                return null;
            }

            var keys = new HashSet<string> { cleanKey };
            var result = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

            if (result != null && result.TryGetValue(cleanKey, out var item))
            {
                return item.Value.GetAsString();
            }
            
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[CloudSaver2] Load failed for '{cleanKey}': {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Save typed object as JSON
    /// </summary>
    public static async Task SaveObject(string key, object obj)
    {
        if (obj == null)
        {
            Debug.LogWarning($"[CloudSaver2] Cannot save null object for '{key}'");
            return;
        }

        string json = JsonUtility.ToJson(obj);
        await SaveData(key, json);
    }

    /// <summary>
    /// Load typed object from JSON
    /// </summary>
    public static async Task<T> LoadObject<T>(string key) where T : class
    {
        string json = await LoadData(key);
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CloudSaver2] Failed to parse: {e.Message}");
            return null;
        }
    }
}