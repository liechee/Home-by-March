using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

public class StaticInterface : UserInterface
{
    public GameObject[] slots;
    private static StaticInterface instance;
    [Header("Cloud Settings")]
    public string cloudFileName = "PlayerEquip";

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        DontDestroyOnLoad(this.gameObject);
    }

    new void Start()
    {
        base.Start(); // Call base Start() for initialization
    }

    public override void CreateSlots()
    {
        slotsOnInterface = new Dictionary<GameObject, InventorySlot>();
        for (int i = 0; i < inventory.GetSlots.Length; i++)
        {
            var obj = slots[i];


            AddEvent(obj, EventTriggerType.PointerEnter, delegate { OnEnter(obj); });
            AddEvent(obj, EventTriggerType.PointerExit, delegate { OnExit(obj); });
            AddEvent(obj, EventTriggerType.BeginDrag, delegate { OnDragStart(obj); });
            AddEvent(obj, EventTriggerType.EndDrag, delegate { OnDragEnd(obj); });
            AddEvent(obj, EventTriggerType.Drag, delegate { OnDrag(obj); });

            inventory.GetSlots[i].slotDisplay = obj;

            slotsOnInterface.Add(obj, inventory.GetSlots[i]);

        }
    }
    // CLOUD SAVE/LOAD METHODS

    // public async Task SaveInventoryToCloudButton()
    // {
    //     if (inventory != null)
    //     {
    //         await inventory.SaveInventoryToCloud("PlayerEquip");
    //         Debug.Log("Cloud Save triggered.");
    //     }
    //     else
    //     {
    //         Debug.LogWarning("Inventory reference is missing.");
    //     }
    // }

    // public async Task LoadInventoryFromCloudButton()
    // {
    //     if (inventory != null)
    //     {
    //         await inventory.LoadInventoryFromCloud("PlayerEquip");
    //         Debug.Log("Cloud Load triggered.");
    //     }
    //     else
    //     {
    //         Debug.LogWarning("Inventory reference is missing.");
    //     }
    // }
    public async Task SaveInventoryToCloudButton()
    {
        if (inventory != null)
        {
            await inventory.SaveInventoryToCloud(cloudFileName);
            Debug.Log($"[StaticInterface] Cloud save to '{cloudFileName}' completed.");
        }
        else
        {
            Debug.LogWarning("[StaticInterface] Inventory reference is missing.");
        }
    }

    public async Task LoadInventoryFromCloudButton()
    {
        if (inventory != null)
        {
            await inventory.LoadInventoryFromCloud(cloudFileName);
            Debug.Log($"[StaticInterface] Cloud load from '{cloudFileName}' completed.");
        }
        else
        {
            Debug.LogWarning("[StaticInterface] Inventory reference is missing.");
        }
    }

    // ===============================
    // UI BUTTON WRAPPERS (Non-async)
    // ===============================

    public void OnSave()
    {
        _ = SaveInventoryToCloudButton(); // Fire-and-forget
    }

    public void OnLoad()
    {
        _ = LoadInventoryFromCloudButton(); // Fire-and-forget
    }
}
