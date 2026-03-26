using UnityEngine;
using Utilities;

namespace HomeByMarch {
    public class EnemyDetector : MonoBehaviour {
        [SerializeField] float detectionAngle = 60f; // Cone in front of enemy
        [SerializeField] float detectionRadius = 10f; // Large circle around enemy
        [SerializeField] float innerDetectionRadius = 5f; // Small circle around enemy
        [SerializeField] float detectionCooldown = 1f; // Time between detections
        [SerializeField] float attackRange = 2f; // Distance from enemy to player to attack

        public Transform Enemy { get; private set; }
        public Health EnemyHealth { get; private set; }

        CountdownTimer detectionTimer;
        IDetectionStrategy detectionStrategy;

        void Awake() {
            // Find the enemy by tag
            var enemyObject = GameObject.FindGameObjectWithTag("Enemy");

            if (enemyObject != null) {
                Enemy = enemyObject.transform;
                EnemyHealth = enemyObject.GetComponent<Health>();
            } else {
                Debug.LogWarning("Enemy with tag 'Enemy' not found.");
            }
        }

        void Start() {
            detectionTimer = new CountdownTimer(detectionCooldown);
            detectionStrategy = new ConeDetectionStrategy(detectionAngle, detectionRadius, innerDetectionRadius);
        }

        void Update() {
            detectionTimer.Tick(Time.deltaTime);
        }

        public bool CanDetectEnemy() {
            if (Enemy == null) return false; // Ensure we have an enemy to detect
            
            bool canDetect = !detectionTimer.IsRunning && detectionStrategy.Execute(Enemy, transform, detectionTimer);
            
            if (canDetect) {
                Debug.Log($"Enemy detected: {Enemy.name}");
                detectionTimer.Start(); // Restart the cooldown timer after detection
            }

            return canDetect;
        }

        public bool CanAttackEnemy() {
            if (Enemy == null) return false; // Ensure we have an enemy to attack

            Vector3 directionToEnemy = Enemy.position - transform.position;
            return directionToEnemy.magnitude <= attackRange;
        }

        public void SetDetectionStrategy(IDetectionStrategy newStrategy) {
            detectionStrategy = newStrategy;
        }

        void OnDrawGizmos() {
            Gizmos.color = Color.red;

            // Draw spheres for the radii
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            Gizmos.DrawWireSphere(transform.position, innerDetectionRadius);

            // Calculate and draw cone directions for visual debugging
            Vector3 forwardConeDirection = Quaternion.Euler(0, detectionAngle / 2, 0) * transform.forward * detectionRadius;
            Vector3 backwardConeDirection = Quaternion.Euler(0, -detectionAngle / 2, 0) * transform.forward * detectionRadius;

            Gizmos.DrawLine(transform.position, transform.position + forwardConeDirection);
            Gizmos.DrawLine(transform.position, transform.position + backwardConeDirection);
        }
    }
}
