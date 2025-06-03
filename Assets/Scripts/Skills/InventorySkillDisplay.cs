using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySkillDisplay : MonoBehaviour
{
    [SerializeField] private Button[] buttons;
    [SerializeField] private Sprite defaultUISprite; // Default sprite for empty or unassigned spells
    [SerializeField] private GameObject panel; // Panel to show when a button is pressed
    [SerializeField] private TextMeshProUGUI spellNameText; // TextMeshPro for spell name
    [SerializeField] private TextMeshProUGUI spellDescriptionText; // TextMeshPro for spell description
    [SerializeField] private Image itemImage;
    private SpellStrategy[] currentSpells;

    void Awake()
    {
        // Ensure the panel is hidden initially
        panel.SetActive(false);

        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            buttons[i].onClick.AddListener(() => HandleButtonPress(index));
        }
    }

    public void UpdateButtonSprites(SpellStrategy[] spells)
    {
        currentSpells = spells;

        for (int i = 0; i < buttons.Length; i++)
        {
            Sprite newSprite;
            bool interactable;

            if (i < spells.Length && spells[i] != null && spells[i].uiDisplay != null)
            {
                newSprite = spells[i].uiDisplay;
                interactable = true;
            }
            else
            {
                newSprite = defaultUISprite;
                interactable = false;
            }

            if (buttons[i].image.sprite != newSprite)
            {
                Debug.Log($"Button {i}: Sprite changed from {buttons[i].image.sprite} to {newSprite}");
                buttons[i].image.sprite = newSprite;
            }

            buttons[i].interactable = interactable;
            Debug.Log($"Button {i}: {(interactable ? "Enabled" : "Disabled")}");
        }
    }

    void HandleButtonPress(int index)
    {
        if (index < currentSpells.Length && currentSpells[index] != null)
        {
            // Show the panel and update the text fields with the spell's name and description
            panel.SetActive(true);
            spellNameText.text = currentSpells[index].spellName;
            spellDescriptionText.text = currentSpells[index].description;
            itemImage.sprite = currentSpells[index].uiDisplay;
            Debug.Log($"Button {index} pressed, panel shown with spell name and description.");
        }
    }
}