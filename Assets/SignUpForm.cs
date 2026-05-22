using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

public class SignUpForm : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField usernameInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private TMP_InputField confirmPasswordInputField;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button signUpButton;
    [SerializeField] private Button backToLoginButton;
    
    [Header("Password Visibility")]
    [SerializeField] private Button showPasswordButton;
    [SerializeField] private Button showConfirmPasswordButton;
    [SerializeField] private Sprite eyeOpenSprite;
    [SerializeField] private Sprite eyeClosedSprite;
    [SerializeField] private Image passwordEyeIcon;
    [SerializeField] private Image confirmPasswordEyeIcon;
    
    [Header("Scene Loading")]
    [SerializeField] private string loginScreenSceneName = "LoginScreen";
    [SerializeField] private string mainScreenSceneName = "MainScreen";
    
    private bool isProcessing = false;
    private bool isPasswordVisible = false;
    private bool isConfirmPasswordVisible = false;
    private bool isInitialized = false;
    
    private void Awake()
    {
        InitializeUnityServices();
        SetupPasswordToggle();
    }
    
    private void SetupPasswordToggle()
    {
        if (showPasswordButton != null)
        {
            showPasswordButton.onClick.AddListener(TogglePasswordVisibility);
        }

        if (showConfirmPasswordButton != null)
        {
            showConfirmPasswordButton.onClick.AddListener(ToggleConfirmPasswordVisibility);
        }
        
        if (passwordInputField != null)
        {
            passwordInputField.contentType = TMP_InputField.ContentType.Password;
            passwordInputField.ForceLabelUpdate();
        }
        
        if (confirmPasswordInputField != null)
        {
            confirmPasswordInputField.contentType = TMP_InputField.ContentType.Password;
            confirmPasswordInputField.ForceLabelUpdate();
        }
        
        UpdateEyeIcon();
    }
    
    private void TogglePasswordVisibility()
    {
        isPasswordVisible = !isPasswordVisible;
        
        if (passwordInputField != null)
        {
            passwordInputField.contentType = isPasswordVisible ? 
                TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
            passwordInputField.ForceLabelUpdate();
        }
        
        UpdateEyeIcon();
    }

    private void ToggleConfirmPasswordVisibility()
    {
        isConfirmPasswordVisible = !isConfirmPasswordVisible;

        if (confirmPasswordInputField != null)
        {
            confirmPasswordInputField.contentType = isConfirmPasswordVisible ? 
                TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
            confirmPasswordInputField.ForceLabelUpdate();
        }

        UpdateEyeIcon();
    }
    
    private void UpdateEyeIcon()
    {
        if (passwordEyeIcon != null)
        {
            if (isPasswordVisible && eyeOpenSprite != null)
                passwordEyeIcon.sprite = eyeOpenSprite;
            else if (!isPasswordVisible && eyeClosedSprite != null)
                passwordEyeIcon.sprite = eyeClosedSprite;
        }
        
        if (confirmPasswordEyeIcon != null)
        {
            if (isConfirmPasswordVisible && eyeOpenSprite != null)
                confirmPasswordEyeIcon.sprite = eyeOpenSprite;
            else if (!isConfirmPasswordVisible && eyeClosedSprite != null)
                confirmPasswordEyeIcon.sprite = eyeClosedSprite;
        }
    }
    
    private async void InitializeUnityServices()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
                Debug.Log("Unity Services initialized successfully");
            }
            
            // Sign out any existing session before showing sign-up form
            if (AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("Signing out existing user before sign-up");
                AuthenticationService.Instance.SignOut();
            }
            
            SetupUI();
            isInitialized = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize Unity Services: {e.Message}");
            ShowStatusMessage("Failed to initialize services. Check internet connection.", true);
        }
    }
    
    private void SetupUI()
    {
        if (signUpButton != null)
            signUpButton.onClick.AddListener(OnSignUpClicked);
        
        if (backToLoginButton != null)
            backToLoginButton.onClick.AddListener(OnBackToLoginClicked);
    }
    
    #region Validation Methods
    
    private bool ValidateUsername(string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            ShowStatusMessage("Username is required.", true);
            return false;
        }
        
        // Username requirements: 3-20 characters, alphanumeric and underscores
        if (username.Length < 3 || username.Length > 20)
        {
            ShowStatusMessage("Username must be 3-20 characters long.", true);
            return false;
        }
        
        if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$"))
        {
            ShowStatusMessage("Username can only contain letters, numbers, and underscores.", true);
            return false;
        }
        
        return true;
    }
    
    private bool ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            ShowStatusMessage("Password is required.", true);
            return false;
        }
        
        // Password requirements: minimum 6 characters
        if (password.Length < 6)
        {
            ShowStatusMessage("Password must be at least 6 characters long.", true);
            return false;
        }
        
        if (password.Length > 30)
        {
            ShowStatusMessage("Password must be less than 30 characters.", true);
            return false;
        }
        
        return true;
    }
    
    #endregion
    
    #region Sign Up Methods
    
    private async void OnSignUpClicked()
    {
        if (isProcessing || !isInitialized) return;
        
        string username = usernameInputField?.text?.Trim();
        string password = passwordInputField?.text;
        string confirmPassword = confirmPasswordInputField?.text;
        
        // Validate all inputs
        if (!ValidateUsername(username)) return;
        if (!ValidatePassword(password)) return;
        
        if (password != confirmPassword)
        {
            ShowStatusMessage("Passwords do not match.", true);
            return;
        }
        
        isProcessing = true;
        SetUIInteractable(false);
        ShowStatusMessage("Creating account...", false);
        
        try
        {
            await RegisterWithUnityCloud(username, password);
        }
        catch (System.Exception e)
        {
            ShowStatusMessage($"Registration failed: {e.Message}", true);
            Debug.LogError($"Registration error: {e.Message}");
            isProcessing = false;
            SetUIInteractable(true);
        }
    }
    
    private async Task RegisterWithUnityCloud(string username, string password)
    {
        try
        {
            // Ensure we're signed out before attempting to sign up
            if (AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("Signing out current user before registration");
                AuthenticationService.Instance.SignOut();
                // Wait a moment for sign out to complete
                await Task.Delay(100);
            }
            
            // Sign up with Username and Password
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);
            
            Debug.Log($"Account created successfully for: {username}");
            
            // Show success message
            ShowStatusMessage("Account created successfully!", false);

            // Make sure the new account is signed in before leaving this screen.
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("Signing in newly created user automatically");
                await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
            }
            
            // Store the username for next time
            PlayerPrefs.SetString("LastSignedInUser", username);
            PlayerPrefs.SetString("LastSignedInPlayer", AuthenticationService.Instance.PlayerName);
            PlayerPrefs.SetString("LastSignedInPlayerId", AuthenticationService.Instance.PlayerId);
            PlayerPrefs.SetString("LastLoginMethod", "Account");
            PlayerPrefs.Save();

            // Update PlayerData so UI listeners refresh with the new name
            var pd = FindObjectOfType<PlayerData>();
            if (pd != null)
                pd.ChangePlayerName(username);
            
            // Wait a moment to show success message
            await Task.Delay(1500);
            
            // Continue into the game with the authenticated session.
            StartCoroutine(LoadMainScreen());
        }
        catch (AuthenticationException ex)
        {
            HandleAuthenticationError(ex);
        }
        catch (RequestFailedException ex)
        {
            HandleRequestFailedError(ex);
        }
        finally
        {
            isProcessing = false;
            SetUIInteractable(true);
        }
    }

    private IEnumerator LoadMainScreen()
    {
        ShowStatusMessage("Loading game...", false);

        if (!string.IsNullOrEmpty(mainScreenSceneName))
        {
            yield return StartCoroutine(LoadSceneAsync(mainScreenSceneName));
        }

        isProcessing = false;
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("Scene name is empty!");
            yield break;
        }

        if (!IsSceneValid(sceneName))
        {
            Debug.LogError($"Scene '{sceneName}' not found in build settings!");
            yield break;
        }

        AsyncOperation asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);

        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }

    private bool IsSceneValid(string sceneName)
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
            string sceneNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (sceneNameWithoutExtension == sceneName)
                return true;
        }

        return false;
    }
    
    private void HandleAuthenticationError(AuthenticationException ex)
    {
        // Error code 10000 means player is already signed in
        if (ex.ErrorCode == 10000)
        {
            Debug.Log("Player was already signed in. Signing out and retrying...");
            // Force sign out
            AuthenticationService.Instance.SignOut();
            // Retry after a short delay
            StartCoroutine(RetrySignUpAfterDelay());
        }
        else
        {
            string errorMessage = ex.ErrorCode switch
            {
                1000 => "Invalid parameters. Please check your inputs.",
                1001 => "Password does not meet requirements.",
                1002 => "Username already taken. Please choose another.",
                _ => $"Registration failed: {ex.Message}"
            };
            
            ShowStatusMessage(errorMessage, true);
            Debug.LogError($"Authentication error during signup: {ex.ErrorCode} - {ex.Message}");
            isProcessing = false;
            SetUIInteractable(true);
        }
    }
    
    private IEnumerator RetrySignUpAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);
        // Retry sign up
        OnSignUpClicked();
    }
    
    private void HandleRequestFailedError(RequestFailedException ex)
    {
        string errorMessage = ex.ErrorCode switch
        {
            400 => "Invalid request. Please check your information.",
            401 => "Unauthorized. Please try again.",
            403 => "Access forbidden. Please contact support.",
            404 => "Service unavailable. Please try again later.",
            409 => "Username already exists. Please choose a different username.",
            _ => $"Registration failed. Please try again. (Error: {ex.ErrorCode})"
        };
        
        ShowStatusMessage(errorMessage, true);
        Debug.LogError($"Request failed during signup: {ex.ErrorCode} - {ex.Message}");
        isProcessing = false;
        SetUIInteractable(true);
    }
    
    #endregion
    
    #region Navigation Methods
    
    private void OnBackToLoginClicked()
    {
        // Clear input fields
        if (usernameInputField != null) usernameInputField.text = "";
        if (passwordInputField != null) passwordInputField.text = "";
        if (confirmPasswordInputField != null) confirmPasswordInputField.text = "";
        
        // Sign out any existing session before going back to login
        if (AuthenticationService.Instance.IsSignedIn)
        {
            AuthenticationService.Instance.SignOut();
        }
        
        // Load login screen
        if (!string.IsNullOrEmpty(loginScreenSceneName))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(loginScreenSceneName);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
    
    #endregion
    
    #region UI Helpers
    
    private void ShowStatusMessage(string message, bool isError)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = isError ? Color.red : Color.green;
            
            if (!string.IsNullOrEmpty(message))
            {
                StartCoroutine(ClearStatusAfterDelay(5f));
            }
        }
        
        Debug.Log($"{(isError ? "Error" : "Info")}: {message}");
    }
    
    private IEnumerator ClearStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (statusText != null && statusText.text != null)
        {
            statusText.text = "";
        }
    }
    
    private void SetUIInteractable(bool interactable)
    {
        if (signUpButton != null) signUpButton.interactable = interactable;
        if (backToLoginButton != null) backToLoginButton.interactable = interactable;
        if (showPasswordButton != null) showPasswordButton.interactable = interactable;
        if (showConfirmPasswordButton != null) showConfirmPasswordButton.interactable = interactable;
        if (usernameInputField != null) usernameInputField.interactable = interactable;
        if (passwordInputField != null) passwordInputField.interactable = interactable;
        if (confirmPasswordInputField != null) confirmPasswordInputField.interactable = interactable;
    }
    
    #endregion
}