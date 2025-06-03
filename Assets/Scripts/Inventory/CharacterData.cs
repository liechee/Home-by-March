using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "CharacterData/NewCharacterSheet")]
public class CharacterData : ScriptableObject
{
   public Equipment[] equipment = new Equipment[4];
}
