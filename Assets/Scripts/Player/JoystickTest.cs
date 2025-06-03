using UnityEngine;

public class JoystickTest : MonoBehaviour
{
    public Joystick joystick;

    void Update()
    {
        Debug.Log($"Joystick Horizontal: {joystick.Horizontal}, Vertical: {joystick.Vertical}");
    }
}