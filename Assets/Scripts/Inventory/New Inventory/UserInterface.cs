using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;



public abstract class UserInterface : MonoBehaviour
{
    public InventoryObject inventory;
    public Dictionary<GameObject, InventorySlot> slotsOnInterface = new Dictionary<GameObject, InventorySlot>();
    public Canvas uiCanvas;

    public void Start()
    {
        for (int i = 0; i < inventory.GetSlots.Length; i++)
        {
            inventory.GetSlots[i].parent = this;
            inventory.GetSlots[i].OnAfterUpdate += OnSlotUpdate;
        }
        CreateSlots();
        AddEvent(gameObject, EventTriggerType.PointerEnter, delegate { OnEnterInterface(gameObject); });
        AddEvent(gameObject, EventTriggerType.PointerExit, delegate { OnExitInterface(gameObject); });

        // Adding the click event for each slot
        foreach (var slot in slotsOnInterface.Keys)
        {
            AddEvent(slot, EventTriggerType.PointerClick, delegate { OnSlotClick(slot); });
        }
    }

    // private void OnSlotUpdate(InventorySlot _slot)
    // {
    //     if (_slot.item.Id >= 0)
    //     {
    //         _slot.slotDisplay.transform.GetChild(0).GetComponentInChildren<Image>().sprite = _slot.ItemObject.uiDisplay;
    //         _slot.slotDisplay.transform.GetChild(0).GetComponentInChildren<Image>().color = new Color(1, 1, 1, 1);
    //         _slot.slotDisplay.GetComponentInChildren<TextMeshProUGUI>().text = _slot.amount == 1 ? "" : _slot.amount.ToString("n0");
    //     }
    //     else
    //     {
    //         _slot.slotDisplay.transform.GetChild(0).GetComponentInChildren<Image>().sprite = null;
    //         _slot.slotDisplay.transform.GetChild(0).GetComponentInChildren<Image>().color = new Color(1, 1, 1, 0);
    //         _slot.slotDisplay.GetComponentInChildren<TextMeshProUGUI>().text = "";
    //     }
    // }
    private void OnSlotUpdate(InventorySlot _slot)
{
    if (_slot == null || _slot.slotDisplay == null)
    {
        return;
    }

    Image img = _slot.slotDisplay.transform.GetChild(0).GetComponentInChildren<Image>();
    TextMeshProUGUI text = _slot.slotDisplay.GetComponentInChildren<TextMeshProUGUI>();

    if (_slot.item.Id >= 0)
    {
        if (img != null)
        {
            img.sprite = _slot.ItemObject.uiDisplay;
            img.color = new Color(1, 1, 1, 1);
        }

        if (text != null)
        {
            text.text = _slot.amount == 1 ? "" : _slot.amount.ToString("n0");
        }
    }
    else
    {
        if (img != null)
        {
            img.sprite = null;
            img.color = new Color(1, 1, 1, 0);
        }

        if (text != null)
        {
            text.text = "";
        }
    }
}


    public abstract void CreateSlots();

    protected void AddEvent(GameObject obj, EventTriggerType type, UnityAction<BaseEventData> action)
    {
        EventTrigger trigger = obj.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = obj.AddComponent<EventTrigger>();

        var eventTrigger = new EventTrigger.Entry();
        eventTrigger.eventID = type;
        eventTrigger.callback.AddListener(action);
        trigger.triggers.Add(eventTrigger);
    }

    public GameObject itemDetailPanel;
    public TMP_Text itemDescriptionText;
    public TMP_Text itemSkillDescriptionText;
    public TMP_Text itemName;
    public TMP_Text itemSkillName;
    public TMP_Text itemStats;
    public TMP_Text itemPriceText;
    public Image itemImage;
    public Image itemSkillImage;
    public GameObject skillInfo;

    // Handle clicking on a slot
    public void OnSlotClick(GameObject obj)
    {
        // Check if the slot is in the interface and if it has an item
        if (slotsOnInterface.ContainsKey(obj))
        {
            var inventorySlot = slotsOnInterface[obj];
            Debug.Log("Slot Clicked: " + obj.name);

            if (inventorySlot?.ItemObject != null)
            {
                itemDetailPanel.SetActive(true);

                itemName.text = inventorySlot.ItemObject.name;
                itemDescriptionText.text = inventorySlot.ItemObject.description;
                itemImage.sprite = inventorySlot.ItemObject.uiDisplay;
                if (inventorySlot.ItemObject.data.buffs != null && inventorySlot.ItemObject.data.buffs.Length > 0)
                {
                    foreach (var buff in inventorySlot.ItemObject.data.buffs)
                    {
                        if (itemStats != null)
                            itemStats.text = $"Attribute: {buff.attribute} Value: {buff.value}";
                        else
                            Debug.LogError("itemStats is not assigned!");
                    }
                }
                if (inventorySlot.ItemObject.skillData == null)
                {
                    skillInfo.SetActive(false);
                }
                else if (inventorySlot.ItemObject.skillData is SpellStrategy spell)
                {
                    skillInfo.SetActive(true);
                    itemSkillDescriptionText.text = $"{spell.description}";
                    itemSkillImage.sprite = spell.uiDisplay;
                    itemSkillName.text = spell.spellName;
                }

                if (inventorySlot.ItemObject.data != null)
                {
                    var data = inventorySlot.ItemObject.data;
                    Debug.Log("Item Data:");
                    Debug.Log("  Name: " + data.Name);
                    Debug.Log("  ID: " + data.Id);

                    foreach (var buff in data.buffs)
                    {
                        Debug.Log($"  Buff: Attribute = {buff.attribute}, Value = {buff.value}, Min = {buff.min}, Max = {buff.max}");
                    }
                }
                else
                {
                    Debug.LogError("Item data is null!");
                }
            }

        }
    }

