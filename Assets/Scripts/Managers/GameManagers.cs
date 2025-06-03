using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManagers : MonoBehaviour
{
    // Singleton instance
    public static GameManagers instance { get; private set; }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        // DontDestroyOnLoad(gameObject); // Optional: persists across scenes
    }

    [Header("Inventory Management")]
    public CursorTooltip cursorTooltip;

    [SerializeField] private GameState gameState;

    // Time threshold to differentiate between tap and hold (in seconds)
    [SerializeField] private float holdThreshold = 0.3f;

    

    private void Start()
    {
        SetGameState(gameState);
    }

    public void SetGameState(GameState newState)
    {
        gameState = newState;

        switch (newState)
        {
            case GameState.Game_Only:
                // Disable tooltip and lock cursor for game-only state
                cursorTooltip.followMouse = false;
                cursorTooltip.HideTooltip();
                Cursor.visible = false;
                // Cursor.lockState = CursorLockMode.Locked;
                Debug.Log("GameOnly");

                // Return items to inventory if tooltip slot is occupied
                if (cursorTooltip.slot.items != null)
                {
                    cursorTooltip.AddBackToSlot();
                }
                break;

            case GameState.UI_Only:
                // Enable tooltip and unlock cursor for UI-only state
                cursorTooltip.followMouse = true;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                Debug.Log("UI_Only");
                break;
        }
    }

    private void Update()
    {
        if (gameState != GameState.Game_Only) return; // Skip tooltip logic if not in Game_Only

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    cursorTooltip.touchStartTime = Time.time;
                    break;

                case TouchPhase.Moved:
                    if (Time.time - cursorTooltip.touchStartTime > holdThreshold)
                    {
                        cursorTooltip.HideTooltip();
                    }
                    break;

                case TouchPhase.Ended:
                    if (Time.time - cursorTooltip.touchStartTime <= holdThreshold)
                    {
                        if (!cursorTooltip.IsTouchInsideTooltip(touch.position))
                        {
                            cursorTooltip.HideTooltip();
                        }
                        else
                        {
                            cursorTooltip.DisplayTooltip("Sample Item", "Sample Description", 100);
                        }
                    }
                    break;
            }
        }
    }


    [Tooltip("For the Player's Backpack ~ Inventory Window(UI)")]
    public DynamicInventoryDisplay playerInvDisplay;

    [Tooltip("For any other inventory ~ Inventory Window(UI)")]
    public DynamicInventoryDisplay publicAccessInvDisplay;

    [Tooltip("For Equipment ~ Inventory Window(UI)")]
    public EquipmentDisplay playerEquipDisplay;

    public void DisplayInventoryWindows(bool display)
    {
        // Disable tooltip when opening inventory or switching to UI_Only
        cursorTooltip.HideTooltip();
        cursorTooltip.followMouse = false;

        if (playerInvDisplay.inventoryHolder != null)
        {
            playerInvDisplay.onDisplay(display);
            Debug.Log($"Player Inventory Display {(display ? "enabled" : "disabled")}");
        }

        if (publicAccessInvDisplay.inventoryHolder != null)
        {
            publicAccessInvDisplay.onDisplay(display);
            Debug.Log($"Public Access Inventory Display {(display ? "enabled" : "disabled")}");
        }

        if (playerEquipDisplay.owner.data.equipment != null)
        {
            playerEquipDisplay.onDisplay(display);
            Debug.Log($"Equipment Display {(display ? "enabled" : "disabled")}");
        }
        else
        {
            Debug.LogWarning("No equipment data found, Equipment Display will not be shown.");
        }

        // Reset the Public Display and return to game mode if display == false
        if (!display)
        {
            publicAccessInvDisplay.inventoryHolder = null;
            SetGameState(GameState.Game_Only);
        }
        else
        {
            SetGameState(GameState.UI_Only);
        }
    }

}

[System.Serializable]
public enum GameState
{
    UI_Only,
    Game_Only
}
