// using UnityEngine;

// public class InventoryBag : MonoBehaviour
// {
//     public GameObject inventorySlotPrefab;
//     public Transform inventoryContent;

//     private void Start()
//     {
//         // Update UI when the game starts
//         InventoryManager.Instance.LoadInventory();
//         UpdateInventoryUI();
//     }

//     public void UpdateInventoryUI()
//     {
//         foreach (Transform child in inventoryContent)
//         {
//             Destroy(child.gameObject);
//         }

//         foreach (var item in InventoryManager.Instance.playerInventory)
//         {
//             GameObject slot = Instantiate(inventorySlotPrefab, inventoryContent);
//             slot.GetComponent<UnityEngine.UI.Image>().sprite = item.itemIcon;
//             slot.GetComponentInChildren<UnityEngine.UI.Text>().text = item.itemName;
//         }
//     }
// }
