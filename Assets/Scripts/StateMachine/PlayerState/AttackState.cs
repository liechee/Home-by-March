using UnityEngine;

namespace HomeByMarch {
    public class AttackState : BaseState {
        public AttackState(PlayerController player, Animator animator) : base(player, animator) { }

        public override void OnEnter() {
            Debug.Log("AttackState.OnEnter");
            if (player.IsMoving()) {
                animator.CrossFade(AttackHash2, crossFadeDuration);
            } else {
                animator.CrossFade(AttackHash, crossFadeDuration);
            }
            player.Attack();
        }

        public override void FixedUpdate() {
            player.HandleMovement();
        }
    }
}