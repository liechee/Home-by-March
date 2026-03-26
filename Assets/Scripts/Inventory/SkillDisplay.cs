using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillDisplay : MonoBehaviour
{
    public Button skillButton; // Reference to the button
    public Image skillIcon;    // Image for the skill icon
    public TextMeshProUGUI skillNameText; // Text for the skill name
    public TextMeshProUGUI skillDescriptionText; // Text for the skill description

    // Method to update the display with skill data
    public void SetSkill(Skill skill)
    {
        if (skill == null)
        {
            Debug.LogWarning("Skill is null, cannot display.");
            return;
        }

        // Update the button and UI elements with skill data
        if (skillIcon != null)
            skillIcon.sprite = skill.icon;

        if (skillNameText != null)
            skillNameText.text = skill.skillName;

        if (skillDescriptionText != null)
            skillDescriptionText.text = skill.description;

        // Optionally, add an event listener for button clicks
        if (skillButton != null)
        {
            skillButton.onClick.RemoveAllListeners(); // Clear existing listeners
            skillButton.onClick.AddListener(() => OnSkillButtonClick(skill));
        }
    }

    // Example method to handle button click
    private void OnSkillButtonClick(Skill skill)
    {
        Debug.Log($"Skill {skill.skillName} clicked! Power: {skill.power}, Cooldown: {skill.cooldown}");
        // Implement additional behavior (e.g., equip or use the skill)
    }
}
