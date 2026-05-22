using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

public class LoginForm : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField usernameInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text welcomeText;
    [SerializeField] private Button signInButton;
    [SerializeField] private Button signUpButton;
    [SerializeField] private Button guestLoginButton;
    [SerializeField] private Button signOutButton;
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject guestPanel;
    [SerializeField] private TMP_Text guestUsernameText;
    
    [Header("Password Field")]
    [SerializeField] private Button showPasswordButton;
    [SerializeField] private Sprite eyeOpenSprite;
    [SerializeField] private Sprite eyeClosedSprite;
    [SerializeField] private Image eyeIconImage;
    
    [Header("Guest Settings")]
    [SerializeField] private TMP_InputField guestUsernameInputField;
    [SerializeField] private Button generateGuestUsernameButton;
    [SerializeField] private Button updateGuestUsernameButton;
    
    [Header("Scene Loading")]
    [SerializeField] private string mainScreenSceneName = "MainScreen";
    [SerializeField] private string loadingScreenSceneName = "LoadingScreen";
    [SerializeField] private string signUpSceneName = "SignUpScene";
    [SerializeField] private float loadingScreenDuration = 2f;
    
    private GuestUsernameGenerator usernameGenerator;
    private string currentGuestUsername;
    private bool isInitialized = false;
    private bool isProcessing = false;
    private bool isSignedIn = false;
    private bool useSceneLoading = true;
    private bool isPasswordVisible = false;
    
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
        
        if (passwordInputField != null)
        {
            passwordInputField.contentType = TMP_InputField.ContentType.Password;
            passwordInputField.ForceLabelUpdate();
        }
        
        UpdateEyeIcon();
    }
    
    private void TogglePasswordVisibility()
    {
        isPasswordVisible = !isPasswordVisible;
        
        if (passwordInputField != null)
        {
            passwordInputField.contentType = isPasswordVisible ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
            passwordInputField.ForceLabelUpdate();
        }
        
        UpdateEyeIcon();
    }
    
    private void UpdateEyeIcon()
    {
        if (eyeIconImage != null)
        {
            if (isPasswordVisible && eyeOpenSprite != null)
                eyeIconImage.sprite = eyeOpenSprite;
            else if (!isPasswordVisible && eyeClosedSprite != null)
                eyeIconImage.sprite = eyeClosedSprite;
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
            
            SetupUI();
            await CheckExistingSession();
            isInitialized = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize Unity Services: {e.Message}");
            ShowStatusMessage("Failed to initialize services. Check internet connection.", true);
        }
    }
    
    private async Task CheckExistingSession()
    {
        if (AuthenticationService.Instance.IsSignedIn)
        {
            isSignedIn = true;
            string playerName = AuthenticationService.Instance.PlayerName;
            string playerId = AuthenticationService.Instance.PlayerId;
            
            ShowWelcomeMessage($"Welcome back, {playerName}!");
            ShowGuestPanel(false);
            ShowLoginPanel(false);
            ShowStatusMessage($"Signed in as: {playerName}", false);
            
            PlayerPrefs.SetString("LastSignedInPlayer", playerName);
            PlayerPrefs.SetString("LastSignedInPlayerId", playerId);
            PlayerPrefs.Save();
        }
        else
        {
            ShowLoginPanel(true);
            ShowGuestPanel(true);
            
            if (PlayerPrefs.HasKey("LastGuestUsername"))
            {
                currentGuestUsername = PlayerPrefs.GetString("LastGuestUsername");
                if (guestUsernameInputField != null)
                    guestUsernameInputField.text = currentGuestUsername;
            }
            else
            {
                GenerateGuestUsername();
            }
        }
    }
    
    private void SetupUI()
    {
        usernameGenerator = FindObjectOfType<GuestUsernameGenerator>();
        if (usernameGenerator == null)
        {
            GameObject generatorObj = new GameObject("GuestUsernameGenerator");
            usernameGenerator = generatorObj.AddComponent<GuestUsernameGenerator>();
        }
        
        if (signInButton != null)
            signInButton.onClick.AddListener(OnSignInClicked);
        
        if (signUpButton != null)
            signUpButton.onClick.AddListener(OnSignUpClicked);
        
        if (guestLoginButton != null)
            guestLoginButton.onClick.AddListener(OnGuestLoginClicked);
        
        if (signOutButton != null)
            signOutButton.onClick.AddListener(OnSignOutClicked);
        
        if (generateGuestUsernameButton != null)
            generateGuestUsernameButton.onClick.AddListener(OnGenerateGuestUsername);
        
        if (updateGuestUsernameButton != null)
            updateGuestUsernameButton.onClick.AddListener(OnUpdateGuestUsername);
        
        if (guestUsernameInputField != null)
        {
            guestUsernameInputField.onValueChanged.AddListener(OnGuestUsernameChanged);
        }
        
        if (string.IsNullOrEmpty(currentGuestUsername))
        {
            GenerateGuestUsername();
        }
        else if (guestUsernameInputField != null)
        {
            guestUsernameInputField.text = currentGuestUsername;
        }
    }
    
    #region Sign In Methods
    
    private async void OnSignInClicked()
    {
        if (isProcessing) return;
        
        string username = usernameInputField?.text?.Trim();
        string password = passwordInputField?.text;
        
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowStatusMessage("Please enter both username and password", true);
            return;
        }
        
        isProcessing = true;
        SetButtonsInteractable(false);
        ShowStatusMessage("Signing in...", false);
        
        try
        {
            await SignInWithUsernamePassword(username, password);
        }
        catch (System.Exception e)
        {
            ShowStatusMessage($"Sign in failed: {e.Message}", true);
            Debug.LogError($"Sign in error: {e.Message}");
            isProcessing = false;
            SetButtonsInteractable(true);
        }
    }
    
    private async Task SignInWithUsernamePassword(string username, string password)
    {
        try
        {
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
            HandleSuccessfulSignIn();
        }
        catch (AuthenticationException ex)
        {
            HandleAuthenticationError(ex);
        }
        catch (RequestFailedException ex)
        {
            HandleRequestFailedError(ex);
        }
    }
    
    private void HandleSuccessfulSignIn()
    {
        string playerName = AuthenticationService.Instance.PlayerName;
        string playerId = AuthenticationService.Instance.PlayerId;
        string username = usernameInputField.text.Trim();
        
        ShowStatusMessage($"Welcome, {playerName}!", false);
        ShowWelcomeMessage($"Successfully signed in as {playerName}");
        
        isSignedIn = true;
        
        PlayerPrefs.SetString("LastSignedInPlayer", playerName);
        PlayerPrefs.SetString("LastSignedInPlayerId", playerId);
        PlayerPrefs.SetString("LastLoginMethod", "Account");
        PlayerPrefs.Save();

        // Update PlayerData so UI listeners refresh (e.g., UserLevel)
        var pd = FindObjectOfType<PlayerData>();
        if (pd != null)
            pd.ChangePlayerName(username);
        
        StartCoroutine(ProceedAfterLogin());
    }
    
    private IEnumerator ProceedAfterLogin()
    {
        yield return new WaitForSeconds(1f);
        StartCoroutine(LoadMainScreen());
    }
    
    private void HandleAuthenticationError(AuthenticationException ex)
    {
        switch (ex.ErrorCode)
        {
            case 1000:
            case 1003:
                ShowStatusMessage("Account not found. Please sign up first.", true);
                break;
            case 1001:
                ShowStatusMessage("Invalid password. Please try again.", true);
                break;
            case 1002:
                ShowStatusMessage("Invalid username format.", true);
                break;
            default:
                ShowStatusMessage($"Authentication failed: {ex.Message}", true);
                break;
        }
        
        isProcessing = false;
        SetButtonsInteractable(true);
    }
    
    private void HandleRequestFailedError(RequestFailedException ex)
    {
        switch (ex.ErrorCode)
        {
            case 401:
                ShowStatusMessage("Invalid username or password.", true);
                break;
            case 403:
                ShowStatusMessage("Access forbidden.", true);
                break;
            case 404:
                ShowStatusMessage("Service not available.", true);
                break;
            default:
                ShowStatusMessage($"Sign in failed: {ex.Message}", true);
                break;
        }
        
        isProcessing = false;
        SetButtonsInteractable(true);
    }
    
    private void OnSignUpClicked()
    {
        if (!string.IsNullOrEmpty(signUpSceneName))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(signUpSceneName);
        }
    }
    
    private async void OnSignOutClicked()
    {
        try
        {
            AuthenticationService.Instance.SignOut();
            isSignedIn = false;
            
            ShowLoginPanel(true);
            ShowGuestPanel(true);
            ShowWelcomeMessage("");
            ShowStatusMessage("Signed out successfully", false);
            
            if (usernameInputField != null) usernameInputField.text = "";
            if (passwordInputField != null) passwordInputField.text = "";
            
            GenerateGuestUsername();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Sign out error: {e.Message}");
            ShowStatusMessage("Failed to sign out", true);
        }
    }
    
    #endregion
    
    #region Guest Methods
    
    private void OnGuestLoginClicked()
    {
        if (isProcessing) return;
        
        string guestName = GetCurrentGuestUsername();
        if (string.IsNullOrEmpty(guestName))
        {
            ShowStatusMessage("Please enter or generate a guest username", true);
            return;
        }
        
        isProcessing = true;
        SetButtonsInteractable(false);
        ShowStatusMessage($"Logging in as guest: {guestName}...", false);
        
        PlayerPrefs.SetString("LastGuestUsername", guestName);
        PlayerPrefs.SetString("LastLoginMethod", "Guest");
        PlayerPrefs.Save();

        // Update PlayerData so UI listeners refresh (e.g., UserLevel)
        var pd = FindObjectOfType<PlayerData>();
        if (pd != null)
            pd.ChangePlayerName(guestName);
        
        StartCoroutine(LoadMainScreenWithGuest(guestName));
    }
    
    private IEnumerator LoadMainScreenWithGuest(string guestName)
    {
        yield return new WaitForSeconds(1f);
        ShowStatusMessage($"Welcome guest: {guestName}!", false);
        yield return new WaitForSeconds(0.5f);
        StartCoroutine(LoadMainScreen());
    }
    
    private void GenerateGuestUsername()
    {
        if (usernameGenerator != null)
        {
            currentGuestUsername = usernameGenerator.GenerateGuestUsername();
            if (guestUsernameInputField != null)
            {
                guestUsernameInputField.text = currentGuestUsername;
            }
            if (guestUsernameText != null)
            {
                guestUsernameText.text = $"Guest: {currentGuestUsername}";
            }
        }
    }
    
    private void OnGenerateGuestUsername()
    {
        GenerateGuestUsername();
        ShowStatusMessage("New guest username generated!", false);
        
        if (updateGuestUsernameButton != null)
        {
            updateGuestUsernameButton.interactable = false;
        }
    }
    
    private void OnGuestUsernameChanged(string newText)
    {
        if (!string.IsNullOrEmpty(newText))
        {
            bool isDifferent = newText != currentGuestUsername;
            if (updateGuestUsernameButton != null)
            {
                updateGuestUsernameButton.interactable = isDifferent;
            }
        }
    }
    
    private void OnUpdateGuestUsername()
    {
        if (guestUsernameInputField != null && !string.IsNullOrEmpty(guestUsernameInputField.text))
        {
            currentGuestUsername = guestUsernameInputField.text.Trim();
            if (guestUsernameText != null)
            {
                guestUsernameText.text = $"Guest: {currentGuestUsername}";
            }
            ShowStatusMessage($"Guest username updated to: {currentGuestUsername}", false);
            // Update PlayerData so UI updates immediately
            var pd = FindObjectOfType<PlayerData>();
            if (pd != null)
                pd.ChangePlayerName(currentGuestUsername);
            
            if (updateGuestUsernameButton != null)
                updateGuestUsernameButton.interactable = false;
        }
    }
    
    private string GetCurrentGuestUsername()
    {
        if (guestUsernameInputField != null && !string.IsNullOrEmpty(guestUsernameInputField.text))
        {
            return guestUsernameInputField.text.Trim();
        }
        return currentGuestUsername;
    }
    
    #endregion
    
    #region UI Helpers
    
    private void ShowLoginPanel(bool show)
    {
        if (loginPanel != null)
            loginPanel.SetActive(show);
    }
    
    private void ShowGuestPanel(bool show)
    {
        if (guestPanel != null)
            guestPanel.SetActive(show);
    }
    
    private void ShowWelcomeMessage(string message)
    {
        if (welcomeText != null)
        {
            welcomeText.text = message;
            welcomeText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
    }
    
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
        if (statusText != null && statusText.text != null && !string.IsNullOrEmpty(statusText.text))
        {
            if (!statusText.text.Contains("Welcome") && 
                !statusText.text.Contains("Signed in"))
            {
                statusText.text = "";
            }
        }
    }
    
    private void SetButtonsInteractable(bool interactable)
    {
        if (signInButton != null) signInButton.interactable = interactable;
        if (signUpButton != null) signUpButton.interactable = interactable;
        if (guestLoginButton != null) guestLoginButton.interactable = interactable;
        if (generateGuestUsernameButton != null) generateGuestUsernameButton.interactable = interactable;
        if (updateGuestUsernameButton != null) updateGuestUsernameButton.interactable = interactable;
        if (signOutButton != null) signOutButton.interactable = interactable;
        if (showPasswordButton != null) showPasswordButton.interactable = interactable;
    }
    
    private IEnumerator LoadMainScreen()
    {
        ShowStatusMessage("Loading game...", false);
        
        if (!string.IsNullOrEmpty(loadingScreenSceneName) && useSceneLoading)
        {
            yield return StartCoroutine(LoadSceneAsync(loadingScreenSceneName));
            yield return new WaitForSeconds(loadingScreenDuration);
        }
        
        yield return StartCoroutine(LoadSceneAsync(mainScreenSceneName));
        
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
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            Debug.Log($"Loading progress: {progress * 100}%");
            yield return null;
        }
    }
    
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
    
    #endregion
}