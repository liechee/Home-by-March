using UnityEngine;

public class CapsuleController : MonoBehaviour
{
    [Header("References")]
    public Rigidbody rb;
    public Animator animator;

    [Header("Movement Settings")]
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float rotationSpeed = 15f;

    Vector2 inputs;
    float currentSpeed;
    Vector3 targetDirection;

    // Animator parameters
    static readonly int Speed = Animator.StringToHash("Speed");

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        HandleInput();
        HandleMovement();
    }

    void FixedUpdate()
    {
        ApplyMovement();
    }

    void HandleInput()
    {
        inputs.x = Input.GetAxis("Horizontal");
        inputs.y = Input.GetAxis("Vertical");
    }

    void HandleMovement()
    {
        currentSpeed = Mathf.Clamp01(Mathf.Abs(inputs.x) + Mathf.Abs(inputs.y));
        animator.SetFloat(Speed, currentSpeed);

        UpdateTargetDirection();

        if (inputs != Vector2.zero && targetDirection.magnitude > 0.1f)
        {
            HandleRotation();
        }
    }

    void UpdateTargetDirection()
    {
        Vector3 forward = Camera.main.transform.TransformDirection(Vector3.forward);
        forward.y = 0;
        Vector3 right = Camera.main.transform.TransformDirection(Vector3.right);
        targetDirection = inputs.x * right + inputs.y * forward;
    }

    void HandleRotation()
    {
        Vector3 lookDirection = targetDirection.normalized;
        Quaternion freeRotation = Quaternion.LookRotation(lookDirection, transform.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, freeRotation, rotationSpeed * Time.deltaTime);
    }

    void ApplyMovement()
    {
        Vector3 moveDirection = new Vector3(inputs.x * moveSpeed, rb.velocity.y, inputs.y * moveSpeed);
        rb.velocity = moveDirection;
    }
}