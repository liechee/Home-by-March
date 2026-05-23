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
    public GameObject validationPanel;
    public TMP_Text validationMessageText;

    private const int MaxNameLength = 20;

    private bool IsAnySignedIn()
    {
        if (AuthManager1.Instance != null)
            return AuthManager1.Instance.IsSignedIn;

        return Unity.Services.Core.UnityServices.State ==
                   Unity.Services.Core.ServicesInitializationState.Initialized
               && AuthenticationService.Instance != null
               && AuthenticationService.Instance.IsSignedIn;
    }

    void Start(){
        if (inputField == null) return;

        HideValidationPanel();

        if (IsAnySignedIn())
        {
            inputField.text = string.IsNullOrEmpty(playerData != null ? playerData.playerName : "")
                ? "Signed-in name locked"
                : playerData.playerName;
            inputField.interactable = false;
        }
        else if (AuthManager1.Instance != null && AuthManager1.Instance.IsGuest)
        {
            inputField.text = AuthManager1.Instance.GuestName;
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
            ShowValidationPanel("Name change is locked while signed in.");
            Debug.LogWarning("[NameChangePanel] Name change blocked while signed in.");
            return;
        }

        if (!TryValidateName(inputField.text, out string newName, out string validationMessage))
        {
            ShowValidationPanel(validationMessage);
            return;
        }

        HideValidationPanel();

        if (AuthManager1.Instance != null && AuthManager1.Instance.IsGuest)
        {
            AuthManager1.Instance.SetGuestName(newName);
            Debug.Log($"Guest name changed to: {newName}");
        }
        else if (playerData != null)
        {
            playerData.ChangePlayerName(newName);
            Debug.Log($"Player name changed to: {newName}");
            playerData.SavePlayerData();
        }
    }

    public bool TryPrepareGuestFromInput(TMP_InputField sourceInput, out string validationMessage)
    {
        validationMessage = "";

        if (sourceInput == null)
        {
            validationMessage = "Name input is missing.";
            ShowValidationPanel(validationMessage);
            return false;
        }

        if (!TryValidateName(sourceInput.text, out string guestName, out validationMessage))
        {
            ShowValidationPanel(validationMessage);
            return false;
        }

        HideValidationPanel();

        if (AuthManager1.Instance != null)
            AuthManager1.Instance.SetGuestName(guestName);
        else
            PlayerPrefs.SetString(AuthManager1.PrefGuestName, guestName);

        PlayerPrefs.SetString(AuthManager1.PrefLoginMode, "Guest");
        PlayerPrefs.DeleteKey("HasLoggedOut");
        PlayerPrefs.Save();

        Debug.Log($"[NameChangePanel] Guest name prepared: '{guestName}'.");
        return true;
    }

    private void ShowValidationPanel(string message)
    {
        if (validationMessageText != null)
            validationMessageText.text = message;

        if (validationPanel != null)
            validationPanel.SetActive(true);
    }

    private void HideValidationPanel()
    {
        if (validationPanel != null)
            validationPanel.SetActive(false);

        if (validationMessageText != null)
            validationMessageText.text = "";
    }

    private bool TryValidateName(string rawName, out string normalizedName, out string validationMessage)
    {
        normalizedName = rawName != null ? rawName.Trim() : "";
        validationMessage = "";

        if (normalizedName.Length == 0)
        {
            validationMessage = "Name is required!";
            return false;
        }

        if (normalizedName.Length > MaxNameLength)
        {
            validationMessage = "Name too long!";
            return false;
        }

        return true;
    }
}
