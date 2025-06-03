using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RewardUI : MonoBehaviour
{
    public GameObject[] slots;
    public Button claimButtonPrefab;
    public Transform shopPanel;
    public List<RewardItem> shopItems;
    public GameObject itemDetailPanel;
    public TMP_Text itemDescriptionText;

    public TMP_Text itemName;
    public TMP_Text itemStats;
    public TMP_Text itemQuantityText;
    public Image itemImage;

    public InventoryObject inventory;
    public PlayerData playerData;

   // public GameObject confirmPurchasePanel;
    public Button confirmButton;
    public GameObject insufficientGoldPanel;
    private RewardItem currentShopItem;

    public delegate void ClaimResultHandler(bool success, string message);
    public event ClaimResultHandler OnClaimResult;

    public ResultHandler resultHandler;
    public GameObject chest;

    void Start()
    {
        if (shopItems == null || shopItems.Count == 0)
        {
            Debug.LogError("Shop items list is empty or not assigned!");
            return;
        }

        claimButtonPrefab.onClick.AddListener(OnClaimButtonClick);

        UpdateSlotImages();

        if (shopItems.Count > 0 && shopItems[0].item != null)
        {
            itemName.text = shopItems[0].item.name;
            itemDescriptionText.text = shopItems[0].item.description;
        }

        OnClaimResult += HandleClaimResult;
    }

    private void OnClaimButtonClick()
    {
        ClaimAllItems();
        Destroy(chest);
       // confirmPurchasePanel.SetActive(false);
    }

    private void ClaimAllItems()
    {
        bool allItemsClaimed = true;
        string resultMessage = "";

        foreach (var shopItem in shopItems)
        {
            if (shopItem != null && shopItem.item != null)
            {
                inventory.Load();

                Item _item = new Item(shopItem.item);

                if (inventory.AddItem(_item, shopItem.quantity))
                {
                    inventory.Save();
                    string itemMessage = "Claimed " + shopItem.quantity + " " + shopItem.item.name;
                    resultMessage += itemMessage + "\n";
                    Debug.Log(itemMessage);
                }
                else
                {
                    string itemMessage = "Failed to add item to inventory: " + shopItem.item.name;
                    resultMessage += itemMessage + "\n";
                    Debug.LogWarning(itemMessage);
                    allItemsClaimed = false;
                }
            }
            else
            {
                string itemMessage = "Shop item or item is null.";
                resultMessage += itemMessage + "\n";
                Debug.LogWarning(itemMessage);
                allItemsClaimed = false;
            }
        }

        UpdateSlotImages();
        OnClaimResult?.Invoke(allItemsClaimed, resultMessage.Trim());
    }

    private void UpdateSlotImages()
    {
        for (int i = 0; i < shopItems.Count; i++)
        {
            var shopItem = shopItems[i];
            var slot = slots[i];

            if (slot == null)
            {
                Debug.LogWarning("Slot is null at index: " + i);
                continue;
            }

            if (shopItem == null || shopItem.item == null || shopItem.item.uiDisplay == null)
            {
                Debug.LogWarning("ShopItem or item or item image is null at index: " + i);
                continue;
            }

            Transform childImage = slot.transform.Find("Image");
            if (childImage != null)
            {
                Image slotImage = childImage.GetComponent<Image>();
                if (slotImage != null)
                {
                    slotImage.sprite = shopItem.item.uiDisplay;
                    Debug.Log("Updated slot image at index: " + i + " with sprite: " + shopItem.item.uiDisplay.name);
                }
                else
                {
                    Debug.LogWarning("Slot image component is missing at index: " + i);
                }
            }
            else
            {
                Debug.LogError("Child object 'Image' not found under slot at index: " + i);
            }
        }
    }

    private void HandleClaimResult(bool success, string message)
    {
        if (resultHandler != null)
        {
            resultHandler.ShowResult(success ? shopItems[0].item.uiDisplay : null, message);
        }
    }
}