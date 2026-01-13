using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class RewardItem: MonoBehaviour, ISerializationCallbackReceiver
{
    public ItemObject item;
    public int quantity;
    
    // Called after deserialization to update the sprite
    public void OnAfterDeserialize()
    {
        
    }

    public void OnBeforeSerialize()
    {
#if UNITY_EDITOR
        // You can add any serialization-related code here, but it's not needed to change the sprite
#endif
    }
}
