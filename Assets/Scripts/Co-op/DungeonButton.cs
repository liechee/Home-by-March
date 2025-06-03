using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DungeonButton : MonoBehaviour
{
    public TextMeshProUGUI titleTextMeshPro;
    public TextMeshProUGUI descriptionTextMeshPro;
    public Image targetImage;

    public string title;

    [TextArea]
    public string description;
    public Sprite imageSprite;

    private Button button;

    public void SetDungeonDetails()
    {
        // Update the title and description text
        titleTextMeshPro.text = title;
        descriptionTextMeshPro.text = description;

        // Update the image
        targetImage.sprite = imageSprite;
    }

    public void OnButtonClick()
    {
        // Perform any action when the button is clicked
        SetDungeonDetails();
    }

    // Start is called before the first frame update
    void Start()
    {
        // Find the button component in the children
        button = GetComponentInChildren<Button>();

        // Add listener to the button
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
        }
        else
        {
            Debug.LogError("Button component not found in children.");
        }
    }
}