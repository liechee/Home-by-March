using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowPlayer : MonoBehaviour
{
    public float speed = 10.0f;
    public float smoothing = 0.1f; // Smoothing factor for animation blending
    public Transform target;
    private Animator animator;
    private float currentSpeed = 0f; // Track current speed for smoother transition

    public float followRange = 10.0f; // Start following when within this range
    public float attackRange = 1.5f;  // Distance at which the enemy attacks
    public float attackCooldown = 1.0f; // Time between attacks
    private bool isAttacking = false;
    private float lastAttackTime = 0f;

    private float distance; // Cache distance to improve performance

    // Start is called before the first frame update
    void Start()
    {
        // Get the Animator component
        animator = GetComponent<Animator>();

        // Optional: Set target based on mobile device resolution or specific settings
        Application.targetFrameRate = 60; // Optimize for mobile, aim for 60 FPS
    }

    // Use FixedUpdate for smoother physics-based movement
    void FixedUpdate()
    {
        // Calculate the distance to the player (target)
        distance = Vector3.Distance(transform.position, target.position);

        // Check if within follow range but not close enough to attack
        if (distance <= followRange && distance > attackRange && !isAttacking)
        {
            // Move and rotate towards the target
            MoveTowardsTarget();
        }
        else if (distance <= attackRange && Time.time - lastAttackTime > attackCooldown)
        {
            // Attack if in range
            Attack();
        }
        else
        {
            // Stop moving (set speed to 0)
            currentSpeed = 0f;
            animator.SetFloat("Speed", currentSpeed);
        }
    }

    void MoveTowardsTarget()
    {
        // Calculate movement and move towards the target
        Vector3 direction = (target.position - transform.position).normalized;
        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.fixedDeltaTime);

        // Rotate to face the target
        if (direction != Vector3.zero)
        {
            Quaternion toRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, toRotation, smoothing);
        }

        // Smoothly transition the speed value for animation
        float targetSpeed = distance > 0.1f ? speed : 0f;
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, smoothing);

        // Set the animator's "Speed" parameter to control the blend tree
        animator.SetFloat("Speed", currentSpeed);
    }

    void Attack()
    {
        // Trigger attack animation (set "Attack" trigger in Animator)
        isAttacking = true;
        animator.SetTrigger("Attack");

        // Stop movement while attacking
        currentSpeed = 0f;
        animator.SetFloat("Speed", currentSpeed);

        // Set last attack time and reset attacking state after cooldown
        lastAttackTime = Time.time;

        // Delay attack for the cooldown period
        StartCoroutine(EndAttack());
    }

    IEnumerator EndAttack()
    {
        // Wait for the attack animation duration or cooldown period
        yield return new WaitForSeconds(attackCooldown);
        isAttacking = false;
    }
}
