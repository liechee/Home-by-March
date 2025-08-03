using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Authentication;
using Unity.Services.Core;
using System.IO;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using System;

public class LogOutManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject loadingPanel;

    public async void LogoutAndRestart()
    {
        Debug.Log("Starting logout process...");

        // 1. Sign out from Unity Services
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            await UnityServices.InitializeAsync();
        }

        if (AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log("Signing out and deleting cloud data...");
            await DeleteAllCloudSaveData();
            AuthenticationService.Instance.SignOut();
            Debug.Log("Signed out completely.");
        }

        // 2. Nuclear data wipe
        NuclearDataWipe();

        // 3. Set logout flag
        PlayerPrefs.SetInt("HasLoggedOut", 1);
        PlayerPrefs.Save();

        // 4. Show loading and restart
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }

        Debug.Log("Complete data wipe finished. Restarting app...");
        StartCoroutine(QuitAfterDelay(2f));
    }

    private void NuclearDataWipe()
    {

        // 1. Reset step counters FIRST (before deleting files)
        ResetStepCounters();

        // 2. Clear ALL PlayerPrefs
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("PlayerPrefs deleted");

        // 3. Delete all files
        DeleteAllFiles();

        // 4. Reset ScriptableObjects
        ResetScriptableObjects();

        // 5. Destroy game objects
        DestroyGameObjects();

        Debug.Log("Delete COMPLETE");
    }

    private void ResetStepCounters()
    {
        Debug.Log("Resetting step counters...");

        try
        {
            // Find and reset OverallStepCounter
            OverallStepCounter stepCounter = FindObjectOfType<OverallStepCounter>();
            if (stepCounter != null)
            {
                // Stop all coroutines that might save data
                stepCounter.StopAllCoroutines();

                // Reset fields to zero
                stepCounter.overallSteps = 0;
                stepCounter.overallStepsBeforeToday = 0;
                stepCounter.stepData = null;

                Debug.Log("OverallStepCounter reset");
            }

            // Find and reset UserLevel
            UserLevel[] userLevels = FindObjectsOfType<UserLevel>();
            foreach (UserLevel userLevel in userLevels)
            {
                if (userLevel != null)
                {
                    userLevel.dailyStepCount = 0;
                    userLevel.overallStepCount = 0;
                    userLevel.currentStepCount = 0;
                    Debug.Log("UserLevel reset");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error resetting step counters: {e.Message}");
        }
    }

    private void DeleteAllFiles()
    {
        string persistentPath = Application.persistentDataPath;
        Debug.Log($"Deleting all files in: {persistentPath}");

        try
        {
            if (Directory.Exists(persistentPath))
            {
                // Get all files
                string[] allFiles = Directory.GetFiles(persistentPath, "*.*", SearchOption.AllDirectories);
                
                foreach (string file in allFiles)
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                        Debug.Log($"Deleted: {Path.GetFileName(file)}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Couldn't delete {Path.GetFileName(file)}: {e.Message}");
                        
                        // Try overwriting with empty content
                        try
                        {
                            File.WriteAllText(file, "");
                            File.Delete(file);
                            Debug.Log($"Force deleted: {Path.GetFileName(file)}");
                        }
                        catch
                        {
                            Debug.LogError($"Failed to delete: {Path.GetFileName(file)}");
                        }
                    }
                }

                // Delete directories
                string[] allDirs = Directory.GetDirectories(persistentPath, "*", SearchOption.AllDirectories);
                foreach (string dir in allDirs)
                {
                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Couldn't delete directory: {e.Message}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error deleting files: {e.Message}");
        }
    }

    private void ResetScriptableObjects()
    {
        Debug.Log("Resetting ScriptableObjects...");

        try
        {
            // Reset InventoryObjects
            InventoryObject[] inventoryObjects = Resources.FindObjectsOfTypeAll<InventoryObject>();
            foreach (InventoryObject inventoryObj in inventoryObjects)
            {
                try
                {
                    inventoryObj.Container.Clear();
                    Debug.Log($"Cleared: {inventoryObj.name}");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Couldn't clear {inventoryObj.name}: {e.Message}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error resetting ScriptableObjects: {e.Message}");
        }
    }

    private void DestroyGameObjects()
    {
        Debug.Log("Destroying game objects...");

        try
        {
            GameObject thisGameObject = this.gameObject;
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            
            foreach (GameObject obj in allObjects)
            {
                if (obj == null || obj == thisGameObject) continue;
                
                if (obj.scene.name == "DontDestroyOnLoad" && 
                    !obj.name.Contains("EventSystem") && 
                    !obj.name.Contains("AudioListener"))
                {
                    try
                    {
                        Debug.Log($"Destroying: {obj.name}");
                        DestroyImmediate(obj);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Error destroying {obj.name}: {e.Message}");
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error destroying objects: {e.Message}");
        }
    }

    private async Task DeleteAllCloudSaveData()
    {
        try
        {
            var keysResult = await CloudSaveService.Instance.Data.RetrieveAllKeysAsync();
            
            if (keysResult.Count > 0)
            {
                foreach (var key in keysResult)
                {
                    await CloudSaveService.Instance.Data.ForceDeleteAsync(key);
                    Debug.Log($"Cloud key deleted: {key}");
                }
                Debug.Log("All cloud data deleted");
            }
            else
            {
                Debug.Log("No cloud data to delete");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Error deleting cloud data: {e.Message}");
        }
    }

    private IEnumerator QuitAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Debug.Log("Restarting to Entry Screen...");
        SceneManager.LoadScene("Entry Screen");
    }
}
