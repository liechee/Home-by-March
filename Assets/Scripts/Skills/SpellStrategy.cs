using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public abstract class SpellStrategy : ScriptableObject {

    public Sprite uiDisplay;

    public string spellName;

    [TextArea(15, 20)]
    public string description;    
    public abstract void CastSpell(Transform origin);
    
}
