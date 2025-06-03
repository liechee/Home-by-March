using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;
using TMPro;
using System.Text;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    public GameObject lobbyPanel;
    public GameObject roomPanel;
    public GameObject loadingPanel;

    public TextMeshProUGUI roomNameText;

    public RoomItem roomItemPrefab;

    private List<RoomItem> roomItems = new List<RoomItem>();

    public Transform contentObject;

    public float timeBetweenRoomUpdates = 1.5f;

    private float nextUpdateTime;

    public List<PlayerItem> playerItemsList = new List<PlayerItem>();

    public PlayerItem playerItemPrefab;
    public GameObject fullRoomPanel;
    public GameObject errorPanel;
    public TextMeshProUGUI errorText;

    public Transform playerItemParent;
    public TMP_InputField joinInput;


    public TextMeshProUGUI titleTextMeshPro;
    public TextMeshProUGUI descriptionTextMeshPro;
    public Image targetImage;

    public string sceneName;

    public GameObject playButton;


    private void Start()
    {
        PhotonNetwork.JoinLobby();
    }

    public void JoinRoom()
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            PhotonNetwork.JoinRoom(joinInput.text);
        }

    }


    public void OnClickCreate()
    {
        string roomName = GenerateRandomRoomName(6);
        PhotonNetwork.CreateRoom(roomName, new RoomOptions { MaxPlayers = 2, IsVisible = true, IsOpen = true, BroadcastPropsChangeToAll = true }, null);
    }

    public override void OnJoinedRoom()
    {
        lobbyPanel.SetActive(false);
        roomPanel.SetActive(true);
        roomNameText.text = PhotonNetwork.CurrentRoom.Name;
        UpdatePlayerList();
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        UpdatePlayerList();
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        UpdatePlayerList();
    }



    private string GenerateRandomRoomName(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        StringBuilder result = new StringBuilder(length);
        System.Random random = new System.Random(System.DateTime.Now.Millisecond);
        for (int i = 0; i < length; i++)
        {
            result.Append(chars[random.Next(chars.Length)]);
        }
        return result.ToString();
    }

    public void OnClickLeaveRoom()
    {
        PhotonNetwork.LeaveRoom();
        lobbyPanel.SetActive(true);
        roomPanel.SetActive(false);
    }

    public override void OnLeftRoom()
    {
        roomPanel.SetActive(false);
        lobbyPanel.SetActive(true);
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinLobby();
    }

    void UpdatePlayerList()
    {
        foreach (PlayerItem item in playerItemsList)
        {
            Destroy(item.gameObject);
        }
        playerItemsList.Clear();

        if (PhotonNetwork.CurrentRoom == null)
        {
            return;
        }

        foreach (KeyValuePair<int, Photon.Realtime.Player> player in PhotonNetwork.CurrentRoom.Players)
        {
            PlayerItem item = Instantiate(playerItemPrefab, playerItemParent);
            playerItemsList.Add(item);

            if (player.Value == PhotonNetwork.LocalPlayer)
            {
                item.ApplyLocalChanges();
            }
            item.SetPlayerInfo(player.Value);
        }
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        ShowErrorPanel($"Failed to create room: {message}");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        loadingPanel.SetActive(false);

        if (returnCode == ErrorCode.GameFull)
        {
            fullRoomPanel.SetActive(true);
        }
        else
        {
            ShowErrorPanel($"Failed to join room: {message}");
        }
    }

    // public override void OnDisconnected(DisconnectCause cause)
    // {
    //     Debug.LogError($"Disconnected from Photon: {cause}");
  
    // }

    private void ShowErrorPanel(string message)
    {
        errorPanel.SetActive(true);
        errorText.text = message;
    }

    private void Update()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            playButton.SetActive(true);
        }
        else
        {
            playButton.SetActive(false);
        }



    }
    public void OnClickPlayButtonClick()
    {
        Debug.Log(sceneName);
        PhotonNetwork.LoadLevel(sceneName);

    }

    public void SetsceneName(string name)
    {
        sceneName = name;
    }
}