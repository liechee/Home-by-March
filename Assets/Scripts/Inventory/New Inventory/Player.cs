using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public InventoryObject inventory;
    public InventoryObject equipment;

    public Attribute[] attributes;

    private Transform boots;
    private Transform chest;
    private Transform helmet;
    private Transform offhand;
    private Transform sword;

    public Transform weaponTransform;
    public Transform headTransform;
    public Transform offhandWristTransform;
    public Transform offhandHandTransform;

    public SpellStrategy[] spells;

    public PlayerData playerData;

    public HeadsUpDisplay headsUpDisplay;
    public InventorySkillDisplay inventorySkillDisplay;  // Added reference to InventorySkillDisplay

    private void Start()
    {
        // Get reference to InventorySkillDisplay

        if (headsUpDisplay == null)
        {
            Debug.LogWarning("HeadsUpDisplay not found! Ensure it is assigned in the scene.");
        }

        if (inventorySkillDisplay == null)
        {
            Debug.LogError("InventorySkillDisplay not found! Ensure it is assigned in the scene.");
        }

        for (int i = 0; i < attributes.Length; i++)
            attributes[i].SetParent(this);

        for (int i = 0; i < equipment.GetSlots.Length; i++)
        {
            equipment.GetSlots[i].OnBeforeUpdate += OnRemoveItem;
            equipment.GetSlots[i].OnAfterUpdate += OnAddItem;
        }

        UpdatePlayerDataAttributes();
    }

    private void UpdatePlayerDataAttributes()
    {
        playerData.healthBuff = 0;
        playerData.attackBuff = 0;
        playerData.defenseBuff = 0;
        playerData.cooldownBuff = 0;

        foreach (var attribute in attributes)
        {
            switch (attribute.type)
            {
                case Attributes.Health:
                    playerData.healthBuff += attribute.value.ModifiedValue;
                    break;
                case Attributes.Attack:
                    playerData.attackBuff += (int)attribute.value.ModifiedValue;
                    break;
                case Attributes.Defense:
                    playerData.defenseBuff += (int)attribute.value.ModifiedValue;
                    break;
                case Attributes.Cooldown:
                    playerData.cooldownBuff += attribute.value.ModifiedValue;
                    break;
            }
        }

        Debug.Log("PlayerData buffs updated incrementally.");
    }

    public void OnAddItem(InventorySlot _slot)
    {
        if (_slot.ItemObject == null) return;

        switch (_slot.parent.inventory.type)
        {
            case InterfaceType.Equipment:
                // Add buffs
                foreach (var buff in _slot.item.buffs)
                {
                    foreach (var attribute in attributes)
                    {
                        if (attribute.type == buff.attribute)
                            attribute.value.AddModifier(buff);
                    }
                }

                // Add visuals
                if (_slot.ItemObject.characterDisplay != null)
                {
                    switch (_slot.AllowedItems[0])
                    {
                        case ItemType.Helmet:
                            helmet = Instantiate(_slot.ItemObject.characterDisplay, headTransform).transform;
                            AddSkillToSpells(_slot.ItemObject.skillData as SpellStrategy);
                            break;
                        case ItemType.Weapon:
                            sword = Instantiate(_slot.ItemObject.characterDisplay, weaponTransform).transform;
                            AddSkillToSpells(_slot.ItemObject.skillData as SpellStrategy);
                            break;
                        case ItemType.Shield:
                            offhand = Instantiate(_slot.ItemObject.characterDisplay, offhandHandTransform).transform;
                            AddSkillToSpells(_slot.ItemObject.skillData as SpellStrategy);
                            break;
                    }
                }
                break;
        }

        UpdatePlayerDataAttributes();
        playerData.UpdateCurrentStats();
    }

    public void OnRemoveItem(InventorySlot _slot)
    {
        if (_slot.ItemObject == null) return;

        switch (_slot.parent.inventory.type)
        {
            case InterfaceType.Equipment:
                // Remove buffs
                foreach (var buff in _slot.item.buffs)
                {
                    foreach (var attribute in attributes)
                    {
                        if (attribute.type == buff.attribute)
                            attribute.value.RemoveModifier(buff);
                    }
                }

                // Remove visuals
                switch (_slot.AllowedItems[0])
                {
                    case ItemType.Helmet:
                        if (helmet != null) { Destroy(helmet.gameObject); helmet = null; RemoveSkillFromSpells(_slot.ItemObject.skillData as SpellStrategy); }
                        break;
                    case ItemType.Weapon:
                        if (sword != null) { Destroy(sword.gameObject); sword = null; RemoveSkillFromSpells(_slot.ItemObject.skillData as SpellStrategy); }
                        break;
                    case ItemType.Shield:
                        if (offhand != null) { Destroy(offhand.gameObject); offhand = null; RemoveSkillFromSpells(_slot.ItemObject.skillData as SpellStrategy); }
                        break;
                }
                break;
        }

        UpdatePlayerDataAttributes();
        playerData.UpdateCurrentStats();
    }

    private void OnEnable()
    {
        HeadsUpDisplay.OnButtonPressed += CastSpell;
    }

    private void OnDisable()
    {
        HeadsUpDisplay.OnButtonPressed -= CastSpell;
    }

    private void CastSpell(int index)
    {
        spells[index].CastSpell(transform);
        Debug.Log("Spell casted");
    }

    private void AddSkillToSpells(SpellStrategy skillData)
    {
        if (skillData != null)
        {
            List<SpellStrategy> spellList = new List<SpellStrategy>(spells);
            spellList.Add(skillData);
            spells = spellList.ToArray();

            Debug.Log("Updated spells list:");
            foreach (var spell in spells)
            {
                Debug.Log(spell);
            }

            UpdateSpellButtonSprites(); // Update button sprites after adding a skill
        }
    }

    private void RemoveSkillFromSpells(SpellStrategy skillData)
    {
        if (skillData != null)
        {
            List<SpellStrategy> spellList = new List<SpellStrategy>(spells);
            if (spellList.Remove(skillData))
            {
                spells = spellList.ToArray();

                Debug.Log("Updated spells list after removal:");
                foreach (var spell in spells)
                {
                    Debug.Log(spell);
                }

                UpdateSpellButtonSprites(); // Update button sprites after removing a skill
            }
        }
    }

    private void UpdateSpellButtonSprites()
    {
        if (headsUpDisplay != null)
        {
            Debug.Log(" Updating HeadsUp button sprites with current spells.");
            headsUpDisplay.UpdateButtonSprites(spells);
        }
        if (inventorySkillDisplay != null)
        {
            Debug.Log("Updating button sprites with current spells.");
            inventorySkillDisplay.UpdateButtonSprites(spells);
        }
        else
        {
            Debug.LogWarning("InventorySkillDisplay is null. Skipping sprite update.");
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        var groundItem = other.GetComponent<GroundItem>();
        if (groundItem)
        {
            Item _item = new Item(groundItem.item);
            if (inventory.AddItem(_item, 1))
            {
                Destroy(other.gameObject);
                inventory.Save();
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            inventory.Save();
            equipment.Save();
        }

        if (Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            inventory.Load();
            equipment.Load();
        }
    }

    public void AttributeModified(Attribute attribute)
    {
        Debug.Log(string.Concat(attribute.type, " was updated! Value is now ", attribute.value.ModifiedValue));
    }

    private void OnApplicationQuit()
    {
        inventory.Clear();
        equipment.Clear();
    }
}


[System.Serializable]
public class Attribute
{
    [System.NonSerialized] public Player parent;
    public Attributes type;
    public ModifiableInt value;

    public void SetParent(Player _parent)
    {
        parent = _parent;
        value = new ModifiableInt(AttributeModified);
    }

    public void AttributeModified()
    {
        parent.AttributeModified(this);
    }
}
