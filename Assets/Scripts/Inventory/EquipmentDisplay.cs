using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EquipmentDisplay : MonoBehaviour
{
    // Start is called before the first frame update
    public Character owner;
    public EquipmentSlot slotPrefab;

    public ItemData[] equipParent = new ItemData[4];

    public void onDisplay(bool isDisplayed)
    {
        switch (isDisplayed)
        {
            case true:
                for (int i = 0; i < owner.data.equipment.Length; i++)
                {
                    EquipmentSlot slot = Instantiate(slotPrefab, transform) as EquipmentSlot;
                    switch (i)
                    {
                        case 0:
                            slot._slotType = EquipmentType.Head;
                            break;
                        case 1:
                            slot._slotType = EquipmentType.Body;
                            break;
                        case 2:
                            slot._slotType = EquipmentType.Pants;
                            break;
                        case 3:
                            slot._slotType = EquipmentType.Feet;
                            break;
                        default:    
                            break;
                    }

                    //create temp data

                    ItemData tempData = new ItemData();

                    tempData.items = owner.data.equipment[i];

                    if (tempData.items != null) tempData.stack = 1;
                    else tempData.stack = -1;

                    equipParent[i] = tempData;
                    slot.onUpdateSlotDisplay(tempData);
                }
                break;
            case false:
                foreach (Transform child in transform)
                {
                    Destroy(child.gameObject);
                }
                break;
        }
    }
}
