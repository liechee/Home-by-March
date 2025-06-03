// using System.Collections.Generic;
// using Cinemachine;
// using KBCore.Refs;
// using UnityEngine;
// using Utilities;

// namespace HomeByMarch
// {
//     public class PlayerController : ValidatedMonoBehaviour
//     {
//         [Header("References")]
//         Rigidbody rb;
//         [SerializeField] FixedJoystick joystick;
//         // [SerializeField, Self] GroundChecker groundChecker;
//         [SerializeField, Self] Animator animator;
//         [SerializeField, Anywhere] InputReader input;

//         [Header("Movement Settings")]
//         [SerializeField] float moveSpeed = 6f;
//         [SerializeField] float rotationSpeed = 15f;
//         [SerializeField] float smoothTime = 0.2f;
//         [SerializeField] bool useCharacterForward = false;

//         [SerializeField] float turnSpeed = 10f;

//         [Header("Jump Settings")]
//         [SerializeField] float jumpForce = 10f;
//         [SerializeField] float jumpDuration = 0.5f;
//         [SerializeField] float jumpCooldown = 0f;
//         [SerializeField] float gravityMultiplier = 3f;

//         [Header("Dash Settings")]
//         [SerializeField] float dashForce = 10f;
//         [SerializeField] float dashDuration = 1f;
//         [SerializeField] float dashCooldown = 2f;

//         [Header("Attack Settings")]
//         [SerializeField] float attackCooldown = 2f;
//         [SerializeField] float attackDistance = 1f;
//         [SerializeField] int attackDamage = 10;

//         const float ZeroF = 0f;

//         Transform mainCam;
//         Vector2 inputs;
//         float currentSpeed;
//         float velocity;
//         float turnSpeedMultiplier;
//         float jumpVelocity;
//         float dashVelocity = 1f;
//         float speed;
//         Camera mainCamera;
//         Vector3 targetDirection;
//         List<Timer> timers;
//         CountdownTimer jumpTimer;
//         CountdownTimer jumpCooldownTimer;
//         CountdownTimer dashTimer;
//         CountdownTimer dashCooldownTimer;
//         CountdownTimer attackTimer;

//         StateMachine stateMachine;

//         // Animator parameters
//         static readonly int Speed = Animator.StringToHash("Speed");

//         void Awake()
//         {
//             rb = GetComponent<Rigidbody>();
//             mainCamera = Camera.main;

//             SetupTimers();
//             SetupStateMachine();
//         }

//         void SetupStateMachine()
//         {
//             // State Machine
//             stateMachine = new StateMachine();

//             // Declare states
//             var locomotionState = new LocomotionState(this, animator);
//             // var jumpState = new JumpState(this, animator);
//             // var dashState = new DashState(this, animator);
//             var attackState = new AttackState(this, animator);

//             // Define transitions
//             // At(locomotionState, jumpState, new FuncPredicate(() => jumpTimer.IsRunning));
//             // At(locomotionState, dashState, new FuncPredicate(() => dashTimer.IsRunning));
//             At(locomotionState, attackState, new FuncPredicate(() => attackTimer.IsRunning));
//             At(attackState, locomotionState, new FuncPredicate(() => !attackTimer.IsRunning));
//             Any(locomotionState, new FuncPredicate(ReturnToLocomotionState));

//             // Set initial state
//             stateMachine.SetState(locomotionState);
//         }

//         bool ReturnToLocomotionState()
//         {
//             return !attackTimer.IsRunning;
//         }

//         void SetupTimers()
//         {
//             // Setup timers
//             jumpTimer = new CountdownTimer(jumpDuration);
//             jumpCooldownTimer = new CountdownTimer(jumpCooldown);

//             jumpTimer.OnTimerStart += () => jumpVelocity = jumpForce;
//             jumpTimer.OnTimerStop += () => jumpCooldownTimer.Start();

//             dashTimer = new CountdownTimer(dashDuration);
//             dashCooldownTimer = new CountdownTimer(dashCooldown);

//             dashTimer.OnTimerStart += () => dashVelocity = dashForce;
//             dashTimer.OnTimerStop += () =>
//             {
//                 dashVelocity = 1f;
//                 dashCooldownTimer.Start();
//             };

//             attackTimer = new CountdownTimer(attackCooldown);

//             timers = new(5) { jumpTimer, jumpCooldownTimer, dashTimer, dashCooldownTimer, attackTimer };
//         }

//         void At(IState from, IState to, IPredicate condition) => stateMachine.AddTransition(from, to, condition);
//         void Any(IState to, IPredicate condition) => stateMachine.AddAnyTransition(to, condition);

