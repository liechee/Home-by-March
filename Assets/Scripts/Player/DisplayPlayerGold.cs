using UnityEngine;
using TMPro;

public class DisplayPlayerGold : MonoBehaviour
{
    public TextMeshProUGUI goldText; // Reference to the TextMeshProUGUI component
    public PlayerData playerData;    // Reference to the PlayerData script

    void Start()
    {
        if (goldText == null)
        {
            Debug.LogError("Gold TextMeshProUGUI is not assigned!");
        }

        if (playerData == null)
        {
            Debug.LogError("PlayerData script is not assigned!");
        }

        UpdateGoldDisplay(); // Initialize the display
    }

    void Update()
    {
        UpdateGoldDisplay(); // Continuously update the display
    }

    // Updates the text to reflect the player's current gold
    void UpdateGoldDisplay()
    {
        if (goldText != null && playerData != null)
        {
            goldText.text = playerData.gold.ToString();
        }
    }
}
