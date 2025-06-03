using System.Collections;
using System.Collections.Generic;
using System.IO; // For file handling
using UnityEngine;

public class InventoryHolder : MonoBehaviour
{
    public Inventorys inventory;
    public Transform display;

    private int returnAmount;

    [Header("Testing")]
    public Items item;
    public int stack = 32;

    private void Update()
    {
        // Testing inventory interactions (Optional)
    }

    public void UpdateUISlot(int index, ItemData data)
    {
        if (display.childCount <= index || index < 0)
        {
            Debug.LogWarning($"Invalid slot index: {index}");
            return;
        }

        InventorySlots slot = display.GetChild(index).GetComponent<InventorySlots>();
        slot.onUpdateSlotDisplay(data);
    }

    public ItemData RemoveItem(int index, bool isFullStack)
    {
        if (index < 0 || index >= inventory.itemlist.Length || inventory.itemlist[index] == null)
        {
            Debug.LogWarning($"Invalid or empty slot at index: {index}");
            return null;
        }

        ItemData currentSelectedSlot = inventory.itemlist[index];
        ItemData tempData = new ItemData
        {
            items = currentSelectedSlot.items,
            stack = currentSelectedSlot.stack
        };

        if (isFullStack)
        {
            inventory.itemlist[index] = null;
        }
        else
        {
            int halfStack = Mathf.RoundToInt(currentSelectedSlot.stack / 2);
            tempData.stack = halfStack;
            currentSelectedSlot.stack -= halfStack;
        }

        UpdateUISlot(index, inventory.itemlist[index]);
        return tempData;
    }

    public bool AddItem(ItemData itemDataToAdd)
    {
        for (int i = 0; i < inventory.itemlist.Length; i++)
        {
            ItemData currentSelectedSlot = inventory.itemlist[i];

            if (currentSelectedSlot != null && currentSelectedSlot.items == itemDataToAdd.items && currentSelectedSlot.stack < currentSelectedSlot.items.maxStackSize)
            {
                int totalCount = currentSelectedSlot.stack + itemDataToAdd.stack;

                if (totalCount <= currentSelectedSlot.items.maxStackSize)
                {
                    currentSelectedSlot.stack = totalCount;
                }
                else
                {
                    int excess = totalCount - currentSelectedSlot.items.maxStackSize;
                    currentSelectedSlot.stack = currentSelectedSlot.items.maxStackSize;

                    itemDataToAdd.stack = excess;
                    return AddItem(itemDataToAdd); // Try adding the remaining items
                }

                UpdateUISlot(i, currentSelectedSlot);
                return true;
            }
            else if (currentSelectedSlot == null)
            {
                inventory.itemlist[i] = new ItemData
                {
                    items = itemDataToAdd.items,
                    stack = itemDataToAdd.stack
                };

                UpdateUISlot(i, inventory.itemlist[i]);
                return true;
            }
        }

        Debug.LogWarning("Inventorys is full or unable to add item.");
        return false;
    }

    #region Optimized Save and Load Inventorys
    public void SaveInventory()
    {
        if (inventory == null || inventory.itemlist == null)
        {
            Debug.LogError("Inventorys is null or not initialized!");
            return;
        }

        // Prepare a list of non-empty slots
        List<InventorySlotData> slotDataList = new List<InventorySlotData>();

        for (int i = 0; i < inventory.itemlist.Length; i++)
        {
            if (inventory.itemlist[i] != null && inventory.itemlist[i].items != null)
            {
                slotDataList.Add(new InventorySlotData
                {
                    index = i,
                    itemData = inventory.itemlist[i]
                    
                });
                Debug.Log("item saved: " + inventory.itemlist[i]);
            }
        }

        // Serialize the non-empty slot data
        string json = JsonUtility.ToJson(new InventoryContainer { slots = slotDataList }, true);

        // Save the JSON to a file
        string path = Path.Combine(Application.persistentDataPath, "optimized_inventory.json");
        File.WriteAllText(path, json);

        Debug.Log($"Inventorys saved to {path}");
    }

    public void LoadInventory()
    {
        string path = Path.Combine(Application.persistentDataPath, "optimized_inventory.json");

        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            InventoryContainer container = JsonUtility.FromJson<InventoryContainer>(json);

            // Clear current inventory
            for (int i = 0; i < inventory.itemlist.Length; i++)
            {
                inventory.itemlist[i] = null; // Reset the slot
                UpdateUISlot(i, null);       // Clear the UI for the slot
            }

            // Populate the inventory with saved data
            foreach (var slotData in container.slots)
            {
                if (slotData.index >= 0 && slotData.index < inventory.itemlist.Length)
                {
                    if (slotData.itemData != null && slotData.itemData.stack > 0)
                    {
                        inventory.itemlist[slotData.index] = slotData.itemData;
                        UpdateUISlot(slotData.index, slotData.itemData);
                    }
                    else
                    {
                        Debug.LogWarning($"Skipping slot {slotData.index} due to invalid or zero stack.");
                    }
                }
                else
                {
                    Debug.LogWarning($"Invalid slot index in saved data: {slotData.index}");
                }
            }

            Debug.Log("Inventorys loaded and UI updated successfully.");
        }
        else
        {
            Debug.LogWarning("No inventory file found!");
        }
    }


    [System.Serializable]
    private class InventoryContainer
    {
        public List<InventorySlotData> slots;
    }

    [System.Serializable]
    private class InventorySlotData
    {
        public int index;
        public ItemData itemData;
    }
    #endregion

}
