using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using UnityEngine.UI;
using TMPro;
using System;


public class IAPManager : MonoBehaviour
{
    private string coins50 = "coins50";
    private string coins100 = "coins100";
    private string coins150 = "coins150";
    private string coins1000 = "coins1000";

    [SerializeField]
    private ShopController _shopController;
    [SerializeField]
    private Button coins50Button;
    [SerializeField]
    private Button coins100Button;
    [SerializeField]
    private Button coins150Button;
    [SerializeField]
    private Button coins1000Button;


    void Start()
    {
        //    StartCoroutine(CheckInitialization());
    }

    // IEnumerator CheckInitialization()
    // {
    //     bool isInitialized = false;
    //     while(!isInitialized){
    //         if(CodelessIAPStoreListener.Instance != null){
    //             isInitialized = true;
    //             InitializePurchasing();
    //         }
    //     }
    // }



    // Reference to the ShopController

    // Called when a purchase is successfully completed
    public void OnPurchaseComplete(Product product)
    {

        int quantity = GetPurchasedQuantity(product);
        if (product.definition.id == coins50)
        {
            for (int i = 0; i < quantity; i++)
            {
                _shopController.Coin50Button();
            }
            // _shopController.Coin50Button();
        }
        else if (product.definition.id == coins100)
        {
            for (int i = 0; i < quantity; i++)
            {
                _shopController.Coin100Button();
            }

        }
        else if (product.definition.id == coins150)
        {
            for (int i = 0; i < quantity; i++)
            {
                _shopController.Coin150Button();
            }

        }
        else if (product.definition.id == coins1000)
        {
            for (int i = 0; i < quantity; i++)
            {
                _shopController.Coin1000Button();
            }

        }
        else
        {
            Debug.LogWarning($"Unrecognized product ID: {product.definition.id}");
        }
    }
    private int GetPurchasedQuantity(Product product)
    {
        int quantity = 1;

        if (product.hasReceipt)
        {
            var receipt = product.receipt;
            ReceiptData receiptData = JsonUtility.FromJson<ReceiptData>(receipt);
            if (receiptData.Store != "fake")
            {
                Payload payload = JsonUtility.FromJson<Payload>(receiptData.Payload);
                PaymentData paymentData = JsonUtility.FromJson<PaymentData>(payload.json);
                quantity = paymentData.quantity; // You can replace this with your own logic to get the purchased quantity
            }
        }
        return quantity; // You can replace this with your own logic to get the purchased quantity
    }

    // Called when a purchase fails
    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
    {
        Debug.Log($"Purchase failed for product: {product.definition.id}. Reason: {failureDescription.reason}");
    }
    public void OnProductFetched(Product product)
    {
        if (product.definition.id == coins50)
        {
            UpdateButtonPrice(coins50Button, product);
        }
        else if (product.definition.id == coins100)
        {
            UpdateButtonPrice(coins100Button, product);
        }
        else if (product.definition.id == coins150)
        {
            UpdateButtonPrice(coins150Button, product);
        }
        else if (product.definition.id == coins1000)
        {
            UpdateButtonPrice(coins1000Button, product);
        }

    }
    private void UpdateButtonPrice(Button button, Product product)
    {
        TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.text = product.metadata.localizedPriceString;
        }
    }

}

[Serializable]
public class ReceiptData
{
    public string Payload;
    public string Store;
    public string TransactionID;

}
[Serializable]
public class Payload
{
    public string json;
    public string signature;
    public PaymentData paymentData;
}

[Serializable]
public class PaymentData
{
    public string orderId;
    public string productId;
    public string packageName;
    public string purchaseToken;
    public int purchaseState;

    public int quantity;
    public bool acknowledged;

}
