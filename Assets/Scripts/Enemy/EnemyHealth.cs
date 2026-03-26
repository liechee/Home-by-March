using UnityEngine;

namespace HomeByMarch {
    public class EnemyHealth : MonoBehaviour {
        [SerializeField] int maxHealth = 100;
        [SerializeField] FloatEventChannel enemyHealthChannel;
        [SerializeField] int currentHealth; // Serialized for testing

        public delegate void DamageTaken();
        public event DamageTaken OnDamageTaken;

        public bool IsDead => currentHealth <= 0;

        void Awake() {
            currentHealth = maxHealth;
        }

        void Start() {
            PublishHealthPercentage();
        }

        public void EnemyTakeDamage(int damage) {
            currentHealth -= damage;
            PublishHealthPercentage();
            // OnDamageTaken?.Invoke(); // Notify listeners that damage was taken

            if (IsDead) {
                Debug.Log("Enemy is dead!");
            }
        }

        void PublishHealthPercentage() {
            if (enemyHealthChannel != null)
                enemyHealthChannel.Invoke(currentHealth / (float)maxHealth);
        }
    }
}