//         void Start() => input.EnablePlayerActions();

//         void OnEnable()
//         {
//             input.Dash += OnDash;
//             input.Attack += OnAttack;
//         }

//         void OnDisable()
//         {
//             input.Attack -= OnAttack;
//         }

//         void OnAttack()
//         {
//             if (!attackTimer.IsRunning)
//             {
//                 attackTimer.Start();
//             }
//         }

//         public void Attack()
//         {
//             // Null check for Rigidbody and Animator
//             if (rb == null || animator == null)
//             {
//                 Debug.LogError("Required components (Rigidbody or Animator) are missing!");
//                 return;
//             }

//             // Get attack position
//             Vector3 attackPos = transform.position + transform.forward;
//             Collider[] hitEnemies = Physics.OverlapSphere(attackPos, attackDistance);

//             // Handle enemy collision
//             foreach (var enemy in hitEnemies)
//             {
//                 Debug.Log(enemy.name);
//                 // Null check for Health component
//                 var health = enemy.GetComponent<Health>();
//                 if (health != null)
//                 {
//                     health.TakeDamage(attackDamage);
//                 }
//                 else
//                 {
//                     Debug.LogWarning($"{enemy.name} does not have a Health component!");
//                 }
//             }
//         }

//         void OnDash(bool performed)
//         {
//             if (performed && !dashTimer.IsRunning && !dashCooldownTimer.IsRunning)
//             {
//                 dashTimer.Start();
//             }
//             else if (!performed && dashTimer.IsRunning)
//             {
//                 dashTimer.Stop();
//             }
//         }

//         void Update()
//         {
//             stateMachine.Update();

//             HandleTimers();
//             // UpdateAnimator();
//         }

//         void FixedUpdate()
//         {
// #if ENABLE_LEGACY_INPUT_MANAGER
//             inputs.x = joystick.Horizontal;
//             inputs.y = joystick.Vertical;

//             stateMachine.FixedUpdate();
// #else
//             InputSystemHelper.EnableBackendsWarningMessage();
// #endif
//         }

//         void UpdateAnimator()
//         {
//             animator.SetFloat(Speed, currentSpeed);
//         }

//         void HandleTimers()
//         {
//             foreach (var timer in timers)
//             {
//                 timer.Tick(Time.deltaTime);
//             }
//         }

//         public void HandleMovement()
//         {
//             if (useCharacterForward)
//             {
//                 speed = Mathf.Abs(inputs.x) + inputs.y;
//             }
//             else
//             {
//                 speed = Mathf.Abs(inputs.x) + Mathf.Abs(inputs.y);
//             }

//             speed = Mathf.Clamp(speed, 0f, 1f);
//             speed = Mathf.SmoothDamp(animator.GetFloat("Speed"), speed, ref velocity, 0.1f);
//             animator.SetFloat("Speed", speed);

//             UpdateTargetDirection();

//             Vector3 moveDirection = new Vector3(inputs.x * moveSpeed, rb.velocity.y, inputs.y * moveSpeed);
//             rb.velocity = moveDirection;

//             if (inputs != Vector2.zero && targetDirection.magnitude > 0.1f)
//             {
//                 HandleRotation();
//             }
//         }

//         public void UpdateTargetDirection()
//         {
//             if (!useCharacterForward)
//             {
//                 turnSpeedMultiplier = 1f;
//                 Vector3 forward = mainCamera.transform.TransformDirection(Vector3.forward);
//                 forward.y = 0;
//                 Vector3 right = mainCamera.transform.TransformDirection(Vector3.right);
//                 targetDirection = inputs.x * right + inputs.y * forward;
//             }
//             else
//             {
//                 turnSpeedMultiplier = 0.2f;
//                 Vector3 forward = transform.TransformDirection(Vector3.forward);
//                 forward.y = 0;
//                 Vector3 right = transform.TransformDirection(Vector3.right);
//                 targetDirection = inputs.x * right + Mathf.Abs(inputs.y) * forward;
//             }
//         }

//         void HandleRotation()
//         {
//             Vector3 lookDirection = targetDirection.normalized;
//             Quaternion freeRotation = Quaternion.LookRotation(lookDirection, transform.up);
//             float differenceRotation = freeRotation.eulerAngles.y - transform.eulerAngles.y;
//             float eulerY = transform.eulerAngles.y;

//             if (differenceRotation != 0)
//             {
//                 eulerY = freeRotation.eulerAngles.y;
//             }

//             Vector3 euler = new Vector3(0, eulerY, 0);
//             transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(euler), turnSpeed * turnSpeedMultiplier * Time.deltaTime);
//         }
//     }
// }
