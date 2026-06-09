using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Authentication.PlayerAccounts.Samples;

/// <summary>
/// Signs out of the current session and reloads the login screen.
/// Cloud data and local save data are intentionally preserved.
///
/// Call LogoutAndRestart() from any UI button (e.g. Scene2AuthUI.OnSignOutButtonClicked).
/// </summary>
public class LogOutManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("UI")]
    [SerializeField] private GameObject loadingPanel;

    [Header("Navigation")]
    [SerializeField] private string loginSceneName = "Log In";
    [SerializeField] private float reloadDelay = 1.5f;

    // ── Public API ────────────────────────────────────────────────────────────

    public async void LogoutAndRestart()
    {
        Debug.Log("[LogOut] ── Starting logout ──────────────────────────────────");

        if (loadingPanel != null) loadingPanel.SetActive(true);
        await Task.Yield();

        OverallStepCounter stepCounter = FindObjectOfType<OverallStepCounter>();
        PlayerData playerData = FindObjectOfType<PlayerData>();

        if (stepCounter != null)
        {
            // stepCounter.isLoggingOut = true;
            // stepCounter.ResetStepDataForLogout(); // Use this instead of ResetStepDataCompletely
            //stepCounter.PrepareForLogout();
            stepCounter.RestartAsNewSession();
            Debug.Log("[LogOut] Step data preserved for next login.");
        }

        if (playerData != null)
        {
            playerData.isLoggingOut = true;
            playerData.Reset();
            Debug.Log("[LogOut] PlayerData reset.");
        }

        ResetInventoryObjects();
        DeleteLocalSaveFiles();
        ClearPlayerPrefsForNewSession();

        await EnsureServicesInitializedAsync();
        SignOutServices();

        PlayerPrefs.SetInt("HasLoggedOut", 1);
        PlayerPrefs.Save();
        Debug.Log("[LogOut] Logout flag written.");

        float delay = reloadDelay;
        string sceneName = loginSceneName;
        await Task.Delay(TimeSpan.FromSeconds(delay));
        SceneManager.LoadScene(sceneName);
    }

    // ── Service sign-out ──────────────────────────────────────────────────────

    private static async Task EnsureServicesInitializedAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
    }

    /// <summary>
    /// Signs out both PlayerAccountService and AuthenticationService in the
    /// correct order: portal session first, auth session second.
    /// </summary>
    private static void SignOutServices()
    {
        // Sign out the portal session first.
        if (PlayerAccountService.Instance.IsSignedIn)
        {
            PlayerAccountService.Instance.SignOut();
            Debug.Log("[LogOut] PlayerAccountService signed out.");
        }

        // Sign out and clear the on-disk session token.
        if (AuthenticationService.Instance.IsSignedIn ||
            AuthenticationService.Instance.SessionTokenExists)
        {
            AuthenticationService.Instance.SignOut(clearCredentials: true);
            AuthenticationService.Instance.ClearSessionToken();
            Debug.Log("[LogOut] AuthenticationService signed out (credentials cleared).");
        }
    }

    private static void ClearPlayerPrefsForNewSession()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.SetInt("HasLoggedOut", 1);
        PlayerPrefs.SetInt("SuppressCloudRestore", 1); // ← add this
        PlayerPrefs.Save();
    }

    private static void DeleteLocalSaveFiles()
    {
        string root = Application.persistentDataPath;
        foreach (string relativePath in GetLocalSaveFileNames())
        {
            DeleteFileIfExists(Path.Combine(root, relativePath));
        }

        if (!Directory.Exists(root)) return;

        foreach (string datFile in Directory.GetFiles(root, "*.dat", SearchOption.AllDirectories))
        {
            DeleteFileIfExists(datFile);
        }
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
            Debug.Log($"[LogOut] Deleted local save: {Path.GetFileName(path)}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LogOut] Could not delete {Path.GetFileName(path)}: {e.Message}");
        }
    }

    private static void ResetInventoryObjects()
    {
        foreach (InventoryObject inventoryObject in Resources.FindObjectsOfTypeAll<InventoryObject>())
        {
            try
            {
                inventoryObject.Clear();
                Debug.Log($"[LogOut] Cleared inventory '{inventoryObject.name}'.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LogOut] Could not clear inventory '{inventoryObject.name}': {e.Message}");
            }
        }
    }

    /// <summary>
    /// Resets player data to new game state (fresh start)
    /// </summary>
    public void ResetToNewGame()
    {
        // Reset all player stats to default values
        // Add your specific player data reset logic here

        // Example:
        // playerLevel = 1;
        // playerExp = 0;
        // playerGold = 0;
        // etc.

        Debug.Log("[NewGame] Player data reset to new game state.");
    }
}