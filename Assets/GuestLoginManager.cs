using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class GuestLoginManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField usernameDisplayText;
    [SerializeField] private TMP_InputField usernameInputField;
    [SerializeField] private Button generateNewButton;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button updateUsernameButton;
    
    [Header("Scene Loading")]
    [SerializeField] private string loadingScreenSceneName = "Loading Screen 1";
    [SerializeField] private string mainScreenSceneName = "Main Screen";
    [SerializeField] private float loadingScreenDuration = 5f;
    [SerializeField] private bool useSceneLoading = true;
    
    [Header("Settings")]
    [SerializeField] private bool generateOnAwake = true;
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private Color enabledButtonColor = Color.white;
    [SerializeField] private Color disabledButtonColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    
    private GuestUsernameGenerator usernameGenerator;
    private string currentSystemGeneratedUsername;
    private string originalUsername;
    private string savedUsername;
    private bool isInitialized = false;
    private bool isProcessingLogin = false;
    
    // Properties to access current username from other scripts
    public string CurrentUsername => currentSystemGeneratedUsername;
    public string SavedUsername => savedUsername;
    public bool IsInitialized => isInitialized;
    
    private void Awake()
    {
        // Initialize on Awake if specified
        if (generateOnAwake)
        {
            InitializeAndGenerateUsername();
        }
    }
    
    private void Start()
    {
        // Initialize on Start if not already done in Awake
        if (!isInitialized && generateOnStart)
        {
            InitializeAndGenerateUsername();
        }
    }
    
    /// <summary>
    /// Initialize the manager and generate username
    /// </summary>
    private void InitializeAndGenerateUsername()
    {
        // Get the generator instance
        usernameGenerator = FindObjectOfType<GuestUsernameGenerator>();
        
        if (usernameGenerator == null)
        {
            Debug.LogError("GuestUsernameGenerator not found in scene! Creating one...");
            // Create the generator if it doesn't exist
            GameObject generatorObj = new GameObject("GuestUsernameGenerator");
            usernameGenerator = generatorObj.AddComponent<GuestUsernameGenerator>();
        }
        
        // Setup UI
        SetupButtons();
        SetupInputField();
        
        // Generate initial username
        GenerateAndDisplayUsername();
        
        isInitialized = true;
        Debug.Log("GuestLoginManager initialized and ready");
    }
    
    private void SetupButtons()
    {
        if (generateNewButton != null)
            generateNewButton.onClick.AddListener(OnGenerateNewUsername);
        
        if (loginButton != null)
            loginButton.onClick.AddListener(OnLoginAsGuest);
        
        if (updateUsernameButton != null)
        {
            updateUsernameButton.onClick.AddListener(OnUpdateUsername);
            // Initially disable the update button
            SetUpdateButtonState(false);
        }
    }
    
    private void SetupInputField()
    {
        if (usernameInputField != null)
        {
            // Add listener for when the text changes
            usernameInputField.onValueChanged.AddListener(OnUsernameInputChanged);
            
            // Add listener for when the input field is submitted (Enter key)
            usernameInputField.onSubmit.AddListener(OnUsernameSubmit);
        }
    }
    
    /// <summary>
    /// Called whenever the input field text changes
    /// </summary>
    private void OnUsernameInputChanged(string newText)
    {
        if (!isInitialized) return;
        
        // Enable update button if text is different from system-generated username
        bool shouldEnable = !string.IsNullOrEmpty(newText) && newText != currentSystemGeneratedUsername;
        
        SetUpdateButtonState(shouldEnable);
        
        // Optional: Add visual feedback for invalid username
        if (!string.IsNullOrEmpty(newText) && usernameGenerator != null && usernameGenerator.IsUsernameTaken(newText))
        {
            // Username is taken, show warning
            if (usernameInputField.targetGraphic != null)
            {
                usernameInputField.targetGraphic.color = Color.red;
            }
            
            // Also disable update button if username is taken
            if (shouldEnable)
            {
                SetUpdateButtonState(false);
            }
        }
        else
        {
            if (usernameInputField.targetGraphic != null)
            {
                usernameInputField.targetGraphic.color = Color.white;
            }
        }
    }
    
    /// <summary>
    /// Called when Enter key is pressed in the input field
    /// </summary>
    private void OnUsernameSubmit(string text)
    {
        if (updateUsernameButton != null && updateUsernameButton.interactable)
        {
            OnUpdateUsername();
        }
    }
    
    /// <summary>
    /// Generate a new system username
    /// </summary>
    private void GenerateAndDisplayUsername()
    {
        // Generate a new guest username
        currentSystemGeneratedUsername = usernameGenerator.GenerateGuestUsername();
        originalUsername = currentSystemGeneratedUsername;
        
        // Display it
        if (usernameDisplayText != null)
        {
            usernameDisplayText.text = $"System Generated: {currentSystemGeneratedUsername}";
        }
        
        // Set input field text
        if (usernameInputField != null)
        {
            usernameInputField.text = currentSystemGeneratedUsername;
        }
        
        // Disable update button since it matches the system username
        SetUpdateButtonState(false);
        
        Debug.Log($"Generated guest username: {currentSystemGeneratedUsername}");
    }
    
    /// <summary>
    /// Update the username with the text from input field and proceed to loading
    /// </summary>
    private void OnUpdateUsername()
    {
        if (isProcessingLogin)
        {
            Debug.Log("Already processing login, please wait...");
            return;
        }
        
        if (usernameInputField == null || string.IsNullOrEmpty(usernameInputField.text))
            return;
        
        string newUsername = usernameInputField.text.Trim();
        
        // Validate username
        if (usernameGenerator.IsUsernameTaken(newUsername))
        {
            Debug.LogWarning($"Username '{newUsername}' is already taken!");
            // Show error message to user
            if (usernameDisplayText != null)
            {
                usernameDisplayText.text = $"Error: '{newUsername}' is already taken!";
                StartCoroutine(ResetDisplayTextAfterDelay(2f));
            }
            return;
        }
        
        // Save the new username
        savedUsername = newUsername;
        // Persist for access in other scenes / main menu
        PlayerPrefs.SetString("LastGuestUsername", savedUsername);
        PlayerPrefs.SetString("LastLoginMethod", "Guest");
        PlayerPrefs.Save();
        currentSystemGeneratedUsername = newUsername;
        originalUsername = newUsername;
        
        // Register the new username
        usernameGenerator.RegisterUsername(currentSystemGeneratedUsername);
        
        // Update display
        if (usernameDisplayText != null)
        {
            usernameDisplayText.text = $"Username Saved: {currentSystemGeneratedUsername}";
        }
        
        // Disable update button after successful update
        SetUpdateButtonState(false);
        
        // Disable input field to prevent further changes during loading
        if (usernameInputField != null)
        {
            usernameInputField.interactable = false;
        }
        
        // Disable generate new button
        if (generateNewButton != null)
        {
            generateNewButton.interactable = false;
        }
        
        Debug.Log($"Username updated and saved to: {currentSystemGeneratedUsername}");
        
        // Start the loading process
        StartCoroutine(LoadGameSequence());
    }
    
    /// <summary>
    /// Generate a completely new system username
    /// </summary>
    private void OnGenerateNewUsername()
    {
        if (isProcessingLogin) return;
        
        // Generate new username
        currentSystemGeneratedUsername = usernameGenerator.GenerateGuestUsername();
        originalUsername = currentSystemGeneratedUsername;
        
        // Update UI
        if (usernameDisplayText != null)
        {
            usernameDisplayText.text = $"System Generated: {currentSystemGeneratedUsername}";
        }
        
        if (usernameInputField != null)
        {
            usernameInputField.text = currentSystemGeneratedUsername;
        }
        
        // Disable update button since it matches the new system username
        SetUpdateButtonState(false);
        
        Debug.Log($"New system username generated: {currentSystemGeneratedUsername}");
    }
    
    /// <summary>
    /// Login as guest with current username (without updating)
    /// </summary>
    private void OnLoginAsGuest()
    {
        if (isProcessingLogin) return;
        
        if (!string.IsNullOrEmpty(currentSystemGeneratedUsername))
        {
            savedUsername = currentSystemGeneratedUsername;
            // Persist guest login for main menu
            PlayerPrefs.SetString("LastGuestUsername", savedUsername);
            PlayerPrefs.SetString("LastLoginMethod", "Guest");
            PlayerPrefs.Save();
            Debug.Log($"Logging in as guest: {currentSystemGeneratedUsername}");
            
            // Show login message
            if (usernameDisplayText != null)
            {
                usernameDisplayText.text = $"Logging in as: {currentSystemGeneratedUsername}...";
            }
            
            // Disable UI during loading
            SetUIInteractable(false);
            
            // Start loading sequence
            StartCoroutine(LoadGameSequence());
        }
    }
    
    /// <summary>
    /// Load loading screen, wait 5 seconds, then load main screen
    /// </summary>
    private IEnumerator LoadGameSequence()
    {
        isProcessingLogin = true;
        
        Debug.Log("Starting game loading sequence...");
        
        if (useSceneLoading)
        {
            // Load the loading screen
            yield return StartCoroutine(LoadSceneAsync(loadingScreenSceneName));
            
            // Wait for 5 seconds on the loading screen
            yield return new WaitForSeconds(loadingScreenDuration);
            
            // Load the main screen
            yield return StartCoroutine(LoadSceneAsync(mainScreenSceneName));
        }
        else
        {
            // Simulate loading without scene changes (for testing)
            Debug.Log($"Simulating loading: Would show {loadingScreenSceneName} for {loadingScreenDuration} seconds, then load {mainScreenSceneName}");
            
            // You can activate/deactivate GameObjects here instead of loading scenes
            yield return new WaitForSeconds(loadingScreenDuration);
        }
        
        isProcessingLogin = false;
        Debug.Log("Game loading sequence completed!");
    }
    
    /// <summary>
    /// Load a scene asynchronously
    /// </summary>
    private IEnumerator LoadSceneAsync(string sceneName)
    {
        Debug.Log($"Loading scene: {sceneName}");
        
        // Check if scene exists
        if (!IsSceneValid(sceneName))
        {
            Debug.LogError($"Scene '{sceneName}' not found in build settings!");
            yield break;
        }
        
        // Load scene asynchronously
        AsyncOperation asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
        
        // Wait until the scene is fully loaded
        while (!asyncLoad.isDone)
        {
            // You can update a loading progress bar here if needed
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            Debug.Log($"Loading progress: {progress * 100}%");
            yield return null;
        }
    }
    
    /// <summary>
    /// Check if a scene is valid and added to build settings
    /// </summary>
    private bool IsSceneValid(string sceneName)
    {
        int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            string scenePath = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
            string sceneNameFromPath = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (sceneNameFromPath == sceneName)
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Set UI interactable state
    /// </summary>
    private void SetUIInteractable(bool interactable)
    {
        if (usernameInputField != null)
            usernameInputField.interactable = interactable;
        
        if (generateNewButton != null)
            generateNewButton.interactable = interactable;
        
        if (updateUsernameButton != null)
            updateUsernameButton.interactable = interactable;
        
        if (loginButton != null)
            loginButton.interactable = interactable;
    }
    
    /// <summary>
    /// Set the update button's interactable state
    /// </summary>
    private void SetUpdateButtonState(bool isEnabled)
    {
        if (updateUsernameButton != null && !isProcessingLogin)
        {
            updateUsernameButton.interactable = isEnabled;
            
            // Optional: Change button color to indicate state
            var buttonColors = updateUsernameButton.colors;
            if (isEnabled)
            {
                buttonColors.normalColor = enabledButtonColor;
            }
            else
            {
                buttonColors.normalColor = disabledButtonColor;
            }
            updateUsernameButton.colors = buttonColors;
        }
    }
    
    /// <summary>
    /// Reset display text after delay
    /// </summary>
    private IEnumerator ResetDisplayTextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (usernameDisplayText != null)
        {
            usernameDisplayText.text = $"{currentSystemGeneratedUsername}";
        }
    }
    
    /// <summary>
    /// Restore the display text
    /// </summary>
    private void RestoreDisplayText()
    {
        if (usernameDisplayText != null)
        {
            usernameDisplayText.text = $"{currentSystemGeneratedUsername}";
        }
    }
    
    /// <summary>
    /// Manually trigger username generation (can be called from other scripts)
    /// </summary>
    public void ForceGenerateNewUsername()
    {
        if (usernameGenerator != null)
        {
            GenerateAndDisplayUsername();
        }
        else
        {
            Debug.LogWarning("Username generator not initialized yet!");
            InitializeAndGenerateUsername();
        }
    }
    
    /// <summary>
    /// Get the current username (for external use)
    /// </summary>
    public string GetCurrentUsername()
    {
        return currentSystemGeneratedUsername;
    }
    
    /// <summary>
    /// Get the saved username after update
    /// </summary>
    public string GetSavedUsername()
    {
        return savedUsername;
    }
    
    /// <summary>
    /// Set custom username programmatically
    /// </summary>
    public bool SetCustomUsername(string username)
    {
        if (string.IsNullOrEmpty(username))
            return false;
        
        if (usernameGenerator.IsUsernameTaken(username))
            return false;
        
        currentSystemGeneratedUsername = username;
        originalUsername = username;
        usernameGenerator.RegisterUsername(currentSystemGeneratedUsername);
        
        if (usernameDisplayText != null)
            usernameDisplayText.text = $"{currentSystemGeneratedUsername}";
        
        if (usernameInputField != null)
            usernameInputField.text = currentSystemGeneratedUsername;
        
        SetUpdateButtonState(false);
        // Persist custom username
        PlayerPrefs.SetString("LastGuestUsername", currentSystemGeneratedUsername);
        PlayerPrefs.SetString("LastLoginMethod", "Guest");
        PlayerPrefs.Save();
        return true;
    }
}