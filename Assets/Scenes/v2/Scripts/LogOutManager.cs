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
        Debug.Log("[LOGOUT] ── Starting logout ─────────────────────────────────────");

        OverallStepCounter stepCounter = FindObjectOfType<OverallStepCounter>();
        if (stepCounter != null)
        {
            stepCounter.isLoggingOut = true;
            Debug.Log("[LOGOUT] isLoggingOut set — all saves blocked.");
        }

        // Disable the sync button so it cannot fire SaveToCloud mid-wipe
        PlayerPrefsCloudSyncButton syncButton = FindObjectOfType<PlayerPrefsCloudSyncButton>();
        if (syncButton != null) syncButton.enabled = false;

        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log("[LOGOUT] Preserving cloud data; clearing local/session state only.");
            AuthenticationService.Instance.SignOut(true);
            Debug.Log("[LOGOUT] Signed out.");
        }

        NuclearDataWipe(stepCounter);

        PlayerPrefs.SetInt("HasLoggedOut", 1);
        PlayerPrefs.SetInt("SuppressCloudRestore", 1);
        PlayerPrefs.DeleteKey("CloudRestored");
        PlayerPrefs.DeleteKey("HasEverSignedIn");
        PlayerPrefs.Save();
        Debug.Log("[LOGOUT] Session flags set.");

        if (loadingPanel != null) loadingPanel.SetActive(true);
        StartCoroutine(ReloadEntryScreen(2f));
    }

    private void NuclearDataWipe(OverallStepCounter stepCounter)
    {
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

    private async Task DeleteAllCloudSaveData()
    {
        await Task.CompletedTask;
    }
    private IEnumerator ReloadEntryScreen(float delay)
    {
        yield return new WaitForSeconds(delay);
        Debug.Log("[LOGOUT] ── Loading Entry Screen ──────────────────────────────");
        SceneManager.LoadScene("LogIn Screen 2");
    }
}