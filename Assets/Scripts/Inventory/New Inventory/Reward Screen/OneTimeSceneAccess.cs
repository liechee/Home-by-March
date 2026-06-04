using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;// Assuming this is a custom namespace for handling async operations related to scene loading

public class OneTimeSceneAccess : MonoBehaviour
{
    private string playerDataPath;
    private bool playerDataExists; // player data check
    private AsyncOperation preloadedScene; // reference to async operations

    // void Start()
    // {
    //     playerDataPath = Application.persistentDataPath + "/playerData.json";
    //     playerDataExists = File.Exists(playerDataPath); // check if player data file exists at the start and store the result

    //     string targetScene = playerDataExists ? "Main Screen" : "Log In";
    //     preloadedScene = SceneManager.LoadSceneAsync(targetScene);
    //     preloadedScene.allowSceneActivation = false; // hold it ready
    // }
    void Start()
    {
        playerDataPath = Application.persistentDataPath + "/playerData.json";
        playerDataExists = File.Exists(playerDataPath);

        string lastLoginMethod = PlayerPrefs.GetString("LastLoginMethod", "");
        bool isAccountSession = lastLoginMethod == "UsernamePassword";
        bool isGuestSession = lastLoginMethod == "Guest";

        string targetScene;

        if (isAccountSession || isGuestSession)
            targetScene = "Main Screen"; // returning player, session known
        else
            targetScene = "Log In"; // first time or logged out

        preloadedScene = SceneManager.LoadSceneAsync(targetScene);
        preloadedScene.allowSceneActivation = false;
    }

    // This method will be called when the button is clicked
    public void OnSceneChangeButtonClick()
    {
        // Debug.Log("Button clicked");

        // // Check if player data exists in playerData.json
        // if (PlayerDataExists())
        // {
        //     // Player data exists, change scene to Main
        //     SceneManager.LoadScene("Main Screen");
        //     Debug.Log("[OneTimeSceneAccess] PlayerData file exists");
        // }
        // else
        // {
        //     // Player data doesn't exist, change scene to LogIn
        //     SceneManager.LoadScene("Log In");
        //     Debug.Log("[OneTimeSceneAccess] PlayerData does not exists");
        // }
        preloadedScene.allowSceneActivation = true; // instant switch
    }

    // Method to check if player data exists
    private bool PlayerDataExists()
    {
        //return File.Exists(playerDataPath);
        return playerDataExists;
    }
}
