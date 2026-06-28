using UnityEngine;
using TMPro;

// Simple component to display the player's saved name on the main menu.
public class MainMenuDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;

    private void OnEnable()
    {
        UpdatePlayerNameDisplay();
    }

    public void UpdatePlayerNameDisplay()
    {
        // Prefer signed-in account name if present
        string signedIn = PlayerPrefs.GetString("LastSignedInPlayer", "");
        string guest = PlayerPrefs.GetString("LastGuestUsername", "");

        string displayName = !string.IsNullOrEmpty(signedIn) ? signedIn : guest;

        if (playerNameText != null)
        {
            playerNameText.text = string.IsNullOrEmpty(displayName) ? "Player" : displayName;
        }
    }
}
