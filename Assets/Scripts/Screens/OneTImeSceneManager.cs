using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class OneTimeSceneManager : MonoBehaviour
{
    private const string OneTimeSceneKey = "HasSeenOneTimeScene"; // Key for PlayerPrefs
    [SerializeField] private string oneTimeSceneName = "OneTimeScene"; // Name of the one-time scene
    [SerializeField] private string mainSceneName = "MainScene"; // Name of the main scene

    [Header("UI Elements")]
    [SerializeField] private TMP_InputField inputField; // TMP Input field for user text
    [SerializeField] private Button nextSceneButton; // Button to move to the next scene

    void Start()
    {
        // Check if the one-time scene has already been shown
        if (PlayerPrefs.GetInt(OneTimeSceneKey, 0) == 1 && SceneManager.GetActiveScene().name != oneTimeSceneName)
        {
            // Load the main scene directly if the one-time scene has already been shown
            SceneManager.LoadScene(mainSceneName);
            return;
        }

        // Mark the one-time scene as shown
        PlayerPrefs.SetInt(OneTimeSceneKey, 1);
        PlayerPrefs.Save();

        // Set initial button interactability based on input
        UpdateButtonInteractable();

        // Add listeners for TMP input field changes and button click
        if (inputField != null)
        {
            inputField.onValueChanged.AddListener(delegate { UpdateButtonInteractable(); });
        }

        if (nextSceneButton != null)
        {
            nextSceneButton.onClick.AddListener(MoveToMainScene);
        }
    }

    private void UpdateButtonInteractable()
    {
        // Enable the button only if there is text in the TMP input field
        nextSceneButton.interactable = !string.IsNullOrWhiteSpace(inputField.text);
    }

    private void MoveToMainScene()
    {
        // Load the main scene
        SceneManager.LoadScene("Main Tutorial");
    }
}
