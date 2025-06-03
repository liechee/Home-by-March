using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

public class CloudSaver : MonoBehaviour
{
    async void Start()
    {
        await UnityServices.InitializeAsync();
    }

    public async static Task SaveDataToCloud(string key, object saveData)
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { key, saveData }
        };

        try
        {
            await CloudSaveService.Instance.Data.ForceSaveAsync(data);
            Debug.Log("Saved data for player ID: " + AuthenticationService.Instance.PlayerId);
        }
        catch
        {
            Debug.Log("Failed to save data for player ID: " + AuthenticationService.Instance.PlayerId);
        }
    }

    public async static Task<string> LoadDataFromCloud(string key)
    {
        // Returns data from the cloud save
        Dictionary<string, string> savedData = await CloudSaveService.Instance.Data.LoadAsync(new HashSet<string> { key });

        string dataString = savedData[key];
        Debug.Log("Loaded data for player ID: " + AuthenticationService.Instance.PlayerId);

        // Be sure to use JsonUtility.FromJson<classhere>(variableName); to extract from data classes
        // T data = JsonUtility.FromJson<T>(dataString);
        return dataString;
    }
}