    public void OnEnter(GameObject obj)
    {
        MouseData.slotHoveredOver = obj;
        if (slotsOnInterface.ContainsKey(obj) && slotsOnInterface[obj].ItemObject != null)
        {
            var itemObject = slotsOnInterface[obj].ItemObject;
            Debug.Log($"ItemObject Entered: Description = {itemObject.description}, Type = {itemObject.type}, Stackable = {itemObject.stackable}");

            var itemData = itemObject.data;
            Debug.Log($"Item Data: Name = {itemData.Name}, Id = {itemData.Id}");

            foreach (var buff in itemData.buffs)
            {
                Debug.Log($"Buff: Attribute = {buff.attribute}, Value = {buff.value}, Min = {buff.min}, Max = {buff.max}");
            }
        }
    }

    public void OnExit(GameObject obj)
    {
        MouseData.slotHoveredOver = null;
    }

    public void OnEnterInterface(GameObject obj)
    {
        MouseData.interfaceMouseIsOver = obj.GetComponent<UserInterface>();
    }

    public void OnExitInterface(GameObject obj)
    {
        MouseData.interfaceMouseIsOver = null;
    }

    public void OnDragStart(GameObject obj)
    {
        MouseData.tempItemBeingDragged = CreateTempItem(obj);
    }

    public GameObject CreateTempItem(GameObject obj)
    {
        GameObject tempItem = null;
        if (slotsOnInterface[obj].item.Id >= 0)
        {
            tempItem = new GameObject();
            var rt = tempItem.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(50, 50);
            tempItem.transform.SetParent(transform.parent);
            var img = tempItem.AddComponent<Image>();
            img.sprite = slotsOnInterface[obj].ItemObject.uiDisplay;
            img.raycastTarget = false;
        }
        return tempItem;
    }

    public void OnDragEnd(GameObject obj)
    {
        Destroy(MouseData.tempItemBeingDragged);

        // If no interface is hovered over, return the item to its original slot
        if (MouseData.interfaceMouseIsOver == null || MouseData.slotHoveredOver == null)
        {
            Debug.Log("Drag ended outside inventory or equipment. Returning item to original slot.");
            return;
        }

        // Handle item swapping
        InventorySlot mouseHoverSlotData = MouseData.interfaceMouseIsOver.slotsOnInterface[MouseData.slotHoveredOver];
        inventory.SwapItems(slotsOnInterface[obj], mouseHoverSlotData);
    }

    public void OnDrag(GameObject obj)
    {
        if (MouseData.tempItemBeingDragged != null)
        {
            RectTransform tempItemRect = MouseData.tempItemBeingDragged.GetComponent<RectTransform>();

            if (uiCanvas.renderMode == RenderMode.ScreenSpaceCamera && uiCanvas.worldCamera != null)
            {
                // For Screen Space - Camera
                Vector3 mousePosition = Input.mousePosition;
                Vector3 worldPosition = uiCanvas.worldCamera.ScreenToWorldPoint(new Vector3(
                    mousePosition.x,
                    mousePosition.y,
                    uiCanvas.planeDistance // Use plane distance for proper depth calculation
                ));

                tempItemRect.position = worldPosition;

                // Set smaller size for Screen Space - Camera
                tempItemRect.sizeDelta = new Vector2(5, 5); // Adjust this size as needed
            }
            else
            {
                // For Screen Space - Overlay or other render modes
                Vector3 mousePosition = Input.mousePosition;
                tempItemRect.position = mousePosition;

                // Set larger size for Screen Space - Overlay
                tempItemRect.sizeDelta = new Vector2(200, 200); // Adjust this size as needed

                // Ensure the dragged item is under the UI canvas
                if (tempItemRect.parent == null || tempItemRect.parent != uiCanvas.transform)
                {
                    tempItemRect.SetParent(uiCanvas.transform, false);
                }
            }
        }
    }



}

public static class MouseData
{
    public static UserInterface interfaceMouseIsOver;
    public static GameObject tempItemBeingDragged;
    public static GameObject slotHoveredOver;
}



public static class ExtensionMethods
{
    public static void UpdateSlotDisplay(this Dictionary<GameObject, InventorySlot> _slotsOnInterface)
    {
        foreach (KeyValuePair<GameObject, InventorySlot> _slot in _slotsOnInterface)
        {
            if (_slot.Value.item.Id >= 0)
            {
                _slot.Key.transform.GetChild(0).GetComponentInChildren<Image>().sprite = _slot.Value.ItemObject.uiDisplay;
                _slot.Key.transform.GetChild(0).GetComponentInChildren<Image>().color = new Color(1, 1, 1, 1);
                _slot.Key.GetComponentInChildren<TextMeshProUGUI>().text = _slot.Value.amount == 1 ? "" : _slot.Value.amount.ToString("n0");
            }
            else
            {
                _slot.Key.transform.GetChild(0).GetComponentInChildren<Image>().sprite = null;
                _slot.Key.transform.GetChild(0).GetComponentInChildren<Image>().color = new Color(1, 1, 1, 0);
                _slot.Key.GetComponentInChildren<TextMeshProUGUI>().text = "";
            }
        }
    }
}