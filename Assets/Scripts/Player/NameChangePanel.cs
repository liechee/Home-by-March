using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NameChangePanel : MonoBehaviour
{
    public PlayerData playerData;
    public TMP_InputField inputField;

    void Start(){
        inputField.text = playerData.playerName;
    }

    public void ChangeName(){

        if (inputField.text.Length <= 20){
            playerData.ChangePlayerName(inputField.text);
            Debug.Log($"Player name changed to: {inputField.text}");
            playerData.SavePlayerData();
        } else {
            inputField.text = "Name too long!";
        }
    }
}
