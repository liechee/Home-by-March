using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using TMPro;

public class ConnectToServer : MonoBehaviourPunCallbacks
{
    public PlayerData playerData;
    public GameObject noInternetPanel; // Assign this in the Inspector
    public TextMeshProUGUI errorMessageText; // Assign this in the Inspector

    // Start is called before the first frame update
    void Start()
    {
        CheckInternetAndConnect();
    }

    private void CheckInternetAndConnect()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            ShowErrorMessage("No internet connection detected. Please check your connection and try again.");
        }
        else
        {
            Debug.Log("Internet connection available. Connecting to Photon server in the Asia region...");
            ConnectToPhoton();
        }
    }

    private void ConnectToPhoton()
    {
        try
        {
            PhotonNetwork.NickName = playerData.playerName;
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = "asia"; // Specify Asia region
            PhotonNetwork.AutomaticallySyncScene = true;
            PhotonNetwork.ConnectUsingSettings();
        }
        catch (System.Exception ex)
        {
            ShowErrorMessage("An error occurred while connecting to the server: " + ex.Message);
        }
    }

    public void OnClickRetryConnection()
    {
        noInternetPanel.SetActive(false); // Hide the panel
        errorMessageText.text = ""; // Clear the error message
        CheckInternetAndConnect(); // Retry the connection
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to the Master server in Asia.");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Lobby.");
        Debug.Log("Player Name: " + playerData.playerName);
        SceneManager.LoadScene("Lobby");
    }

    public override void OnDisconnected(Photon.Realtime.DisconnectCause cause)
    {
        ShowErrorMessage("Disconnected from the server. Reason: " + cause);
    }

    private void ShowErrorMessage(string message)
    {
        Debug.LogWarning(message);
        noInternetPanel.SetActive(true); // Show the panel
        errorMessageText.text = message; // Display the error message
    }
}
