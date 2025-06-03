using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Import TextMesh Pro namespace

public class ButtonCooldownManager : MonoBehaviour
{
    [System.Serializable]
    public class CooldownButton
    {
        public Button button; // The button to assign
        public TMP_Text cooldownText; // TextMesh Pro Text to display the cooldown timer
        public float cooldownTime; // Cooldown duration in seconds
    }

    public List<CooldownButton> cooldownButtons = new List<CooldownButton>();

    private void Start()
    {
        // Attach the OnClick listeners to all buttons in the list
        foreach (var cooldownButton in cooldownButtons)
        {
            cooldownButton.button.onClick.AddListener(() => StartCooldown(cooldownButton));
        }
    }

    private void StartCooldown(CooldownButton cooldownButton)
    {
        // Disable the button and start the cooldown process
        cooldownButton.button.interactable = false;
        StartCoroutine(CooldownCoroutine(cooldownButton));
    }

    private IEnumerator CooldownCoroutine(CooldownButton cooldownButton)
    {
        float remainingTime = cooldownButton.cooldownTime;

        // Update the cooldown text and countdown
        while (remainingTime > 0)
        {
            cooldownButton.cooldownText.text = Mathf.CeilToInt(remainingTime).ToString();
            remainingTime -= Time.deltaTime;
            yield return null;
        }

        // Reset the button and clear the text
        cooldownButton.cooldownText.text = "";
        cooldownButton.button.interactable = true;
    }
}
