using UnityEngine;
using UnityEngine.UI;

namespace HomeByMarch
{
    public class PlayerHealth : MonoBehaviour
    {
        public PlayerData playerData;
        public float maxHealth;
        public Image healthBar;

        void Start()
        {
            if (playerData == null)
            {
                playerData = GetComponent<PlayerData>();
            }
            playerData.health = maxHealth;
        }

        void Update()
        {
            // Update the health bar based on the current health
            healthBar.fillAmount = Mathf.Clamp(playerData.health / maxHealth, 0.0f, 1.0f);
            Debug.Log("Current Health: " + playerData.health);
        }

        // Method to take damage
        public void TakeDamage(float damageAmount)
        {
            playerData.health -= damageAmount;

            // Ensure health doesn't drop below zero
            playerData.health = Mathf.Clamp(playerData.health, 0.0f, maxHealth);

            // Check if the player is dead
            if (playerData.health <= 0)
            {
                Die();
            }
        }

        // Method to handle player's death
        private void Die()
        {
            Debug.Log("Player has died.");
            // Additional logic for when the player dies (e.g., respawn, game over screen)
        }
    }
}