using UnityEngine;
using System.Collections.Generic; // Include this namespace for List

public class DynamicInventoryDisplay : MonoBehaviour
{
    public InventoryHolder inventoryHolder;
    public InventorySlots slotPrefab;

    private List<InventorySlots> activeSlots = new List<InventorySlots>();

    public void onDisplay(bool display)
    {
        if (inventoryHolder == null || inventoryHolder.inventory == null)
        {
            Debug.LogError("InventoryHolder or inventory is not assigned!");
            return;
        }

        if (display)
        {
            ClearExistingSlots();

            for (int i = 0; i < inventoryHolder.inventory.itemlist.Length; i++)
            {
                InventorySlots slot = Instantiate(slotPrefab, transform);
                slot.onUpdateSlotDisplay(inventoryHolder.inventory.itemlist[i]);
                activeSlots.Add(slot);
            }
        }
        else
        {
            ClearExistingSlots();
        }
    }

    private void ClearExistingSlots()
    {
        foreach (var slot in activeSlots)
        {
            Destroy(slot.gameObject);
        }
        activeSlots.Clear();
    }
}
