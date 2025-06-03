using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour
{
    [Header("Equipable Meshes")]
    public MeshFilter head;
    public MeshFilter body;
    public MeshFilter pants;
    public MeshFilter feet;
    public CharacterData data;

    [Header("Default Skin")]
    public Mesh defHead;
    public Mesh defBody;
    public Mesh defPants;
    public Mesh defFeet;

    private void Start()
    {
        for (int i = 0; i < data.equipment.Length; i++)
        {
            UpdateEquipmentData(data.equipment[i], i);
        }
    }

    public void UpdateEquipmentData(Equipment equipment, int index)
    {
        data.equipment.SetValue(equipment, index);

        if (data.equipment.GetValue(index) == null)
        {
            RemoveEquipmentItem(index);
            return;
        }
        EquipItem(equipment);
    }

    private void EquipItem(Equipment equipment)
    {
        switch (equipment._type)
        {
            case EquipmentType.Head:
                head.mesh = equipment.model;
                break;
            case EquipmentType.Body:
                body.mesh = equipment.model;
                break;
            case EquipmentType.Pants:
                pants.mesh = equipment.model;
                break;
            case EquipmentType.Feet:
                feet.mesh = equipment.model;
                break;
        }
    }

    private void RemoveEquipmentItem(int index)
    {
        switch (index)
        {
            case 0:
                head.mesh = defHead;
                break;
            case 1:
                body.mesh = defBody;
                break;
            case 2:
                pants.mesh = defPants;
                break;
            case 3:
                feet.mesh = defFeet;
                break;
            default:
                break;
        }
    }
}
