using UnityEngine;
using UnityEngine.AI;

namespace HomeByMarch {
    public class EnemyDeathState : EnemyBaseState {
        readonly NavMeshAgent agent;
        
        public EnemyDeathState(Enemy enemy, Animator animator, NavMeshAgent agent) : base(enemy, animator) {
            this.agent = agent;
        }
        
        public override void OnEnter() {
            Debug.Log("Death");
            
            // Disable agent movement
            agent.isStopped = true;

            // Play death animation
            animator.CrossFade(DieHash, crossFadeDuration);
            
            // Optionally disable the NavMeshAgent and enemy components
            enemy.enabled = false;
            GameObject.Destroy(enemy.gameObject, 4f); // Disable enemy logic
        }

        public override void Update() {
            // Enemy has died; no need to do anything in the update loop.
        }
        
        // Optionally, handle clean-up or removal from the game after the death animation
        public override void OnExit() {
            // Example: Destroy the enemy after the animation finishes
            // GameObject.Destroy(enemy.gameObject, 1f); // Destroy enemy after 3 seconds
        }
    }
}
