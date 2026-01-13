using UnityEngine;

namespace HomeByMarch {
    public class DamageState : BaseState {
        public DamageState(PlayerController player, Animator animator) : base(player, animator) { }

        public override void OnEnter() {
            Debug.Log("DamageState.OnEnter");
            animator.CrossFade(DamageHash, crossFadeDuration); // Play damage animation
            // player.rb.velocity = Vector3.zero; // Stop movement when taking damage
        }

        public override void FixedUpdate() {
            // During damage, prevent movement by overriding HandleMovement
            player.HandleMovement(); // Optionally reduce movement speed or disable entirely
        }

        public override void OnExit() {
            Debug.Log("DamageState.OnExit");
        }
    }
}
