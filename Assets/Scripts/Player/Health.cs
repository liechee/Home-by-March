using UnityEngine;
using TMPro; // Required for TextMeshPro

namespace HomeByMarch
{
    public class Health : MonoBehaviour
    {
        [SerializeField] private FloatEventChannel playerHealthChannel;
        [SerializeField] private GameObject deathPanel; // Reference to the death panel
        [SerializeField] private BloodParticle bloodParticle; // Reference to the BloodParticle scriptable object
        [SerializeField] public SFXManager SFXManager;
        [SerializeField] private TextMeshProUGUI healthText; // Reference to TextMeshPro for health display

        public delegate void DamageTaken();
        public event DamageTaken OnDamageTaken;

        public PlayerData playerData;

        public float currentHealth;

        public float CurrentHealth
        {
            get => currentHealth;
            set => currentHealth = value;
        }
        public float MaxHealth => playerData.currentHealth;
        public bool IsDead => currentHealth <= 0;

        void Awake()
        {
            currentHealth = MaxHealth;
        }

        void Start()
        {
            currentHealth = MaxHealth; // Set currentHealth to MaxHealth on start
            PublishHealthPercentage();
            UpdateHealthText(); // Update health display on start
        }

        public void TakeDamage(int damage)
        {
            if (IsDead) return;

            // Reduce damage based on player's defense percentage
            float reducedDamage = damage * (1 - Mathf.Clamp(playerData.defense / 100f, 0f, 0.99f));

            // Round the reduced damage to a whole number
            int finalDamage = Mathf.RoundToInt(reducedDamage);

            currentHealth = Mathf.Clamp(currentHealth - finalDamage, 0, MaxHealth);
            PublishHealthPercentage();
            UpdateHealthText(); // Update health display
            OnDamageTaken?.Invoke();
            Debug.Log($"Player took damage from an enemy: {damage} damage.");

            // Activate BloodParticle effect
            ActivateBloodParticle();
            SFXManager.PlaySFX(SoundTypes.Damage);

            if (IsDead)
            {
                Debug.Log("Player is dead!");
                ShowDeathPanel(); // Show the death panel when the player dies
            }
        }


        public void Heal(int amount)
        {
            if (IsDead) return;

            currentHealth = Mathf.Clamp(currentHealth + amount, 0, MaxHealth);
            PublishHealthPercentage();
            UpdateHealthText(); // Update health display
        }

        private void PublishHealthPercentage()
        {
            if (playerHealthChannel != null)
            {
                playerHealthChannel.Invoke(currentHealth / (float)MaxHealth);
                Debug.Log($"Player Health Updated: {currentHealth}/{MaxHealth}");
            }
            else
            {
                Debug.LogWarning("PlayerHealthChannel is not assigned.");
            }
        }

        // Activate the BloodParticle effect
        private void ActivateBloodParticle()
        {
            if (bloodParticle != null)
            {
                bloodParticle.CastSpell(transform); // Cast the BloodParticle spell
            }
            else
            {
                Debug.LogWarning("BloodParticle is not assigned.");
            }
        }

        // Method to show the death panel
        public void ShowDeathPanel()
        {
            if (deathPanel != null)
            {
                deathPanel.SetActive(true); // Show the death panel
            }
            else
            {
                Debug.LogWarning("Death panel is not assigned.");
            }
        }

        // Optional method to hide the death panel if you need to reset or respawn
        public void HideDeathPanel()
        {
            if (deathPanel != null)
            {
                deathPanel.SetActive(false); // Hide the death panel
            }
        }

        // Update the TextMeshPro health display
        private void UpdateHealthText()
        {
            if (healthText != null)
            {
                healthText.text = $"{currentHealth}/{MaxHealth}"; // Display in format 100/100
            }
            else
            {
                Debug.LogWarning("HealthText is not assigned.");
            }
        }
    }
}
