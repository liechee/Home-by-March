using UnityEngine;
using System.Collections;

public class ShakeDetector : MonoBehaviour
{
    // Adjustable threshold for shake detection
    public float shakeThreshold = 2.0f;
    // Minimum interval between shake detections (in seconds)
    public float shakeCooldown = 0.5f;

    private Vector3 lastAcceleration;
    private float shakeTimer = 0.0f;

    // Event to trigger when shake is detected
    public delegate void ShakeAction();
    public static event ShakeAction OnShakeDetected;

    void Start()
    {
        // Initialize last acceleration
        lastAcceleration = Input.acceleration;
    }

    void Update()
    {
        // Update shake timer
        shakeTimer += Time.deltaTime;

        // Get current acceleration
        Vector3 currentAcceleration = Input.acceleration;

        // Calculate the change in acceleration (delta)
        Vector3 deltaAcceleration = currentAcceleration - lastAcceleration;

        // Check if the delta exceeds the shake threshold and cooldown period
        if (deltaAcceleration.magnitude > shakeThreshold && shakeTimer > shakeCooldown)
        {
            // Shake detected, reset timer
            shakeTimer = 0.0f;

            // Trigger shake event
            OnShakeDetected?.Invoke();

            // Trigger phone vibration
            VibratePhone();
        }

        // Update last acceleration
        lastAcceleration = currentAcceleration;
    }

    void VibratePhone()
    {
        // Check if the device supports vibration
        if (SystemInfo.supportsVibration)
        {
            // Vibrate the phone for a short duration
            Handheld.Vibrate();
        }
    }
}