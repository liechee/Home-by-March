using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Authentication;
using System.IO;
using System.Threading.Tasks;
using Unity.Services.Core;


public class LogOutManager : MonoBehaviour
{
    public string entrySceneName = "Entry Screen"; // Make sure this matches your scene name exactly
    // void Awake()
    // {
    //     DontDestroyOnLoad(this.gameObject);
    // }
    public void LogoutAndRestart()
    {
        // Debug.Log("Logging out...");

        // // Step 1: Sign out from Unity Authentication
        // if (AuthenticationService.Instance.IsSignedIn)
        // {
        //     AuthenticationService.Instance.SignOut();
        //     Debug.Log("Signed out.");
        // }
        // // Step 2: Clear PlayerPrefs
        // PlayerPrefs.DeleteAll();
        // PlayerPrefs.Save();

        // // Step 3: Delete all files and directories in persistent data path
        // string persistentPath = Application.persistentDataPath; // <-- Use the directory, not the file

        // if (Directory.Exists(persistentPath))
        // {
        //     foreach (string file in Directory.GetFiles(persistentPath))
        //     {
        //         try
        //         {
        //             File.SetAttributes(file, FileAttributes.Normal);
        //             File.Delete(file);
        //         }
        //         catch (System.Exception e)
        //         {
        //             Debug.LogWarning("Could not delete file: " + file + " Exception: " + e.Message);
        //         }
        //     }

        //     foreach (string dir in Directory.GetDirectories(persistentPath))
        //     {
        //         try
        //         {
        //             Directory.Delete(dir, true);
        //         }
        //         catch (System.Exception e)
        //         {
        //             Debug.LogWarning("Could not delete directory: " + dir + " Exception: " + e.Message);
        //         }
        //     }
        // }

        // Debug.Log("All local data cleared.");

        // Debug.Log("All local data cleared.");

        Debug.Log("Logging out...");

        // Ensure UnityServices is initialized
        if (!UnityServices.State.Equals(ServicesInitializationState.Initialized))
        {
            UnityServices.InitializeAsync();
        }

        try
        {
            if (AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut();
                Debug.Log("Signed out from AuthenticationService.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Error signing out: " + e.Message);
        }

        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("PlayerPrefs cleared.");

        string persistentPath = Application.persistentDataPath;

        if (Directory.Exists(persistentPath))
        {
            foreach (string file in Directory.GetFiles(persistentPath))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("Could not delete file: " + file + " Exception: " + e.Message);
                }
            }

            foreach (string dir in Directory.GetDirectories(persistentPath))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("Could not delete directory: " + dir + " Exception: " + e.Message);
                }
            }
        }

        Debug.Log("All local data cleared.");
        // Step 4: Reload the game to Entry Screen
        SceneManager.LoadScene(entrySceneName);
    }
}

