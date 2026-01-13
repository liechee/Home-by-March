using System.Collections;
using System.Collections.Generic;
using UnityEngine;



[CreateAssetMenu(menuName = "Inventory/New Item")]
public class Items : ScriptableObject
{
    public Sprite icon;
    public int maxStackSize = 64;

    public string description; // This is public and should be accessible.
    public int value;
}
