using UnityEngine;
using UnityEngine.AI;

namespace HomeByMarch {
    public class EnemyWanderState : EnemyBaseState {
        readonly NavMeshAgent agent;
        readonly Vector3 startPoint;
        readonly float wanderRadius;

        public EnemyWanderState(Enemy enemy, Animator animator, NavMeshAgent agent, float wanderRadius) : base(enemy, animator) {
            this.agent = agent;
            this.startPoint = enemy.transform.position;
            this.wanderRadius = wanderRadius;
        }
        
        public override void OnEnter() {
            Debug.Log("Wander");
            animator.CrossFade(WalkHash, crossFadeDuration);
        }

        public override void Update() {
            if (HasReachedDestination()) {
                var randomDirection = Random.insideUnitSphere * wanderRadius;
                randomDirection += startPoint;
                NavMeshHit hit;
                NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, 1);
                var finalPosition = hit.position;
                
                agent.SetDestination(finalPosition);
            }
        }
        
        bool HasReachedDestination() {
            return !agent.pathPending
                   && agent.remainingDistance <= agent.stoppingDistance
                   && (!agent.hasPath || agent.velocity.sqrMagnitude == 0f);
        }
        // readonly NavMeshAgent agent;
        // readonly Transform enemyTransform;
        // readonly float wanderRadius;
        // Vector3 currentDirection;
        // float directionChangeInterval = 4f;
        // float directionTimer = 0f;

        // public EnemyWanderState(Enemy enemy, Animator animator, NavMeshAgent agent, float wanderRadius) : base(enemy, animator) {
        //     this.agent = agent;
        //     this.enemyTransform = enemy.transform;
        //     this.wanderRadius = wanderRadius;
        // }

        // public override void OnEnter() {
        //     Debug.Log("Natural Wander Entered");
        //     animator.CrossFade(WalkHash, crossFadeDuration);
        //     PickNewDirection();
        // }

        // public override void Update() {
        //     directionTimer += Time.deltaTime;

        //     if (HasReachedDestination() || directionTimer >= directionChangeInterval) {
        //         PickNewDirection();
        //         directionTimer = 0f;
        //     }
        // }

        // void PickNewDirection() {
        //     // Add noise to current direction for more organic movement
        //     Vector3 noise = new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));
        //     currentDirection = (enemyTransform.forward + noise).normalized;

        //     // Clamp target within radius
        //     Vector3 potentialTarget = enemyTransform.position + currentDirection * Random.Range(wanderRadius * 0.3f, wanderRadius);
        //     NavMeshHit hit;
        //     if (NavMesh.SamplePosition(potentialTarget, out hit, wanderRadius, NavMesh.AllAreas)) {
        //         agent.SetDestination(hit.position);
        //     }
        // }

        // bool HasReachedDestination() {
        //     return !agent.pathPending &&
        //            agent.remainingDistance <= agent.stoppingDistance &&
        //            (!agent.hasPath || agent.velocity.sqrMagnitude == 0f);
        // }
    }
}