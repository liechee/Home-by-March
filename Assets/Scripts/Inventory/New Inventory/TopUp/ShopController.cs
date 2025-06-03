using TMPro;
using UnityEngine;

public class ShopController : MonoBehaviour
{
    [SerializeField] private TMP_Text coinText; // Reference to the coin text UI
    [SerializeField] private PlayerData playerData; // Reference to the PlayerData instance

    // Method to add 50 coins
    public void Coin50Button()
    {
        AddGold(100);
    }

    // Method to add 100 coins
    public void Coin100Button()
    {
        AddGold(250);
    }

    // Method to add 150 coins
    public void Coin150Button()
    {
        AddGold(500);
    }

    // Method to add 1000 coins
    public void Coin1000Button()
    {
        AddGold(1000);
    }

    // Private helper method to add gold
    private void AddGold(int goldAmount)
    {
        playerData.AddGold(goldAmount); // Use the AddGold method from PlayerData
        UpdateCoinText();
    }

    // Method to update the coin text UI
    private void UpdateCoinText()
    {
        coinText.text = playerData.gold.ToString();
    }
}