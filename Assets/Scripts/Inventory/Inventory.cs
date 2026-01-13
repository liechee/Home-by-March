using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/New Inventory")]
public class Inventorys : ScriptableObject
{
    // Changed the class to a Scriptable Object
    public ItemData[] itemlist;
}
