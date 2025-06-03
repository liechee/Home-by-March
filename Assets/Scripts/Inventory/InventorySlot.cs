using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class InventorySlots : MonoBehaviour, IPointerDownHandler
{
    public Image icon;
    public TMP_Text counter;
    private ItemData data;

    public void onUpdateSlotDisplay(ItemData desiredData)
    {
        data = desiredData;
        if (desiredData == null || desiredData.items == null)
        {
            icon.sprite = null;
            icon.color = Color.clear;
            counter.text = "";
        }
        else
        {
            icon.sprite = desiredData.items.icon;
            icon.color = Color.white;
            counter.text = desiredData.stack > 1 ? desiredData.stack.ToString() : "";
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (GameManagers.instance == null || GameManagers.instance.cursorTooltip == null)
        {
            Debug.LogWarning("GameManagers or cursorTooltip is not initialized.");
            return;
        }

        ItemData tooltipData = GameManagers.instance.cursorTooltip.slot;
        ItemData[] parentInventory = GetComponentInParent<DynamicInventoryDisplay>().inventoryHolder.inventory.itemlist;

        if (tooltipData == null || data == null)
        {
            Debug.LogWarning("Tooltip or slot data is null.");
            return;
        }

        // Handle picking up or swapping items
        if (tooltipData.items == null && data.items != null)
        {
            // Pick up item
            GameManagers.instance.cursorTooltip.UpdateSlot(data, parentInventory, transform.GetSiblingIndex());
            ClearThisSlot();
        }
        else if (data.items == null)
        {
            // Place item in an empty slot
            data = tooltipData;
            onUpdateSlotDisplay(data);
            GameManagers.instance.cursorTooltip.ClearThisSlot();
        }
        else if (data.items == tooltipData.items)
        {
            // Combine stacks or handle overflow
            int total = data.stack + tooltipData.stack;
            if (total > data.items.maxStackSize)
            {
                tooltipData.stack = total - data.items.maxStackSize;
                data.stack = data.items.maxStackSize;
                GameManagers.instance.cursorTooltip.UpdateSlot(tooltipData, parentInventory, transform.GetSiblingIndex());
            }
            else
            {
                data.stack = total;
                GameManagers.instance.cursorTooltip.ClearThisSlot();
            }
            onUpdateSlotDisplay(data);
        }
        else
        {
            // Swap items
            SwapItems(parentInventory);
        }
    }

    public void ClearThisSlot()
    {
        data = new ItemData();
        onUpdateSlotDisplay(data);
    }

    public void SwapItems(ItemData[] items)
    {
        ItemData tempItem = GameManagers.instance.cursorTooltip.slot;
        GameManagers.instance.cursorTooltip.UpdateSlot(data, items, transform.GetSiblingIndex());
        data = tempItem;
        onUpdateSlotDisplay(data);
    }
}
