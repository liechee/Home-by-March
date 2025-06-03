using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class InventoryCameraView : MonoBehaviour
{
    public List<Transform> cameraPositions;
    public Button leftButton;
    public Button rightButton;
    private int currentIndex = 0;

    void Start()
    {
        leftButton.onClick.AddListener(MoveCameraLeft);
        rightButton.onClick.AddListener(MoveCameraRight);
        
        // Set the initial camera position to the first position
        if (cameraPositions.Count > 0)
        {
            currentIndex = 0;
            UpdateCameraPosition();
        }
    }

    void MoveCameraLeft()
    {
        if (cameraPositions.Count == 0) return;

        currentIndex--;
        if (currentIndex < 0)
        {
            currentIndex = cameraPositions.Count - 1;
        }
        Debug.Log("Moving camera left to position: " + currentIndex);
        UpdateCameraPosition();
    }

    void MoveCameraRight()
    {
        if (cameraPositions.Count == 0) return;

        currentIndex++;
        if (currentIndex >= cameraPositions.Count)
        {
            currentIndex = 0;
        }
        Debug.Log("Moving camera right to position: " + currentIndex);
        UpdateCameraPosition();
    }

    void UpdateCameraPosition()
    {
        Debug.Log("Updating camera position to: " + currentIndex);
        transform.position = cameraPositions[currentIndex].position;
        transform.rotation = cameraPositions[currentIndex].rotation;
    }
}