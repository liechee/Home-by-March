using UnityEngine;
using UnityEngine.UI;

public class ExitDungeon : MonoBehaviour
{
    [Header("UI References")]
    public GameObject exitPanel; // Panel to show on trigger
    public Button confirmButton; // Button to confirm exit
    public Button cancelButton;  // Button to cancel exit

    private void Start()
    {
        // Ensure the panel is hidden at the start
        if (exitPanel != null)
            exitPanel.SetActive(false);

        // Assign button click events
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmExit);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelExit);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Show the panel when the player enters the trigger zone
        if (other.CompareTag("Player") && exitPanel != null)
        {
            exitPanel.SetActive(true);
            Time.timeScale = 0f; // Pause the game
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Hide the panel when the player leaves the trigger zone
        if (other.CompareTag("Player") && exitPanel != null)
        {
            exitPanel.SetActive(false);
            Time.timeScale = 1f; // Resume the game
        }
    }

    private void OnConfirmExit()
    {
        // Handle dungeon exit logic (e.g., load a new scene)
        Debug.Log("Player confirmed exit.");
        Time.timeScale = 1f; // Resume the game
        // Replace "MainMenu" with your desired scene name
        UnityEngine.SceneManagement.SceneManager.LoadScene("Story Screen");
    }

    private void OnCancelExit()
    {
        // Hide the panel and resume the game
        if (exitPanel != null)
            exitPanel.SetActive(false);

        Time.timeScale = 1f; // Resume the game
    }
}
