using System.Collections.Generic;
using Cinemachine;
using KBCore.Refs;
using UnityEngine;
using Utilities;
using Photon.Pun;
using System.Collections;

namespace HomeByMarch
{
    public class PlayerController : ValidatedMonoBehaviour
    {
        [Header("References")]
        Rigidbody m_Body; // Rigidbody reference
        public FixedJoystick joystick;
        [SerializeField, Self] Animator animator;
        [SerializeField, Anywhere] InputReader input;
        [SerializeField] PlayerData playerData; // Add reference to PlayerData

        [Header("Movement Settings")]
        [SerializeField] float moveSpeed = 6f;
        [SerializeField] float rotationSpeed = 15f;
        [SerializeField] float smoothTime = 0.2f;
        [SerializeField] bool useCharacterForward = false;
        [SerializeField] float turnSpeed = 10f;
        [SerializeField] private Health health;
        [Header("SFX")]
        [SerializeField] public SFXManager SFXManager;

        Vector3 networkPosition;
        Quaternion networkRotation;

        [Header("Jump Settings")]
        [SerializeField] float jumpForce = 10f;
        [SerializeField] float jumpDuration = 0.5f;
        [SerializeField] float jumpCooldown = 0f;
        [SerializeField] float gravityMultiplier = 3f;

        [Header("Dash Settings")]
        [SerializeField] float dashForce = 10f;
        [SerializeField] float dashDuration = 1f;
        [SerializeField] float dashCooldown = 2f;

        [Header("Attack Settings")]
        [SerializeField] public float attackCooldown = 0.1f;
        [SerializeField] public float attackDistance = 30f;
        [SerializeField] public int attackDelay = 2;

        [SerializeField] BlueSlash[] blueSlashSpells;

        [SerializeField] SpellStrategy[] spells;
        public LayerMask attackLayer;

        bool attacking = false;
        bool readyToAttack = true;
        int attackCount;

        const float ZeroF = 0f;

        Transform mainCam;
        Vector2 inputs;
        float currentSpeed;
        float velocity;
        float turnSpeedMultiplier;
        float jumpVelocity;
        float dashVelocity = 1f;
        float speed;
        Camera mainCamera;
        Vector3 targetDirection;
        List<Timer> timers;
        CountdownTimer jumpTimer;
        CountdownTimer jumpCooldownTimer;
        CountdownTimer dashTimer;
        CountdownTimer dashCooldownTimer;
        CountdownTimer attackTimer;

        StateMachine stateMachine;

        // Animator parameters
        static readonly int Speed = Animator.StringToHash("Speed");

        Transform Enemy;
        Health EnemyHealth;

        void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
            mainCamera = Camera.main;
            attackLayer = LayerMask.GetMask("Enemy");
            if (mainCamera == null)
            {
                Debug.LogError("Main Camera is not assigned or could not be found.");
            }

