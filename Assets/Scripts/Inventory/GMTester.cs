using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Required for Button

public class GMTester : MonoBehaviour
{
    public GameObject prefab; // Reference to the prefab or GameObject to toggle
    private bool isOpen;

    public Button toggleButton; // Reference to the button to trigger the toggle

    private void Start()
    {
        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(ToggleInventory); // Add listener to button click
        }
    }

    private void ToggleInventory()
    {
        isOpen = !isOpen;

        // Use GameManagers to display/hide inventory windows
        GameManagers.instance.DisplayInventoryWindows(isOpen);

        // Toggle the prefab's active state (if any additional UI is tied to this toggle)
        if (prefab != null)
        {
            prefab.SetActive(isOpen);
        }

        // Debug log to confirm the toggle state
        Debug.Log($"Inventory toggled: {(isOpen ? "Opened" : "Closed")}");
    }
}
