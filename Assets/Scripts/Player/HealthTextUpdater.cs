using UnityEngine;
using TMPro;
using System.IO;
using System;

public class HealthTextUpdater : MonoBehaviour
{
    public PlayerData playerData;
    public TextMeshProUGUI healthTextMeshPro;
    public TextMeshProUGUI attackTextMeshPro;
    public TextMeshProUGUI defenseTextMeshPro;
    public TextMeshProUGUI cooldownTextMeshPro;
    public TextMeshProUGUI movementSpeedTextMeshPro;

    public TextMeshProUGUI healthBuff;
    public TextMeshProUGUI attackBuff;
    public TextMeshProUGUI defenseBuff;
    public TextMeshProUGUI cooldownBuff;
    public TextMeshProUGUI movementSpeedBuff;

    void Start()
    {
        if (playerData == null || healthTextMeshPro == null || attackTextMeshPro == null || defenseTextMeshPro == null || cooldownTextMeshPro == null || movementSpeedTextMeshPro == null)
        {
            Debug.LogError("PlayerData or TextMeshProUGUI references are not assigned.");
            return;
        }
        UpdateHealthText();
        UpdateAttackText();
        UpdateDefenseText();
        UpdateCooldownText();
        UpdateMovementSpeedText();
    }

    public void UpdateHealthText()
    {
        healthTextMeshPro.text = playerData.health.ToString();
        healthBuff.text = "+"+playerData.healthBuff.ToString();
    }

    public void UpdateAttackText()
    {
        attackTextMeshPro.text = playerData.attack.ToString();
        attackBuff.text = "+"+playerData.attackBuff.ToString();
    }

    public void UpdateDefenseText()
    {
        defenseTextMeshPro.text = playerData.defense.ToString();
        defenseBuff.text = "+"+playerData.defenseBuff.ToString();
    }

    public void UpdateCooldownText()
    {
        cooldownTextMeshPro.text = playerData.cooldown.ToString() + "%";
        cooldownBuff.text = "+"+playerData.cooldownBuff.ToString();
    }

    public void UpdateMovementSpeedText()
    {
        movementSpeedTextMeshPro.text = Math.Round(playerData.currentMovementSpeed).ToString();
        movementSpeedBuff.text = "+"+playerData.movementSpeedBuff.ToString();
    }

    void Update()
    {
        // Optionally, update the text every frame
        UpdateHealthText();
        UpdateAttackText();
        UpdateDefenseText();
        UpdateCooldownText();
        UpdateMovementSpeedText();
    }
}