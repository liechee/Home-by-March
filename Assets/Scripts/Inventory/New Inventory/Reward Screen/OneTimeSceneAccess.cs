using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

public class OneTimeSceneAccess : MonoBehaviour
{
    private string playerDataPath;

    void Start()
    {
        playerDataPath = Application.persistentDataPath + "/playerData.json";
    }

    // This method will be called when the button is clicked
    public void OnSceneChangeButtonClick()
    {
        Debug.Log("Button clicked");
        
        // Check if player data exists in playerData.json
        if (PlayerDataExists())
        {
            // Player data exists, change scene to Main
            SceneManager.LoadScene("Main Screen");
        }
        else
        {
            // Player data doesn't exist, change scene to LogIn
            SceneManager.LoadScene("LogIn Screen");
        }
    }

    // Method to check if player data exists
    private bool PlayerDataExists()
    {
        return File.Exists(playerDataPath);
    }
}
