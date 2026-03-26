using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // Import TextMeshPro namespace
using Photon.Pun;
using Photon.Realtime; // Import Photon.Realtime namespace for error handling

public class CreateAndJoinRoom : MonoBehaviourPunCallbacks
{
    public TMP_InputField createInput; // Use TMP_InputField instead of InputField
    public TMP_InputField joinInput;   // Use TMP_InputField instead of InputField
    // public Button createRoomButton;
    // public Button joinRoomButton;

    // Start is called before the first frame update
    public void CreateRoom()
    {
        Debug.Log("Creating room: " + createInput.text);
        PhotonNetwork.CreateRoom(createInput.text);
    }

    public void JoinRoom()
    {
        Debug.Log("Joining room: " + joinInput.text);
        PhotonNetwork.JoinRoom(joinInput.text);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Joined room successfully.");
        PhotonNetwork.LoadLevel("CoopSample");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError("Failed to join room: " + message);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError("Failed to create room: " + message);
    }
}