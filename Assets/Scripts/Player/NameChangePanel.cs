using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Services.Authentication.PlayerAccounts.Samples;
using Unity.Services.Authentication;

public class NameChangePanel : MonoBehaviour
{
    public PlayerData playerData;
    public TMP_InputField inputField;

    private bool IsAnySignedIn()
    {
        if (AuthManager.Instance != null)
            return AuthManager.Instance.IsSignedIn;

        return Unity.Services.Core.UnityServices.State ==
                   Unity.Services.Core.ServicesInitializationState.Initialized
               && AuthenticationService.Instance != null
               && AuthenticationService.Instance.IsSignedIn;
    }

    void Start(){
        if (inputField == null) return;

        if (IsAnySignedIn())
        {
            inputField.text = string.IsNullOrEmpty(playerData != null ? playerData.playerName : "")
                ? "Signed-in name locked"
                : playerData.playerName;
            inputField.interactable = false;
        }
        else if (AuthManager.Instance != null && AuthManager.Instance.IsGuest)
        {
            inputField.text = AuthManager.Instance.GuestName;
            inputField.interactable = true;
        }
        else if (playerData != null)
        {
            inputField.text = playerData.playerName;
            inputField.interactable = true;
        }
    }

    public void ChangeName(){

        if (inputField == null) return;

        if (IsAnySignedIn())
        {
            Debug.LogWarning("[NameChangePanel] Name change blocked while signed in.");
            return;
        }

        string newName = inputField.text.Trim();

        if (newName.Length == 0)
        {
            inputField.text = "Name is required!";
            return;
        }

        if (newName.Length <= 20){
            if (AuthManager.Instance != null && AuthManager.Instance.IsGuest)
            {
                AuthManager.Instance.SetGuestName(newName);
                Debug.Log($"Guest name changed to: {newName}");
            }
            else if (playerData != null)
            {
                playerData.ChangePlayerName(newName);
                Debug.Log($"Player name changed to: {newName}");
                playerData.SavePlayerData();
            }
            return;
        } else {
            inputField.text = "Name too long!";
        }
    }
}
