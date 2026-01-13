using UnityEngine;
using UnityEngine.AI;

namespace HomeByMarch {
    public class EnemyOnHitState : EnemyBaseState {
        private readonly NavMeshAgent agent;
        private readonly Animator animator;

        public EnemyOnHitState(Enemy enemy, Animator animator, NavMeshAgent agent) : base(enemy, animator) {
            this.agent = agent;
            this.animator = animator;
        }

        public override void OnEnter() {
            Debug.Log("Enemy hit");
            animator.CrossFade(HitHash, crossFadeDuration);// Play hit animation
            agent.isStopped = true; // Stop enemy movement
        }

        public override void Update() {
            // Additional logic during hit can be added here, if needed
        }

        public override void OnExit() {
            agent.isStopped = false; // Resume movement when exiting the hit state
        }
    }
}
