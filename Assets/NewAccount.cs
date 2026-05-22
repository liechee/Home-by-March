using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;

public class AccessTokenChecker : MonoBehaviour
{
    private const string LastAccessTokenKey = "LastAccessToken";
    private const string HasSeenLoginKey = "HasSeenLoginFor_";

    private async void Start()
    {
        DontDestroyOnLoad(gameObject); // Optional if using across scenes
        await InitializeAndCheckToken();
    }

    private async Task InitializeAndCheckToken()
    {
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync(); // or your login flow
        }

        string currentToken = AuthenticationService.Instance.AccessToken;
        string loginKeyForCurrentToken = HasSeenLoginKey + currentToken;
        bool hasSeenLogin = PlayerPrefs.GetInt(loginKeyForCurrentToken, 0) == 1;

        if (!hasSeenLogin)
        {
            Debug.Log("First time seeing login screen for this account. Clearing all data.");
            ClearAllLocalData();

            PlayerPrefs.SetString(LastAccessTokenKey, currentToken);
            PlayerPrefs.SetInt(loginKeyForCurrentToken, 1);
            PlayerPrefs.Save();

            SceneManager.LoadScene("Log In");
        }
        else
        {
            Debug.Log("Already seen login screen. Loading main game...");
            SceneManager.LoadScene("Main Screen"); // Replace with your main gameplay scene name
        }
    }

    void ClearAllLocalData()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        // Clear additional local data (files, cached saves, etc.) here if needed
        Debug.Log("All local data cleared.");
    }
}
