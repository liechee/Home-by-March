using UnityEngine;
using UnityEngine.UI;

public class HeadsUpDisplay : MonoBehaviour
{
    [SerializeField] private Button[] buttons;
    [SerializeField] private Sprite defaultUISprite; // Default sprite for empty or unassigned spells

    public delegate void ButtonPressedEvent(int index);
    public static event ButtonPressedEvent OnButtonPressed;

    void Awake()
    {
        // Initialize button listeners
        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            buttons[i].onClick.AddListener(() => HandleButtonPress(index));
        }

        // Ensure buttons are set correctly on start
        UpdateButtonSprites(new SpellStrategy[buttons.Length]);
    }

    public void UpdateButtonSprites(SpellStrategy[] spells)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (i < spells.Length && spells[i] != null && spells[i].uiDisplay != null)
            {
                buttons[i].image.sprite = spells[i].uiDisplay;
                buttons[i].interactable = true; // Enable the button
                Debug.Log($"Spell {i}: {spells[i].uiDisplay}");
            }
            else
            {
                buttons[i].image.sprite = defaultUISprite; // Set to default sprite
                buttons[i].interactable = false; // Disable the button
                Debug.Log($"Button {i}: Default sprite set and button disabled.");
            }
        }
    }

    void HandleButtonPress(int index) => OnButtonPressed?.Invoke(index);
}
