using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using HomeByMarch;
using UnityEngine.InputSystem;

public class Play: MonoBehaviour
{
    public float health = 100f;
    public float maxHealth = 100f;
    public HealthBar healthBar; // Reference to the HealthBar script

    private void Update(){
        //healthBar.fillAmount = health / maxHealth; // Update the health bar fill amount
        if(Keyboard.current.eKey.wasPressedThisFrame){
            TakeDamage(10f); // Example damage taken when space is pressed
        }
    }
    public void TakeDamage(float damage){
        health -= damage;
        health = Mathf.Max(health, 0); // Ensure health doesn't go below 0
    }
}