            SetupTimers();
            SetupStateMachine();
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                // Send data for local player
                stream.SendNext(m_Body.position);
                stream.SendNext(m_Body.rotation);
                stream.SendNext(m_Body.velocity);
            }
            else
            {
                // Receive data for remote players
                networkPosition = (Vector3)stream.ReceiveNext();
                networkRotation = (Quaternion)stream.ReceiveNext();
                m_Body.velocity = (Vector3)stream.ReceiveNext();

                // Apply lag compensation
                float lag = Mathf.Abs((float)(PhotonNetwork.Time - info.timestamp));
                networkPosition += m_Body.velocity * lag;
            }
        }

        void SetupStateMachine()
        {
            // State Machine
            stateMachine = new StateMachine();

            // Declare states
            var locomotionState = new LocomotionState(this, animator);
            var attackState = new AttackState(this, animator);
            var deathState = new DeathState(this, animator);

            // Define transitions
            At(locomotionState, attackState, new FuncPredicate(() => attackTimer.IsRunning));
            At(attackState, locomotionState, new FuncPredicate(() => !attackTimer.IsRunning));
            Any(deathState, new FuncPredicate(() => IsDead()));

            // Set initial state
            stateMachine.SetState(locomotionState);
        }

        // Add this method to check if the player is dead
        public bool IsDead()
        {
            return health != null && health.CurrentHealth <= 0;
        }
        bool ReturnToLocomotionState()
        {
            return !attackTimer.IsRunning;
        }

        void SetupTimers()
        {
            // Setup timers
            jumpTimer = new CountdownTimer(jumpDuration);
            jumpCooldownTimer = new CountdownTimer(jumpCooldown);

            jumpTimer.OnTimerStart += () => jumpVelocity = jumpForce;
            jumpTimer.OnTimerStop += () => jumpCooldownTimer.Start();

            dashTimer = new CountdownTimer(dashDuration);
            dashCooldownTimer = new CountdownTimer(dashCooldown);

            dashTimer.OnTimerStart += () => dashVelocity = dashForce;
            dashTimer.OnTimerStop += () =>
            {
                dashVelocity = 1f;
                dashCooldownTimer.Start();
            };

            attackTimer = new CountdownTimer(attackCooldown);

            timers = new(5) { jumpTimer, jumpCooldownTimer, dashTimer, dashCooldownTimer, attackTimer };
        }

        void At(IState from, IState to, IPredicate condition) => stateMachine.AddTransition(from, to, condition);
        void Any(IState to, IPredicate condition) => stateMachine.AddAnyTransition(to, condition);

        void Start()
        {
            // Set Photon network settings
            PhotonNetwork.SendRate = 30; // Higher send rate for better responsiveness
            PhotonNetwork.SerializationRate = 60; // Balance between performance and update frequency

            // Enable player actions
            input.EnablePlayerActions();
        }

        void OnEnable()
        {
            input.Attack += OnAttack;
            health.OnDamageTaken += CheckHealth; // Subscribe to health damage event
        }

        void OnDisable()
        {
            input.Attack -= OnAttack;
            health.OnDamageTaken -= CheckHealth; // Unsubscribe from health damage event
        }

        void CheckHealth()
        {
            if (health.IsDead)
            {
                Die(); // Call the Die method when health reaches 0
            }
        }

        public void Die()
        {
            // Logic to handle player death
            stateMachine.SetState(new DeathState(this, animator));

            health.ShowDeathPanel();

            // Start a coroutine to delay Time.timeScale
            StartCoroutine(DelayTimeScale());
        }

        private IEnumerator DelayTimeScale()
        {
            // Wait for 4 seconds before changing time scale
            yield return new WaitForSeconds(4f);

            Time.timeScale = 0f; // Set Time.timeScale after the delay
        }

        // void CastSpell(int index)
        // {
        //     // spells[index].CastSpell(transform);
        //     Debug.Log("spellcasted");
        // }

        void OnAttack()
        {
            if (!attackTimer.IsRunning)
            {
                attackTimer.Start();
                Attack(); // Call the Attack method when the attack is performed
            }
        }

        public void Attack()
        {
            if (!readyToAttack) return; // Only proceed if we are ready to attack

            // Start the attack timer and initiate the attack logic
            readyToAttack = false;
            attacking = true;

            // Call the raycast to check for enemies and deal damage
            AttackRayCast();

            // Activate BlueSlash spell(s)
            ActivateBlueSlash();

            //Activate Sound effects

            SFXManager.PlaySFX(SoundTypes.SwordSwing);

            // Reset the attack state after the attack cooldown
            Invoke(nameof(ResetAttack), attackCooldown);
        }

        void ActivateBlueSlash()
        {
            foreach (var slash in blueSlashSpells)
            {
                if (slash != null)
                {
                    slash.CastSpell(transform); // Use the player's transform as the origin
                }
            }
        }

        void ResetAttack()
        {
            readyToAttack = true;
            attacking = false;
        }

        void AttackRayCast()
        {
            Debug.Log("AttackRayCast initiated");

            // Adjust the ray origin and direction
            Vector3 rayOrigin = transform.position + Vector3.up * 1f; // Adjust for the player's height if necessary

            // Define ray directions (center, left, right, up, down, and diagonals)
            Vector3[] rayDirections = new Vector3[10]
            {
                transform.forward,                  // Straight ahead
                transform.forward + transform.right, // Slightly to the right
                transform.forward - transform.right, // Slightly to the left
                transform.forward + transform.up,    // Slightly upwards
                transform.forward - transform.up,    // Slightly downwards
                transform.forward + transform.right + transform.up, // Diagonal up-right
                transform.forward + transform.right - transform.up, // Diagonal down-right
                transform.forward - transform.right + transform.up, // Diagonal up-left
                transform.forward - transform.right - transform.up, // Diagonal down-left
                transform.forward + transform.up * 2, // More upwards
            };

            // Debug the raycasts by drawing them in the scene view
            foreach (var direction in rayDirections)
            {
                Vector3 endPoint = rayOrigin + direction * attackDistance;
                Debug.DrawLine(rayOrigin, endPoint, Color.blue, 6f);
            }

            // Perform the raycasts
            foreach (var direction in rayDirections)
            {
                if (Physics.Raycast(rayOrigin, direction, out RaycastHit hit, attackDistance, attackLayer))
                {
                    Debug.Log($"Hit object: {hit.transform.name} at position: {hit.point}");

                    // Hit an enemy, apply damage and knockback
                    if (hit.transform.TryGetComponent<Enemy>(out Enemy enemyComponent))
                    {
                        // Apply damage to the enemy
                        int attackDamage = playerData.currentAttack; // Use attackDamage from playerData
                        enemyComponent.TakeDamage(attackDamage);
                        Debug.Log($"Enemy {enemyComponent.name} took {attackDamage} damage.");

                        // Apply knockback force
                        Rigidbody enemyRigidbody = hit.transform.GetComponent<Rigidbody>();
                        if (enemyRigidbody != null)
                        {
                            Vector3 knockbackDirection = (hit.transform.position - transform.position).normalized;
                            knockbackDirection.y = 0; // Optional: Ignore vertical knockback
                            enemyRigidbody.AddForce(knockbackDirection * attackDamage * 5f, ForceMode.Impulse); // Adjust force multiplier as needed
                            Debug.Log($"Enemy {enemyComponent.name} knocked back.");
                        }
                        else
                        {
                            Debug.LogWarning("Hit object does not have a Rigidbody for knockback.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Hit object is not an enemy.");
                    }
                }
                else
                {
                    Debug.LogWarning("Raycast did not hit any objects.");
                }
            }
        }

        void HitTarget(Vector3 pos)
        {
            Debug.Log("Hit target");
            // audioSource.pitch = 1;
            // audioSource.PlayOneShot(hitSound);

            // GameObject GO = Instantiate(hitEffect, pos, Quaternion.identity);
            // Destroy(GO, 20);
        }

        void Update()
        {
            if (GetComponent<PhotonView>().IsMine)
            {
                // Local player updates
                stateMachine.Update();
                HandleTimers();
            }
            else
            {
                // Remote player updates
                m_Body.position = Vector3.Lerp(m_Body.position, networkPosition, Time.deltaTime * 10f);
                m_Body.rotation = Quaternion.Lerp(m_Body.rotation, networkRotation, Time.deltaTime * 10f);
            }
        }

        public bool IsMoving()
        {
            return m_Body.velocity.magnitude > 0.1f; // Example condition for moving
        }

        void FixedUpdate()
        {
            if (GetComponent<PhotonView>().IsMine == true)
            {
                // Debug.Log("PlayerController Is me");
#if ENABLE_LEGACY_INPUT_MANAGER
                inputs.x = joystick.Horizontal;
                inputs.y = joystick.Vertical;

                stateMachine.FixedUpdate();
#else
                InputSystemHelper.EnableBackendsWarningMessage();
#endif
            }
        }

        void UpdateAnimator()
        {
            animator.SetFloat(Speed, currentSpeed);
        }

        void HandleTimers()
        {
            foreach (var timer in timers)
            {
                timer.Tick(Time.deltaTime);
            }
        }

        public void HandleMovement()
        {
            if (useCharacterForward)
            {
                speed = Mathf.Abs(inputs.x) + inputs.y;
            }
            else
            {
                speed = Mathf.Abs(inputs.x) + Mathf.Abs(inputs.y);
            }

            speed = Mathf.Clamp(speed, 0f, 1f);
            speed = Mathf.SmoothDamp(animator.GetFloat("Speed"), speed, ref velocity, 0.1f);
            animator.SetFloat("Speed", speed);

            UpdateTargetDirection();

            Vector3 moveDirection = new Vector3(inputs.x * moveSpeed, m_Body.velocity.y, inputs.y * moveSpeed);
            m_Body.velocity = moveDirection;

            if (inputs != Vector2.zero && targetDirection.magnitude > 0.1f)
            {
                HandleRotation();
            }
        }

        public void UpdateTargetDirection()
        {
            if (!useCharacterForward)
            {
                turnSpeedMultiplier = 1f;
                Vector3 forward = mainCamera.transform.TransformDirection(Vector3.forward);
                forward.y = 0;
                Vector3 right = mainCamera.transform.TransformDirection(Vector3.right);
                targetDirection = (forward * inputs.y) + (right * inputs.x);
            }
            else
            {
                targetDirection = transform.forward;
            }
        }

        public void HandleRotation()
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed * turnSpeedMultiplier);
        }
    }
}