using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class Chest : InventoryHolder, IInteractable
{
    // Start is called before the first frame update
    public void Interact()
    {
        GameManagers.instance.publicAccessInvDisplay.inventoryHolder = this;
        if(display == null) display = GameManagers.instance.publicAccessInvDisplay.transform;
        GameManagers.instance.DisplayInventoryWindows(true);

    }
}
