// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class InventoryManager : MonoBehaviour
// {
//     public static InventoryManager Instance; // Singleton instance

//     public List<Items> allItems; // List of all available items in the game

//     private void Awake()
//     {
//         // Ensure only one instance of InventoryManager exists
//         if (Instance == null)
//         {
//             Instance = this;
//         }
//         else
//         {
//             Destroy(gameObject);
//             return;
//         }

//         DontDestroyOnLoad(gameObject); // Persist across scenes
//     }

//     public Items GetItemByID(int itemID)
//     {
//         // Find and return the item with the specified itemID
//         return allItems.Find(item => item.itemID == itemID);
//     }

//     public void AddNewItem(Items newItem)
//     {
//         if (!allItems.Contains(newItem))
//         {
//             allItems.Add(newItem);
//         }
//     }
// }
