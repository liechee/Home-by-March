// using UnityEngine;
// using System.Threading.Tasks;

// public class InventoryCloudManager : MonoBehaviour
// {
//     public InventoryObject inventoryObject;
//     public string cloudFileName = "inventory_cloud_save";

//     // Call this to save inventory to the cloud
//     public async Task SaveInventory()
//     {
//         if (inventoryObject != null)
//             await inventoryObject.SaveInventoryToCloud(cloudFileName);
//         else
//             Debug.LogWarning("InventoryObject reference is missing!");
//     }

//     // Call this to load inventory from the cloud
//     public async Task LoadInventory()
//     {
//         if (inventoryObject != null)
//             await inventoryObject.LoadInventoryFromCloud(cloudFileName);
//         else
//             Debug.LogWarning("InventoryObject reference is missing!");
//     }

// }