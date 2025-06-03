using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // For TextMeshPro
using Photon.Realtime; // Ensure the correct Player class is used
using Photon.Pun;

public class PlayerItem : MonoBehaviourPunCallbacks
{
    public TextMeshProUGUI playerName; // Use TextMeshProUGUI for TextMeshPro
    private Image backgroundImage;
    public Color highlightColor;
    public GameObject leftArrowButton;
    public GameObject rightArrowButton;

    Photon.Realtime.Player player;

    ExitGames.Client.Photon.Hashtable playerProperties = new ExitGames.Client.Photon.Hashtable();

    public Image PlayerAvatar;



    public Sprite[] avatars;

    

    private void Awake()
    {
        backgroundImage = GetComponent<Image>();
    }

    private void Start()
    {
        backgroundImage = this.GetComponent<Image>();
    }

    public void ApplyLocalChanges()
    {
        backgroundImage.color = highlightColor;

        leftArrowButton.SetActive(true);
        rightArrowButton.SetActive(true);
    }

    public void SetPlayerInfo(Photon.Realtime.Player _Player)
    {
        playerName.text = _Player.NickName; // Access the NickName property
        player = _Player;
        UpdatePlayerItem(player);
    }

    public void OnClickLeftArrow()
    {
        if (playerProperties == null)
        {
            playerProperties = new ExitGames.Client.Photon.Hashtable();
        }

        // Check if "playerAvatar" exists in playerProperties
        if (playerProperties.ContainsKey("playerAvatar"))
        {
            if ((int)playerProperties["playerAvatar"] == 0)
            {
                playerProperties["playerAvatar"] = avatars.Length - 1;
            }
            else
            {
                playerProperties["playerAvatar"] = (int)playerProperties["playerAvatar"] - 1;
            }
        }
        else
        {
            // If "playerAvatar" doesn't exist, initialize it with a default value
            playerProperties["playerAvatar"] = 0;
        }

        // Update player properties with Photon
        PhotonNetwork.SetPlayerCustomProperties(playerProperties);
    }


    public void OnClickRightArrow()
    {
        // Ensure playerProperties is initialized
        if (playerProperties == null)
        {
            playerProperties = new ExitGames.Client.Photon.Hashtable();
        }

        // Check if the "playerAvatar" key exists in playerProperties
        if (playerProperties.ContainsKey("playerAvatar"))
        {
            if ((int)playerProperties["playerAvatar"] == avatars.Length - 1)
            {
                playerProperties["playerAvatar"] = 0;
            }
            else
            {
                playerProperties["playerAvatar"] = (int)playerProperties["playerAvatar"] + 1;
            }
        }
        else
        {
            // If "playerAvatar" does not exist, initialize it with a default value (0)
            playerProperties["playerAvatar"] = 0;
        }

        // Set the player properties to Photon
        PhotonNetwork.SetPlayerCustomProperties(playerProperties);
    }

    public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)

    {
        if (player == targetPlayer)
        {
            UpdatePlayerItem(targetPlayer);
        }
    }

    void UpdatePlayerItem(Photon.Realtime.Player _Player)
    {
        // Check if the player has the "playerAvatar" property set
        if (_Player.CustomProperties.ContainsKey("playerAvatar"))
        {
            // Get the value of the "playerAvatar" property
            int avatarIndex = (int)_Player.CustomProperties["playerAvatar"];
            PlayerAvatar.sprite = avatars[avatarIndex];
        }
        else
        {
            // If the "playerAvatar" property does not exist, set the default avatar
            _Player.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "playerAvatar", 0 } });
        }
    }

    // private void Update()
    // {
    //     if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount >= 2) 
    //     {
    //         playButton.SetActive(true);
    //     }
    //     else
    //     {
    //         playButton.SetActive(false);
    //     }
    // }

    // public void OnClickPlayButton_Click(object sender, RoutedEventArgs e)
    // {
    //     PhotonNetwork.LoadLevel(sceneName);
    // }

    
}