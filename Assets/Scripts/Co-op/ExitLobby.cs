using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun; // Import Photon namespace

public class ExitLobby : MonoBehaviour
{
    // Method to change the scene
    public void OnExitLobby(string sceneName)
    {
        // Check if Photon is connected
        if (PhotonNetwork.IsConnected)
        {
            // Disconnect from Photon and wait before loading the scene
            PhotonNetwork.Disconnect();
            StartCoroutine(WaitForDisconnect(sceneName));
        }
        else
        {
            // Directly load the scene if not connected to Photon
            SceneManager.LoadScene(sceneName);
        }
    }

    // Coroutine to wait until Photon is fully disconnected
    private System.Collections.IEnumerator WaitForDisconnect(string sceneName)
    {
        while (PhotonNetwork.IsConnected)
        {
            yield return null; // Wait for the next frame
        }
        // Load the scene after disconnecting
        SceneManager.LoadScene(sceneName);
    }
}
