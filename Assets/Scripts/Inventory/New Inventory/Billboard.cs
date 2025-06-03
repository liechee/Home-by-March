using UnityEngine;

public class Billboard : MonoBehaviour
{
    public Camera targetCamera; // The camera to face

    void LateUpdate()
    {
        if (targetCamera != null)
        {
            // Make the canvas face the camera
                   transform.forward = targetCamera.transform.forward;
        }
    }
}


