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
    [SerializeField] private float  reloadDelay    = 1.5f;

    // ── Public API ────────────────────────────────────────────────────────────

    public async void LogoutAndRestart()
    {
        Debug.Log("[LogOut] ── Starting logout ──────────────────────────────────");

        // Let the UI show a frame before the sign-out work starts.
        if (loadingPanel != null) loadingPanel.SetActive(true);
        await Task.Yield();

        OverallStepCounter stepCounter = FindObjectOfType<OverallStepCounter>();
        if (stepCounter != null)
        {
            stepCounter.isLoggingOut = true;
            stepCounter.ResetStepDataCompletely();
            Debug.Log("[LogOut] Step data reset.");
        }

        PlayerData playerData = FindObjectOfType<PlayerData>();
        if (playerData != null)
        {
            playerData.isLoggingOut = true;
            playerData.Reset();
            Debug.Log("[LogOut] PlayerData reset.");
        }

        ResetInventoryObjects();
        DeleteLocalSaveFiles();
        ClearPlayerPrefsForNewSession();

        // Sign out of Unity services (also clears on-disk session token).
        await EnsureServicesInitializedAsync();
        SignOutServices();

        // Mark the session as explicitly logged out so Scene1 does not auto-resume.
        PlayerPrefs.SetInt(AuthManager1.PrefHasLoggedOut, 1);
        PlayerPrefs.Save();
        Debug.Log("[LogOut] Logout flag written.");

        // Capture locals before the async gap to avoid captured-variable issues.
        float  delay     = reloadDelay;
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
        PlayerPrefs.SetInt(AuthManager1.PrefHasLoggedOut, 1);
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
            "test.json"
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
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LogOut] Could not clear inventory '{inventoryObject.name}': {e.Message}");
            }
        }
    }

}