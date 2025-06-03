    // using System.Collections;
    // using UnityEngine;
    // using UnityEngine.UI;

    // namespace HomeByMarch
    // {
    //     [AddComponentMenu("")]
    //     [RequireComponent(typeof(Rigidbody))]
    //     public class PlayerControllers : MonoBehaviour
    //     {
    //         [Header("Input Settings")]
    //         [SerializeField] FixedJoystick joystick;
    //         [SerializeField] KeyCode sprintJoystick = KeyCode.JoystickButton2;
    //         [SerializeField] KeyCode sprintKeyboard = KeyCode.Space;

    //         [Header("UI Elements")]
    //         [SerializeField] Button attackButton;

    //         [Header("Movement Settings")]
    //         [SerializeField] float moveSpeed = 5f;
    //         [SerializeField] float turnSpeed = 10f;
    //         [SerializeField] bool useCharacterForward = false;

    //         [Header("Sound Settings")]
    //         [SerializeField] WalkingSound walkingSound;
    //         [SerializeField] float soundCooldown = 0.5f;
    //         [SerializeField] float walkSpeedThreshold = 0.1f;

    //         Rigidbody rb;
    //         Animator animator;
    //         Camera mainCamera;
    //         Vector3 targetDirection;

    //         bool isSprinting = false;
    //         float speed;
    //         float turnSpeedMultiplier;
    //         float velocity;
    //         float soundTimer = 0f;

    //         Vector2 input;
    //         StateMachine stateMachine;
    //         public bool isAttacking { get; set; } = false;

    //         float attackDuration = 1f;  // Duration of the attack in seconds

    //         void Awake()
    //         {
    //             rb = GetComponent<Rigidbody>();
    //             animator = GetComponent<Animator>();
    //             mainCamera = Camera.main;

    //             if (walkingSound == null)
    //             {
    //                 walkingSound = GetComponent<WalkingSound>();
    //             }

    //             if (rb == null || animator == null || mainCamera == null)
    //             {
    //                 Debug.LogError("One of the required components is not assigned or missing!");
    //             }

    //             stateMachine = new StateMachine();
    //             var locomotionState = new LocomotionState(this, animator);
    //             var sprintState = new JumpState(this, animator);
    //             var attackState = new AttackState(this, animator, attackDuration);

    //             At(locomotionState, sprintState, new FuncPredicate(() => isSprinting));
    //             At(sprintState, locomotionState, new FuncPredicate(() => !isSprinting));
    //             At(locomotionState, attackState, new FuncPredicate(() => isAttacking));
    //             At(attackState, locomotionState, new FuncPredicate(() => !isAttacking));
    //             Any(locomotionState, new FuncPredicate(ReturnToLocomotionState));

    //             stateMachine.SetState(locomotionState);

    //             if (attackButton != null)
    //             {
    //                 attackButton.onClick.AddListener(OnAttackButtonClicked);
    //             }
    //             else
    //             {
    //                 Debug.LogError("Attack button is not assigned in the inspector!");
    //             }
    //         }

    //         void At(IState from, IState to, IPredicate condition) => stateMachine.AddTransition(from, to, condition);
    //         void Any(IState to, IPredicate condition) => stateMachine.AddAnyTransition(to, condition);

    //         void FixedUpdate()
    //         {
    // #if ENABLE_LEGACY_INPUT_MANAGER
    //             input.x = joystick.Horizontal;
    //             input.y = joystick.Vertical;

    //             stateMachine.FixedUpdate();
    //             HandleWalkingSound();

    //             isSprinting = (Input.GetKey(sprintJoystick) || Input.GetKey(sprintKeyboard)) && input != Vector2.zero;
    //             animator.SetBool("isSprinting", isSprinting);
    // #else
    //             InputSystemHelper.EnableBackendsWarningMessage();
    // #endif
    //         }
    //         bool ReturnToLocomotionState() {
    //             return !isAttacking;
    //         }

    //         public void HandleMovement()
    //         {
    //             if (useCharacterForward)
    //             {
    //                 speed = Mathf.Abs(input.x) + input.y;
    //             }
    //             else
    //             {
    //                 speed = Mathf.Abs(input.x) + Mathf.Abs(input.y);
    //             }

    //             speed = Mathf.Clamp(speed, 0f, 1f);
    //             speed = Mathf.SmoothDamp(animator.GetFloat("Speed"), speed, ref velocity, 0.1f);
    //             animator.SetFloat("Speed", speed);

    //             UpdateTargetDirection();

    //             Vector3 moveDirection = new Vector3(input.x * moveSpeed, rb.velocity.y, input.y * moveSpeed);
    //             rb.velocity = moveDirection;

    //             if (input != Vector2.zero && targetDirection.magnitude > 0.1f)
    //             {
    //                 HandleRotation();
    //             }
    //         }

    //         void HandleWalkingSound()
    //         {
    //             if (speed > walkSpeedThreshold && walkingSound != null)
    //             {
    //                 soundTimer += Time.deltaTime;
    //                 if (soundTimer >= soundCooldown)
    //                 {
    //                     walkingSound.playSound();
    //                     soundTimer = 0f;
    //                 }
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
    //             if (differenceRotation != 0)
    //             {
    //                 eulerY = freeRotation.eulerAngles.y;
    //             }

    //             Vector3 euler = new Vector3(0, eulerY, 0);
    //             transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(euler), turnSpeed * turnSpeedMultiplier * Time.deltaTime);
    //         }

    //         public void UpdateTargetDirection()
    //         {
    //             if (!useCharacterForward)
    //             {
    //                 turnSpeedMultiplier = 1f;
    //                 Vector3 forward = mainCamera.transform.TransformDirection(Vector3.forward);
    //                 forward.y = 0;
    //                 Vector3 right = mainCamera.transform.TransformDirection(Vector3.right);
    //                 targetDirection = input.x * right + input.y * forward;
    //             }
    //             else
    //             {
    //                 turnSpeedMultiplier = 0.2f;
    //                 Vector3 forward = transform.TransformDirection(Vector3.forward);
    //                 forward.y = 0;
    //                 Vector3 right = transform.TransformDirection(Vector3.right);
    //                 targetDirection = input.x * right + Mathf.Abs(input.y) * forward;
    //             }
    //         }

    //         // Attack button handler
    //         public void OnAttackButtonClicked()
    //         {
    //             if (!isAttacking)
    //             {
    //                 isAttacking = true;
    //                 StartCoroutine(HandleAttackTimer());
    //             }
    //         }

    //         // Coroutine to handle attack timer
    //         private IEnumerator HandleAttackTimer()
    //         {
    //             stateMachine.SetState(new AttackState(this, animator, attackDuration));

    //             float remainingTime = attackDuration;

    //             // Loop through the countdown, logging the time remaining
    //             while (remainingTime > 0f)
    //             {
    //                 Debug.Log($"Attack ends in: {remainingTime} seconds");
    //                 yield return new WaitForSeconds(0.1f);  // Log every 0.1 seconds
    //                 remainingTime -= 0.1f;  // Reduce the remaining time
    //             }

    //             // When the attack is done, set attacking to false
    //             isAttacking = false;
    //             Debug.Log("Attack finished");
    //         }

    //     }
    // }
