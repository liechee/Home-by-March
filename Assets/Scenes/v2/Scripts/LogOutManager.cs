using System;
using System.IO;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Authentication.PlayerAccounts.Samples;

/// <summary>
/// Performs a full local data wipe and reloads the login screen.
/// Cloud data is intentionally preserved — only device-local state is cleared.
///
/// Call LogoutAndRestart() from any UI button (e.g. Scene2AuthUI.OnSignOutButtonClicked).
/// </summary>
public class LogOutManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("UI")]
    [SerializeField] private GameObject loadingPanel;

    [Header("Navigation")]
    [SerializeField] private string loginSceneName = "Entry Screen 1";
    [SerializeField] private float  reloadDelay    = 1.5f;

    // ── Public API ────────────────────────────────────────────────────────────

    public async void LogoutAndRestart()
    {
        Debug.Log("[LogOut] ── Starting logout ──────────────────────────────────");

        // 1. Block any in-flight cloud saves before we wipe.
        OverallStepCounter stepCounter = FindObjectOfType<OverallStepCounter>();
        if (stepCounter != null)
        {
            stepCounter.isLoggingOut = true;
            Debug.Log("[LogOut] Save guard enabled.");
        }

        PlayerPrefsCloudSyncButton syncButton = FindObjectOfType<PlayerPrefsCloudSyncButton>();
        if (syncButton != null) syncButton.enabled = false;

        // 2. Sign out of Unity services (also clears on-disk session token).
        await EnsureServicesInitializedAsync();
        SignOutServices();

        // 3. Wipe all local data (PlayerPrefs, persistent files, ScriptableObjects,
        //    DontDestroyOnLoad objects).
        WipeLocalData(stepCounter);

        // 4. Write the logout flag AFTER PlayerPrefs.DeleteAll() so it is the
        //    only key present. Scene1LoginUI reads this to skip auto-resume.
        PlayerPrefs.SetInt(AuthManager1.PrefHasLoggedOut, 1);
        PlayerPrefs.Save();
        Debug.Log("[LogOut] Logout flag written.");

        // 5. Show loading UI and reload.
        if (loadingPanel != null) loadingPanel.SetActive(true);

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

    // ── Local data wipe ───────────────────────────────────────────────────────

    private void WipeLocalData(OverallStepCounter stepCounter)
    {
        ResetStepCounter(stepCounter);
        ResetUserLevels();
        PlayerPrefs.DeleteAll();        // logout flag is written AFTER this call
        PlayerPrefs.Save();
        DeletePersistentFiles();
        ResetScriptableObjects();
        DestroyPersistentObjects(stepCounter);
        Debug.Log("[LogOut] Local data wipe complete.");
    }

    private static void ResetStepCounter(OverallStepCounter stepCounter)
    {
        if (stepCounter == null) return;
        stepCounter.ResetStepDataCompletely();
        Debug.Log("[LogOut] OverallStepCounter reset.");
    }

    private static void ResetUserLevels()
    {
        foreach (UserLevel ul in FindObjectsOfType<UserLevel>())
        {
            ul.dailyStepCount   = 0;
            ul.overallStepCount = 0;
            ul.currentStepCount = 0;
        }
    }

    private static void DeletePersistentFiles()
    {
        string root = Application.persistentDataPath;
        if (!Directory.Exists(root)) return;

        foreach (string file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
                Debug.Log($"[LogOut] Deleted: {Path.GetFileName(file)}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LogOut] Could not delete {Path.GetFileName(file)}: {e.Message}");
                // Last-resort: overwrite with empty content then retry delete.
                try { File.WriteAllText(file, ""); File.Delete(file); }
                catch { Debug.LogError($"[LogOut] Force-delete failed: {Path.GetFileName(file)}"); }
            }
        }

        // Clean up empty directories left behind.
        foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (Exception e) { Debug.LogWarning($"[LogOut] Could not remove dir: {e.Message}"); }
        }
    }

    private static void ResetScriptableObjects()
    {
        foreach (InventoryObject inv in Resources.FindObjectsOfTypeAll<InventoryObject>())
        {
            try { inv.Container.Clear(); }
            catch (Exception e) { Debug.LogWarning($"[LogOut] Could not clear {inv.name}: {e.Message}"); }
        }
    }

    /// <summary>
    /// Destroys all DontDestroyOnLoad objects except this manager itself, the
    /// OverallStepCounter (already reset), EventSystem, and AudioListener.
    /// </summary>
    private void DestroyPersistentObjects(OverallStepCounter stepCounter)
    {
        GameObject self           = gameObject;
        GameObject stepCounterObj = stepCounter != null ? stepCounter.gameObject : null;

        foreach (GameObject obj in FindObjectsOfType<GameObject>())
        {
            if (obj == null)             continue;
            if (obj == self)             continue;
            if (obj == stepCounterObj)   continue;
            if (obj.scene.name != "DontDestroyOnLoad") continue;

            string objName = "<unknown>";
            try { objName = obj.name; } catch { /* already destroyed */ }

            // Keep infrastructure objects that other systems rely on.
            if (objName.Contains("EventSystem") || objName.Contains("AudioListener"))
                continue;

            try
            {
                DestroyImmediate(obj);
                Debug.Log($"[LogOut] Destroyed: {objName}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LogOut] Could not destroy {objName}: {e.Message}");
            }
        }
    }
}