using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Authentication;
using Unity.Services.Core;
using System.IO;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using System;

/// <summary>
/// Handles full player logout:
///   1. Sets isLoggingOut on OverallStepCounter FIRST — master guard that blocks
///      all saves (local + cloud) for the rest of the session.
///   2. Disables PlayerPrefsCloudSyncButton so it cannot trigger a save mid-wipe.
///   3. Deletes all cloud data and signs out.
///   4. Wipes all local data (PlayerPrefs, files, ScriptableObjects, DDOL objects).
///   5. Sets HasLoggedOut + SuppressCloudRestore so the next Awake() on
///      OverallStepCounter initializes a fresh zero-baseline session.
///   6. Reloads the Entry Screen.
/// </summary>
public class LogOutManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject loadingPanel;

    public async void LogoutAndRestart()
    {
        Debug.Log("[LOGOUT] ── Starting logout ─────────────────────────────────────");

        // ── Step 1: Block all saves immediately ──────────────────────────────────
        // isLoggingOut = true makes every save path in OverallStepCounter a no-op.
        // This must be the very first action before any await.
        OverallStepCounter stepCounter = FindObjectOfType<OverallStepCounter>();
        if (stepCounter != null)
        {
            stepCounter.isLoggingOut = true;
            Debug.Log("[LOGOUT] isLoggingOut set — all saves blocked.");
        }

        // Disable the sync button so it cannot fire SaveToCloud mid-wipe
        PlayerPrefsCloudSyncButton syncButton = FindObjectOfType<PlayerPrefsCloudSyncButton>();
        if (syncButton != null) syncButton.enabled = false;

        // ── Step 2: Ensure Unity Services are ready ──────────────────────────────
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        // ── Step 3: Delete cloud data and sign out ───────────────────────────────
        if (AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log("[LOGOUT] Deleting cloud data...");
            await DeleteAllCloudSaveData();
            AuthenticationService.Instance.SignOut();
            Debug.Log("[LOGOUT] Signed out.");
        }

        // ── Step 4: Wipe all local data ──────────────────────────────────────────
        NuclearDataWipe(stepCounter);

        // ── Step 5: Set flags for the new session ────────────────────────────────
        // HasLoggedOut      → Awake() routes to post-logout init (zero baseline)
        // SuppressCloudRestore → blocks cloud load until new player signs in
        PlayerPrefs.SetInt("HasLoggedOut", 1);
        PlayerPrefs.SetInt("SuppressCloudRestore", 1);
        PlayerPrefs.DeleteKey("CloudRestored");
        PlayerPrefs.DeleteKey("HasEverSignedIn");
        PlayerPrefs.Save();
        Debug.Log("[LOGOUT] Session flags set.");

        // ── Step 6: Reload ───────────────────────────────────────────────────────
        if (loadingPanel != null) loadingPanel.SetActive(true);
        StartCoroutine(ReloadEntryScreen(2f));
    }

    // ─────────────────────────────────────────────────────────
    //  Wipe
    // ─────────────────────────────────────────────────────────

    private void NuclearDataWipe(OverallStepCounter stepCounter)
    {
        // ResetStepDataCompletely: stops coroutines, increments sessionGen
        // (kills all in-flight StepCounterRequest callbacks), deletes local file.
        if (stepCounter != null)
        {
            stepCounter.ResetStepDataCompletely();
            Debug.Log("[LOGOUT] OverallStepCounter reset.");
        }

        foreach (UserLevel ul in FindObjectsOfType<UserLevel>())
        {
            ul.dailyStepCount   = 0;
            ul.overallStepCount = 0;
            ul.currentStepCount = 0;
        }

        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[LOGOUT] PlayerPrefs cleared.");

        DeleteAllFiles();
        ResetScriptableObjects();
        DestroyPersistentObjects(stepCounter);

        Debug.Log("[LOGOUT] Nuclear wipe complete.");
    }

    private void DeleteAllFiles()
    {
        string path = Application.persistentDataPath;
        if (!Directory.Exists(path)) return;

        foreach (string file in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
                Debug.Log($"[LOGOUT] Deleted: {Path.GetFileName(file)}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LOGOUT] Could not delete {Path.GetFileName(file)}: {e.Message}");
                try { File.WriteAllText(file, ""); File.Delete(file); }
                catch { Debug.LogError($"[LOGOUT] Force-delete failed: {Path.GetFileName(file)}"); }
            }
        }

        foreach (string dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
        {
            try { Directory.Delete(dir, true); }
            catch (Exception e) { Debug.LogWarning($"[LOGOUT] Could not delete dir: {e.Message}"); }
        }
    }

    private void ResetScriptableObjects()
    {
        foreach (InventoryObject inv in Resources.FindObjectsOfTypeAll<InventoryObject>())
        {
            try { inv.Container.Clear(); }
            catch (Exception e) { Debug.LogWarning($"[LOGOUT] Could not clear {inv.name}: {e.Message}"); }
        }
    }

    /// <summary>
    /// Destroys DontDestroyOnLoad objects except:
    ///   • This GameObject   — needed to finish the coroutine
    ///   • OverallStepCounter — must survive so isLoggingOut stays true until the
    ///     scene reload completes, blocking any final OnApplicationQuit save.
    ///   • EventSystem / AudioListener — required for UI/audio during reload
    /// </summary>
    private void DestroyPersistentObjects(OverallStepCounter stepCounter)
    {
        GameObject self           = gameObject;
        GameObject stepCounterObj = stepCounter != null ? stepCounter.gameObject : null;

        foreach (GameObject obj in FindObjectsOfType<GameObject>())
        {
            if (obj == null || obj == self || obj == stepCounterObj) continue;
            if (obj.scene.name != "DontDestroyOnLoad") continue;
            if (obj.name.Contains("EventSystem") || obj.name.Contains("AudioListener")) continue;

            try
            {
                Debug.Log($"[LOGOUT] Destroying: {obj.name}");
                DestroyImmediate(obj);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LOGOUT] Could not destroy {obj.name}: {e.Message}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    //  Cloud
    // ─────────────────────────────────────────────────────────

    private async Task DeleteAllCloudSaveData()
    {
        try
        {
            var keys = await CloudSaveService.Instance.Data.RetrieveAllKeysAsync();
            foreach (var key in keys)
            {
                await CloudSaveService.Instance.Data.ForceDeleteAsync(key);
                Debug.Log($"[LOGOUT] Cloud key deleted: {key}");
            }
            Debug.Log(keys.Count > 0 ? "[LOGOUT] All cloud data deleted." : "[LOGOUT] No cloud data found.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LOGOUT] Error deleting cloud data: {e.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────
    //  Scene Reload
    // ─────────────────────────────────────────────────────────

    private IEnumerator ReloadEntryScreen(float delay)
    {
        yield return new WaitForSeconds(delay);
        Debug.Log("[LOGOUT] ── Loading Entry Screen ──────────────────────────────");
        SceneManager.LoadScene("Entry Screen");
    }
}