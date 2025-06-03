using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;


public class EquipmentSlot : MonoBehaviour, IPointerDownHandler
{
    public EquipmentType _slotType;
    public Image icon;
    public TMP_Text counter;
    private ItemData data;

    public void onUpdateSlotDisplay(ItemData desiredData)
    {
        data = desiredData;
        if (desiredData.items == null)
        {
            icon.sprite = null;
            icon.color = Color.clear;
            counter.text = "";
        }
        else
        {
            icon.sprite = desiredData.items.icon;
            icon.color = Color.white;

            if (desiredData.stack > 1) counter.text = desiredData.stack.ToString();
            else counter.text = "";
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Check for a touch or left mouse click
        if (Input.touchCount > 0 || Input.GetMouseButtonDown(0))
        {
            ItemData tooltipData = GameManagers.instance.cursorTooltip.slot;
            ItemData[] parentInventory = GetComponentInParent<EquipmentDisplay>().equipParent;
            Character owner = GetComponentInParent<EquipmentDisplay>().owner;
            //tooltip slot is empty
            if (tooltipData.items == null && data.items != null)
            {
                // Pick up item
                GameManagers.instance.cursorTooltip.UpdateSlot(data, parentInventory, transform.GetSiblingIndex());
                ClearThisSlot();
                parentInventory.SetValue(data, transform.GetSiblingIndex());


                //new for equipment
                owner.UpdateEquipmentData((Equipment)data.items,transform.GetSiblingIndex());
                return;
            }

            //tooltip slot has an item
            else
            {   //added for equipment
                if(tooltipData.items is Equipment){
                    Equipment equipment =  (Equipment)tooltipData.items;
                    if(equipment._type !=_slotType)
                        return;
                }
                else return;


                // Slot is empty
                if (data.items == null)
                {
                    data = tooltipData;
                    onUpdateSlotDisplay(data);
                    GameManagers.instance.cursorTooltip.ClearThisSlot();
                    parentInventory.SetValue(data, transform.GetSiblingIndex());
                    //for equipment
                    owner.UpdateEquipmentData((Equipment)data.items,transform.GetSiblingIndex());
                    
                }
                //slot has an item
                else
                {
                    if (data.items == tooltipData.items)
                    {
                        int total = data.stack + tooltipData.stack;

                        if (data.stack == data.items.maxStackSize)
                        {
                            SwapItems(parentInventory,owner);
                        }
                        else if (total > data.items.maxStackSize)
                        {
                            total -= data.items.maxStackSize;
                            data.stack = data.items.maxStackSize;
                            onUpdateSlotDisplay(data);

                            tooltipData.stack = total;
                            GameManagers.instance.cursorTooltip.UpdateSlot(tooltipData, parentInventory, transform.GetSiblingIndex());
                        }
                        else
                        {
                            data.stack = total;
                            onUpdateSlotDisplay(data);
                            ClearThisSlot();
                        }
                    }
                    else
                    {
                        SwapItems(parentInventory,owner); // character condition
                    }
                }
            }
        }
    }

    public void ClearThisSlot()
    {
        data = new ItemData();
        onUpdateSlotDisplay(data);
    }

    public void ClearTooltipSlot()
    {
        GameManagers.instance.cursorTooltip.ClearThisSlot();
    }

    public void SwapItems(ItemData[] items, Character owner)
    {
        ItemData tempItem = GameManagers.instance.cursorTooltip.slot;
        GameManagers.instance.cursorTooltip.UpdateSlot(data, items, transform.GetSiblingIndex());
        data = tempItem;
        items.SetValue(data, transform.GetSiblingIndex());

        owner.UpdateEquipmentData((Equipment)data.items, transform.GetSiblingIndex());

        onUpdateSlotDisplay(data);
    }

    // Mobile touch-specific behavior
    private bool IsTouchOverSlot()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = touch.position
            };

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (var result in results)
            {
                if (result.gameObject == gameObject)
                    return true;
            }
        }
        return false;
    }
}
