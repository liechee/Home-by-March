using UnityEngine;

public class ShakeResponse : MonoBehaviour
{

    [SerializeField]
    public GameObject shakePanel; // Assign your UI panel in the Inspector

    void OnEnable()
    {
        ShakeDetector.OnShakeDetected += HandleShake;
    }

    void OnDisable()
    {
        ShakeDetector.OnShakeDetected -= HandleShake;
    }

    private void HandleShake()
    {
        Debug.Log("Shake detected!");
        if (shakePanel != null)
        {
            shakePanel.SetActive(true); // Show the panel
        }
    }
}
