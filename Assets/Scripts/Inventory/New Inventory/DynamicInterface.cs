using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

public class DynamicInterface : UserInterface
{
    public GameObject inventoryPrefab;
    public int X_START;
    public int Y_START;
    public int X_SPACE_BETWEEN_ITEM;
    public int NUMBER_OF_COLUMN;
    public int Y_SPACE_BETWEEN_ITEMS;

    private static DynamicInterface instance;
    [Header("Cloud Settings")]
    public string cloudFileName = "PlayerInventory";


    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    void Start()
    {
        base.Start(); // Call base Start() for initialization
    }

    public override void CreateSlots()
    {
        slotsOnInterface = new Dictionary<GameObject, InventorySlot>();
        for (int i = 0; i < inventory.GetSlots.Length; i++)
        {
            var obj = Instantiate(inventoryPrefab, Vector3.zero, Quaternion.identity, transform);
            obj.GetComponent<RectTransform>().localPosition = GetPosition(i);

            AddEvent(obj, EventTriggerType.PointerEnter, delegate { OnEnter(obj); });
            AddEvent(obj, EventTriggerType.PointerExit, delegate { OnExit(obj); });
            AddEvent(obj, EventTriggerType.BeginDrag, delegate { OnDragStart(obj); });
            AddEvent(obj, EventTriggerType.EndDrag, delegate { OnDragEnd(obj); });
            AddEvent(obj, EventTriggerType.Drag, delegate { OnDrag(obj); });

            inventory.GetSlots[i].slotDisplay = obj;
            slotsOnInterface.Add(obj, inventory.GetSlots[i]);
        }
    }

    private Vector3 GetPosition(int i)
    {
        return new Vector3(X_START + (X_SPACE_BETWEEN_ITEM * (i % NUMBER_OF_COLUMN)), Y_START + (-Y_SPACE_BETWEEN_ITEMS * (i / NUMBER_OF_COLUMN)), 0f);
    }

    // // CLOUD SAVE/LOAD METHODS

    // public async Task SaveInventoryToCloudButton()
    // {
    //     if (inventory != null)
    //     {
    //         await inventory.SaveInventoryToCloud("PlayerInventory");
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
    //         await inventory.LoadInventoryFromCloud("PlayerInventory");
    //         Debug.Log("Cloud Load triggered.");
    //     }
    //     else
    //     {
    //         Debug.LogWarning("Inventory reference is missing.");
    //     }
    // }
    // ===============================
    // CLOUD SAVE / LOAD FUNCTIONALITY
    // ===============================

    /// <summary>
    /// Asynchronously saves inventory to the cloud.
    /// </summary>
    public async Task SaveInventoryToCloudButton()
    {
        if (inventory != null)
        {
            await inventory.SaveInventoryToCloud(cloudFileName);
            Debug.Log("Cloud Save triggered." );
        }
        else
        {
            Debug.LogWarning("Inventory reference is missing.");
        }
    }

    /// <summary>
    /// Asynchronously loads inventory from the cloud.
    /// </summary>
    public async Task LoadInventoryFromCloudButton()
    {
        if (inventory != null)
        {
            await inventory.LoadInventoryFromCloud(cloudFileName);
            Debug.Log("Cloud Load triggered.");
        }
        else
        {
            Debug.LogWarning("Inventory reference is missing.");
        }
    }

    // ===============================
    // UI BUTTON WRAPPERS
    // ===============================

    /// <summary>
    /// UI-compatible button wrapper to trigger cloud save.
    /// </summary>
    public void OnSave()
    {
        _ = SaveInventoryToCloudButton(); // Fire-and-forget
    }

    /// <summary>
    /// UI-compatible button wrapper to trigger cloud load.
    /// </summary>
    public void OnLoad()
    {
        _ = LoadInventoryFromCloudButton(); // Fire-and-forget
    }


}
