using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class RoomItem : MonoBehaviour
{

    public TextMeshProUGUI roomNameText;
    // Start is called before the first frame update
    LobbyManager manger;

    private void Start()
    {
        manger = FindObjectOfType<LobbyManager>();
    }

    public void OnClickitem()
    {
        manger.JoinRoom();
    }

    public void SetRoomName(string name)
    {
        roomNameText.text = name;
    }

   
}
