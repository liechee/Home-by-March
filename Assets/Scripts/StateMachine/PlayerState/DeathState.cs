using UnityEngine;

namespace HomeByMarch
{
    public class DeathState : IState
    {
        private readonly PlayerController playerController;
        private readonly Animator animator;
        private static readonly int DieHash = Animator.StringToHash("Death");

        public DeathState(PlayerController playerController, Animator animator)
        {
            this.playerController = playerController;
            this.animator = animator;
        }

        public void OnEnter()
        {
            Debug.Log("Entering Death State");
            animator.CrossFade(DieHash, 0.1f);
        }

        public void OnExit()
        {
            Debug.Log("Exiting Death State");
           
        }

        public void Update()
        {
            // No update logic needed for death state
        }

        public void FixedUpdate()
        {
            // No fixed update logic needed for death state
        }
    }
}