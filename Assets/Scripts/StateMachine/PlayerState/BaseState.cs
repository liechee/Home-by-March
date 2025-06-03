﻿using UnityEngine;

namespace HomeByMarch {
    public abstract class BaseState : IState {
        protected readonly PlayerController player;
        protected readonly Animator animator;
        
        protected static readonly int LocomotionHash = Animator.StringToHash("Locomotion");
        protected static readonly int JumpHash = Animator.StringToHash("Jump");
        // protected static readonly int DashHash = Animator.StringToHash("Dash");
        protected static readonly int AttackHash = Animator.StringToHash("Attack");
        protected static readonly int AttackHash2 = Animator.StringToHash("Attack2");
        protected static readonly int DamageHash = Animator.StringToHash("Damage");
        protected static readonly int DeathHash = Animator.StringToHash("Death");
        
        protected const float crossFadeDuration = 0.1f;
        
        protected BaseState(PlayerController player, Animator animator) {
            this.player = player;
            this.animator = animator;
        }
        
        public virtual void OnEnter() {
            // noop
        }

        public virtual void Update() {
            // noop
        }

        public virtual void FixedUpdate() {
            // noop
        }

        public virtual void OnExit() {
            // noop
        }
    }
}