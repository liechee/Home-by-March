using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System;

public enum InterfaceType
{
    Inventory,
    Equipment,
    Chest
}

[CreateAssetMenu(fileName = "New Inventory", menuName = "Inventory System/Inventory")]
public class InventoryObject : ScriptableObject
{
    public string savePath;
    public ItemDatabaseObject database;
    public InterfaceType type;
    public Inventory Container;

    public static event Action onInventoryLoaded;

    public InventorySlot[] GetSlots => Container.Slots;


    public bool AddItem(Item _item, int _amount)
    {
        if (EmptySlotCount <= 0) return false;
        InventorySlot slot = FindItemOnInventory(_item);
        if (!database.ItemObjects[_item.Id].stackable || slot == null)
        {
            SetEmptySlot(_item, _amount);
            return true;
        }
        slot.AddAmount(_amount);
        return true;
    }

    public int EmptySlotCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < GetSlots.Length; i++)
                if (GetSlots[i].item.Id <= -1) count++;
            return count;
        }
    }

    public InventorySlot FindItemOnInventory(Item _item)
    {
        for (int i = 0; i < GetSlots.Length; i++)
            if (GetSlots[i].item.Id == _item.Id) return GetSlots[i];
        return null;
    }

    public InventorySlot SetEmptySlot(Item _item, int _amount)
    {
        for (int i = 0; i < GetSlots.Length; i++)
        {
            if (GetSlots[i].item.Id <= -1)
            {
                GetSlots[i].UpdateSlot(_item, _amount);
                return GetSlots[i];
            }
        }
        return null;
    }

    public void SwapItems(InventorySlot item1, InventorySlot item2)
    {
        if (item2.CanPlaceInSlot(item1.ItemObject) && item1.CanPlaceInSlot(item2.ItemObject))
        {
            InventorySlot temp = new InventorySlot(item2.item, item2.amount);
            item2.UpdateSlot(item1.item, item1.amount);
            item1.UpdateSlot(temp.item, temp.amount);
        }
    }

    [ContextMenu("Save")]
    public void Save()
    {
        IFormatter formatter = new BinaryFormatter();
        Stream stream = new FileStream(
            string.Concat(Application.persistentDataPath, savePath),
            FileMode.Create, FileAccess.Write);
        formatter.Serialize(stream, Container);
        stream.Close();
    }

    [ContextMenu("Load")]
    public void Load()
    {
        string path = string.Concat(Application.persistentDataPath, savePath);
        if (!File.Exists(path)) return;

        IFormatter formatter = new BinaryFormatter();
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        Inventory loaded = (Inventory)formatter.Deserialize(stream);
        stream.Close();

        for (int i = 0; i < GetSlots.Length; i++)
            GetSlots[i].UpdateSlot(loaded.Slots[i].item, loaded.Slots[i].amount);
    }

    [ContextMenu("Clear")]
    public void Clear() => Container.Clear();

    public async Task SaveInventoryToCloud(string fileName)
    {
        // Skip if empty so we don't overwrite cloud data with a blank inventory
        if (EmptySlotCount == GetSlots.Length)
        {
            Debug.LogWarning($"[Inventory] SaveInventoryToCloud skipped — inventory '{name}' is empty.");
            return;
        }

        // Persist to local binary file first so local and cloud stay in sync
        Save();

        string json = JsonUtility.ToJson(Container, true);
       // await CloudSaver.SaveDataToCloud(fileName, json);
        await CloudSaver2.SaveData(fileName, json); // Optional second cloud save for redundancy
        Debug.Log($"[Inventory] Saved '{name}' to cloud as '{fileName}'.");
    }

    public async Task LoadInventoryFromCloud(string fileName)
    {
      //  string json2 = await CloudSaver.LoadDataFromCloud(fileName);
        string json = await CloudSaver2.LoadData(fileName); // Optional second cloud load for redundancy

        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning($"[Inventory] No cloud data found for '{fileName}'.");
            return;
        }

        Inventory loaded = null;
        try
        {
            loaded = JsonUtility.FromJson<Inventory>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Inventory] Failed to deserialize '{fileName}': {e.Message}");
            return;
        }

        if (loaded?.Slots == null)
        {
            Debug.LogError($"[Inventory] Deserialized data for '{fileName}' has null slots.");
            return;
        }

        // Apply cloud slots to in-memory Container — handle slot count mismatches gracefully
        int match = Mathf.Min(loaded.Slots.Length, GetSlots.Length);
        for (int i = 0; i < match; i++)
            GetSlots[i].UpdateSlot(loaded.Slots[i].item, loaded.Slots[i].amount);

        // Zero out any extra slots if current inventory is larger than cloud save
        for (int i = match; i < GetSlots.Length; i++)
            GetSlots[i].UpdateSlot(new Item(), 0);

        if (loaded.Slots.Length != GetSlots.Length)
            Debug.LogWarning($"[Inventory] Slot count mismatch for '{fileName}': " +
                             $"cloud={loaded.Slots.Length}, current={GetSlots.Length}. Migrated {match} slots.");

        Save();

        Debug.Log($"[Inventory] Loaded '{fileName}' from cloud and applied to '{name}'.");
        onInventoryLoaded?.Invoke();
    }
}

[System.Serializable]
public class Inventory
{
    public InventorySlot[] Slots = new InventorySlot[28];

    public void Clear()
    {
        for (int i = 0; i < Slots.Length; i++)
            Slots[i].RemoveItem();
    }
}

public delegate void SlotUpdated(InventorySlot _slot);

[System.Serializable]
public class InventorySlot
{
    public ItemType[]  AllowedItems = new ItemType[0];

    [System.NonSerialized] public UserInterface parent;
    [System.NonSerialized] public GameObject    slotDisplay;
    [System.NonSerialized] public SlotUpdated   OnAfterUpdate;
    [System.NonSerialized] public SlotUpdated   OnBeforeUpdate;

    public Item item   = new Item();
    public int  amount;

    public ItemObject ItemObject
    {
        get
        {
            if (item.Id >= 0 && parent?.inventory?.database != null)
                return parent.inventory.database.ItemObjects[item.Id];
            return null;
        }
    }

    public InventorySlot()                          => UpdateSlot(new Item(), 0);
    public InventorySlot(Item _item, int _amount)   => UpdateSlot(_item, _amount);

    public void UpdateSlot(Item _item, int _amount)
    {
        OnBeforeUpdate?.Invoke(this);
        item   = _item;
        amount = _amount;
        OnAfterUpdate?.Invoke(this);
    }

    public void RemoveItem()             => UpdateSlot(new Item(), 0);
    public void AddAmount(int value)     => UpdateSlot(item, amount += value);

    public bool CanPlaceInSlot(ItemObject _itemObject)
    {
        if (AllowedItems.Length <= 0 || _itemObject == null || _itemObject.data.Id < 0)
            return true;
        for (int i = 0; i < AllowedItems.Length; i++)
            if (_itemObject.type == AllowedItems[i]) return true;
        return false;
    }
}