using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class CursorTooltip : MonoBehaviour
{
    public GameObject tooltipUI;
    [SerializeField] private int tooltipID;
    [SerializeField] private Vector3 tooltipOffset;
    public TMP_Text tooltipName;
    public TMP_Text tooltipDescription;
    public TMP_Text tooltipValue;
    public bool followMouse;

    [SerializeField] private int slotID;
    [SerializeField] private Vector3 slotOffset;
    public Image slotIcon;
    public TMP_Text slotCounter;
    public ItemData slot;
    [HideInInspector] public bool slotOffsetOveride;
    private ItemData[] prevInventory;
    private int prevIndex;

    // New property to track touch duration for hold detection
    public float touchStartTime;

    // RectTransform of the tooltip UI
    private RectTransform tooltipRectTransform;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);  // This will ensure the tooltip persists across scenes
    }

    void Start()
    {
        tooltipUI.SetActive(false); // Ensure tooltip is hidden at the start
        tooltipRectTransform = tooltipUI.GetComponent<RectTransform>(); // Get the RectTransform of the tooltip
    }

    void Update()
    {
        if (followMouse && tooltipUI.activeSelf)
        {
            Vector3 mousePos = Input.mousePosition;
            tooltipUI.transform.position = mousePos;
        }

        if (!slotOffsetOveride)
            transform.position = Input.mousePosition + tooltipOffset;
        else
            transform.position = Input.mousePosition + slotOffset;
    }

    public void UpdateSlot(ItemData data, ItemData[] parentInv, int index)
    {
        slot = data;
        prevInventory = parentInv;
        prevIndex = index;
        if (data.items == null)
        {
            slotIcon.sprite = null;
            slotCounter.text = "";
            slotOffsetOveride = false;

            HideTempSlot();
        }
        else
        {
            slotIcon.sprite = data.items.icon;
            slotCounter.text = data.stack.ToString();
            slotOffsetOveride = true;

            DisplayTempSlot();
            HideTooltip();
        }
    }

    public void ClearThisSlot()
    {
        slot = new ItemData();
        UpdateSlot(slot, null, -1);
        prevInventory = null;
    }

    public void AddBackToSlot()
    {
        prevInventory.SetValue(slot, prevIndex);
        ClearThisSlot();
    }

    public void DisplayTempSlot()
    {
        transform.GetChild(slotID).gameObject.SetActive(true);
    }

    public void HideTempSlot()
    {
        transform.GetChild(slotID).gameObject.SetActive(false);
    }

    public void DisplayTooltip(string name, string description, int value)
    {
        tooltipName.text = name;
        tooltipDescription.text = description;
        tooltipValue.text = value.ToString();
        tooltipUI.SetActive(true);

        if (followMouse)
        {
            Vector3 mousePos = Input.mousePosition;
            tooltipUI.transform.position = mousePos;
        }
    }

    public void HideTooltip()
    {
        tooltipUI.SetActive(false);
    }

    // Check if the touch is inside the tooltip area
    public bool IsTouchInsideTooltip(Vector2 touchPosition)
    {
        // Convert touch position to world space to compare with the tooltip's RectTransform
        Vector2 localPosition = tooltipRectTransform.InverseTransformPoint(touchPosition);
        return tooltipRectTransform.rect.Contains(localPosition);
    }
}
