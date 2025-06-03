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

    public void changeName(){

        if (inputField.text.Length <= 20){
        playerData.playerName = inputField.GetComponent<TMP_InputField>().text;
        playerData.SavePlayerData();
        } else {
            inputField.GetComponent<TMP_InputField>().text = "Name too long!";
        }
    }
}
