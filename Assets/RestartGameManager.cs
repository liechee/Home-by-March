using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resets the game to a fresh state (New Game) - sets all progress to zero
/// including steps, player data, and inventory.
/// 
/// Call RestartNewGame() from any UI button to start a completely new game.
/// </summary>
public class RestartGameManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("UI")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private string loadingMessage = "Starting new game...";

    [Header("Navigation")]
    [SerializeField] private string targetSceneName = "Main";
    [SerializeField] private float reloadDelay = 1.5f;

    [Header("Step Reset")]
    [Tooltip("Set daily steps to zero on new game")]
    [SerializeField] private bool resetDailySteps = true;
    
    [Tooltip("Set overall steps to zero on new game")]
    [SerializeField] private bool resetOverallSteps = true;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Completely resets the game state to a fresh new game.
    /// Use this for "New Game" button.
    /// </summary>
    public async void RestartNewGame()
    {
        Debug.Log("[NewGame] ── Starting New Game - Resetting all progress ──────────────────");

        // Show loading UI
        if (loadingPanel != null) loadingPanel.SetActive(true);
        await Task.Yield();

        // Reset Step Counter - set daily and overall steps to zero
        OverallStepCounter stepCounter = FindObjectOfType<OverallStepCounter>();
        if (stepCounter != null)
        {
            stepCounter.isLoggingOut = true;
            stepCounter.ResetStepDataCompletely();
            
            // Ensure steps are explicitly set to zero
            stepCounter.ResetToNewGameState(resetDailySteps, resetOverallSteps);
            Debug.Log($"[NewGame] Step data reset: dailySteps={resetDailySteps}, overallSteps={resetOverallSteps}");
        }

        // Reset Player Data
        PlayerData playerData = FindObjectOfType<PlayerData>();
        if (playerData != null)
        {
            playerData.isLoggingOut = true;
            playerData.ResetToNewGame();
            Debug.Log("[NewGame] PlayerData reset to new game state.");
        }

        // Reset all inventory objects
        ResetInventoryObjects();
        
        // Delete all local save files
        DeleteLocalSaveFiles();
        
        // Clear PlayerPrefs but keep essential session data
        ClearPlayerPrefsForNewGame();

        // Optional: Delete cloud save data for a fresh start
        await DeleteCloudSaveData();

        // Delay and load target scene
        float delay = reloadDelay;
        string sceneName = targetSceneName;
        await Task.Delay(TimeSpan.FromSeconds(delay));
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// Resets only step data to zero while keeping other progress.
    /// Use this if you only want to reset steps.
    /// </summary>
    public async void ResetStepsOnly()
    {
        Debug.Log("[NewGame] Resetting steps only...");

        if (loadingPanel != null) loadingPanel.SetActive(true);
        await Task.Yield();

        OverallStepCounter stepCounter = FindObjectOfType<OverallStepCounter>();
        if (stepCounter != null)
        {
            stepCounter.ResetToNewGameState(true, true);
            Debug.Log("[NewGame] Steps reset to zero.");
        }

        float delay = reloadDelay;
        string sceneName = targetSceneName;
        await Task.Delay(TimeSpan.FromSeconds(delay));
        SceneManager.LoadScene(sceneName);
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private async Task DeleteCloudSaveData()
    {
        try
        {
            // Optionally clear cloud data for a completely fresh start
            if (AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("[NewGame] Clearing cloud save data...");
                await CloudSaver2.SaveData("stepData", null);
                await CloudSaver2.SaveData("playerData", null);
                Debug.Log("[NewGame] Cloud data cleared.");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NewGame] Could not clear cloud data: {e.Message}");
        }
    }

    private void ClearPlayerPrefsForNewGame()
    {
        // Clear all PlayerPrefs but mark that this is a new game session
        PlayerPrefs.DeleteAll();
        
        // Set new game flags
        PlayerPrefs.SetInt("IsNewGame", 1);
        PlayerPrefs.SetInt("GameStartedFresh", 1);
        PlayerPrefs.SetInt("SuppressStepQuery", 0);
        PlayerPrefs.SetInt("SuppressCloudRestore", 0);
        PlayerPrefs.Save();
        
        Debug.Log("[NewGame] PlayerPrefs cleared for new game.");
    }

    private static void DeleteLocalSaveFiles()
    {
        string root = Application.persistentDataPath;
        
        // Delete all known save files
        foreach (string relativePath in GetLocalSaveFileNames())
        {
            DeleteFileIfExists(Path.Combine(root, relativePath));
        }

        // Delete any .dat files
        if (Directory.Exists(root))
        {
            foreach (string datFile in Directory.GetFiles(root, "*.dat", SearchOption.AllDirectories))
            {
                DeleteFileIfExists(datFile);
            }
        }
        
        Debug.Log("[NewGame] Local save files deleted.");
    }

    private static IEnumerable<string> GetLocalSaveFileNames()
    {
        return new[]
        {
            "playerData.json",
            "stepData.json",
            "questData.json",
            "playerPositionData.json",
            "guestNameDraft.json",
            "playerDailyQuestData.json",
            "optimized_inventory.json",
            "test.json",
            "equipment.save",
            "inventory.save",
        };
    }

    private static void DeleteFileIfExists(string path)
    {
        if (!File.Exists(path)) return;

        try
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
            Debug.Log($"[NewGame] Deleted: {Path.GetFileName(path)}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NewGame] Could not delete {Path.GetFileName(path)}: {e.Message}");
        }
    }

    private static void ResetInventoryObjects()
    {
        foreach (InventoryObject inventoryObject in Resources.FindObjectsOfTypeAll<InventoryObject>())
        {
            try
            {
                inventoryObject.Clear();
                Debug.Log($"[NewGame] Cleared inventory: {inventoryObject.name}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NewGame] Could not clear inventory {inventoryObject.name}: {e.Message}");
            }
        }
    }
}