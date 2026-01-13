using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    Rigidbody rb;
    public FixedJoystick joystick;

    [Header("Movement Settings")]
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float rotationSpeed = 15f;
    [SerializeField] float smoothTime = 0.2f;
    [SerializeField] bool useCharacterForward = false;

    Camera mainCam;
    Vector2 inputs;
    Vector3 velocity = Vector3.zero;
    Vector3 targetDirection;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("Main Camera is not assigned or could not be found.");
        }
    }

    void Update()
    {
        // Read joystick inputs each frame
        inputs.x = joystick.Horizontal;
        inputs.y = joystick.Vertical;
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    public void HandleMovement()
    {
        // Calculate movement direction
        UpdateTargetDirection();

        // Calculate the target velocity based on input and move speed
        Vector3 moveDirection = targetDirection * moveSpeed;

        // Preserve the current Y velocity (gravity effect) while applying movement only on X and Z
        Vector3 targetVelocity = new Vector3(moveDirection.x, rb.velocity.y, moveDirection.z);

        // Smooth out the movement for a more fluid transition
        rb.velocity = Vector3.SmoothDamp(rb.velocity, targetVelocity, ref velocity, smoothTime);

        // Handle rotation if the player is moving
        if (inputs != Vector2.zero && targetDirection.magnitude > 0.1f)
        {
            HandleRotation();
        }
    }

    public void UpdateTargetDirection()
    {
        if (!useCharacterForward)
        {
            Vector3 forward = mainCam.transform.TransformDirection(Vector3.forward);
            forward.y = 0;  // Remove vertical component to avoid any unwanted movement up/down
            Vector3 right = mainCam.transform.TransformDirection(Vector3.right);
            targetDirection = inputs.x * right + inputs.y * forward;
        }
        else
        {
            Vector3 forward = transform.TransformDirection(Vector3.forward);
            forward.y = 0;  // Remove vertical component to avoid any unwanted movement up/down
            Vector3 right = transform.TransformDirection(Vector3.right);
            targetDirection = inputs.x * right + Mathf.Abs(inputs.y) * forward;
        }
    }

    void HandleRotation()
    {
        // Rotate towards the movement direction
        Vector3 lookDirection = targetDirection.normalized;
        Quaternion freeRotation = Quaternion.LookRotation(lookDirection, transform.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, freeRotation, rotationSpeed * Time.deltaTime);
    }
}
