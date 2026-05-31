using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginForm : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Status UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text welcomeText;

    [Header("Login Panel")]
   // [SerializeField] private GameObject      loginPanel;
    [SerializeField] private TMP_InputField  usernameInputField;
    [SerializeField] private TMP_InputField  passwordInputField;
    [SerializeField] private Button          signInButton;
    [SerializeField] private Button          signUpButton;

    [Header("Password Visibility")]
    [SerializeField] private Button showPasswordButton;
    [SerializeField] private Sprite eyeOpenSprite;
    [SerializeField] private Sprite eyeClosedSprite;
    [SerializeField] private Image  eyeIconImage;

    [Header("Scene Loading")]
    [SerializeField] private string mainScreenSceneName    = "Main Screen";
    [SerializeField] private string loadingScreenSceneName = "Loading Screen 1";
    [SerializeField] private string signUpSceneName        = "SignUpScene";
    [SerializeField] private float  loadingScreenDuration  = 2f;

    // ── Private state ─────────────────────────────────────────────────────────

    private GuestUsernameGenerator usernameGenerator;
    private string currentGuestUsername;
    private bool   isProcessing;
    private bool   isPasswordVisible;
    private bool   isLoadingScene;  // Prevent duplicate scene loading
    private PlayerData playerData;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (AuthManager.Instance != null)
            AuthManager.Instance.OnStateChanged += OnAuthStateChanged;
    }

    private void OnDisable()
    {
        if (AuthManager.Instance != null)
            AuthManager.Instance.OnStateChanged -= OnAuthStateChanged;
    }

    private void Start()
    {
        // Resolve the current PlayerData reference if one is already live.
        FindPlayerData();
        
        SetupPasswordToggle();
        SetupButtons();

        // Hide everything until auth state is known.
       // loginPanel?.SetActive(false);
        isLoadingScene = false;

        // Re-subscribe in case OnEnable fired before AuthManager.Awake completed.
        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.OnStateChanged -= OnAuthStateChanged;
            AuthManager.Instance.OnStateChanged += OnAuthStateChanged;
        }

        StartCoroutine(WaitForAuthThenRefresh());
    }

    private void FindPlayerData()
    {
        playerData = AuthManager.Instance?.CurrentPlayerData ?? FindObjectOfType<PlayerData>();
    }

    private bool TryGetPlayerData(out PlayerData resolvedPlayerData)
    {
        if (AuthManager.Instance?.CurrentPlayerData != null)
        {
            resolvedPlayerData = AuthManager.Instance.CurrentPlayerData;
            playerData = resolvedPlayerData;
            return true;
        }

        if (playerData == null)
        {
            FindPlayerData();
        }

        resolvedPlayerData = playerData;
        return resolvedPlayerData != null;
    }

    // ── Auth-ready coroutine ──────────────────────────────────────────────────

    /// <summary>
    /// Polls until AuthManager.IsReady, then fires an initial UI refresh.
    /// Handles the race between this MonoBehaviour's Start and AuthManager's async init.
    /// </summary>
    private IEnumerator WaitForAuthThenRefresh()
    {
        const float kTimeout = 10f;
        float elapsed = 0f;

        while (elapsed < kTimeout)
        {
            if (AuthManager.Instance != null && AuthManager.Instance.IsReady) break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (elapsed >= kTimeout)
            Debug.LogWarning("[LoginForm] Timed out waiting for AuthManager.IsReady.");

        OnAuthStateChanged();
    }

    /// <summary>
    /// Syncs the player name from login username to PlayerData
    /// </summary>
    private void SyncPlayerDataName(string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return;
        }

        if (TryGetPlayerData(out var resolvedPlayerData))
        {
            // Use PlayerData API so it triggers change notifications and saves properly
            resolvedPlayerData.ChangePlayerName(username);
            Debug.Log($"[LoginForm] Synced player name to PlayerData: {username}");
        }
    }

    // ── Auth state handler ────────────────────────────────────────────────────

    private void OnAuthStateChanged()
    {
        if (AuthManager.Instance == null) return;

        if (AuthManager.Instance.IsSignedIn)
        {
            // Session was restored or sign-in succeeded
            string name = AuthManager.Instance.CloudUsername
                          ?? AuthManager.Instance.GuestName
                          ?? PlayerPrefs.GetString("LastSignedInPlayer", "Player");

            // Sync the name to PlayerData
            SyncPlayerDataName(name);

            ShowWelcomeMessage($"Welcome back, {name}!");
           // loginPanel?.SetActive(false);

            // Only load main screen if we're not already loading and not already in main scene
            if (!isLoadingScene && !IsInMainScene())
            {
                StartCoroutine(LoadMainScreen());
            }
        }
        else
        {
            // No session — show login panel
            //loginPanel?.SetActive(true);
            ShowWelcomeMessage("");

            // Reset processing flag when showing login form
            isProcessing = false;
            SetButtonsInteractable(true);
        }
    }

    // ── Helper to check if already in main scene ──────────────────────────────

    private bool IsInMainScene()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        return currentScene == mainScreenSceneName;
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    private void SetupButtons()
    {
        signInButton?.onClick.AddListener(OnSignInClicked);
        signUpButton?.onClick.AddListener(OnSignUpClicked);
    }

    private void SetupPasswordToggle()
    {
        showPasswordButton?.onClick.AddListener(TogglePasswordVisibility);

        if (passwordInputField != null)
        {
            passwordInputField.contentType = TMP_InputField.ContentType.Password;
            passwordInputField.ForceLabelUpdate();
        }

        UpdateEyeIcon();
    }

    private void SetupGuestGenerator()
    {
        usernameGenerator = FindObjectOfType<GuestUsernameGenerator>();
        if (usernameGenerator == null)
        {
            GameObject go = new GameObject("GuestUsernameGenerator");
            usernameGenerator = go.AddComponent<GuestUsernameGenerator>();
        }
    }

    // ── Sign in ───────────────────────────────────────────────────────────────

    private async void OnSignInClicked()
    {
        if (isProcessing || isLoadingScene) return;

        string username = usernameInputField?.text?.Trim();
        string password = passwordInputField?.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowStatusMessage("Please enter both username and password.", true);
            return;
        }

        isProcessing = true;
        SetButtonsInteractable(false);
        ShowStatusMessage("Signing in…", false);

        // ✅ IMPORTANT: Sign out any existing session before attempting to sign in
        if (AuthManager.Instance.IsSignedIn)
        {
            Debug.Log("[LoginForm] Existing session detected. Signing out before new sign in.");
            AuthManager.Instance.SignOut();
            await Task.Delay(100); // Wait a moment for sign out to complete
        }

        AuthResult result = await AuthManager.Instance.SignInAsync(username, password);

        if (result.IsSuccess)
        {
            ShowStatusMessage("Sign in successful!", false);
            // OnAuthStateChanged will fire and load the main screen
        }
        else
        {
            ShowStatusMessage(result.ErrorMessage, true);
            isProcessing = false;
            SetButtonsInteractable(true);
        }
    }

    // ── Sign up navigation ────────────────────────────────────────────────────

    private void OnSignUpClicked()
    {
        if (isLoadingScene) return;
        
        // Sign out any existing session before going to sign-up
        if (AuthManager.Instance != null && AuthManager.Instance.IsSignedIn)
        {
            Debug.Log("[LoginForm] Signing out existing session before sign-up.");
            AuthManager.Instance.SignOut();
        }
        
        if (!string.IsNullOrEmpty(signUpSceneName))
            UnityEngine.SceneManagement.SceneManager.LoadScene(signUpSceneName);
        else
            Debug.LogError("[LoginForm] Sign-up scene name not set!");
    }

    // ── Password toggle ───────────────────────────────────────────────────────

    private void TogglePasswordVisibility()
    {
        isPasswordVisible = !isPasswordVisible;

        if (passwordInputField != null)
        {
            passwordInputField.contentType = isPasswordVisible
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;
            passwordInputField.ForceLabelUpdate();
        }

        UpdateEyeIcon();
    }

    private void UpdateEyeIcon()
    {
        if (eyeIconImage == null) return;
        if (isPasswordVisible  && eyeOpenSprite  != null) eyeIconImage.sprite = eyeOpenSprite;
        if (!isPasswordVisible && eyeClosedSprite != null) eyeIconImage.sprite = eyeClosedSprite;
    }

    // ── Scene loading ─────────────────────────────────────────────────────────

    private IEnumerator LoadMainScreen()
    {
        if (isLoadingScene) yield break;
        
        isLoadingScene = true;
        ShowStatusMessage("Loading game…", false);

        if (!string.IsNullOrEmpty(loadingScreenSceneName))
        {
            yield return StartCoroutine(LoadSceneAsync(loadingScreenSceneName));
            yield return new WaitForSeconds(loadingScreenDuration);
        }

        yield return StartCoroutine(LoadSceneAsync(mainScreenSceneName));
        
        isProcessing = false;
        isLoadingScene = false;
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) 
        { 
            Debug.LogError("[LoginForm] Scene name is empty!"); 
            yield break; 
        }
        
        if (!IsSceneValid(sceneName))        
        { 
            Debug.LogError($"[LoginForm] Scene '{sceneName}' not in build settings!"); 
            yield break; 
        }

        AsyncOperation op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone)
        {
            Debug.Log($"[LoginForm] Loading '{sceneName}': {Mathf.Clamp01(op.progress / 0.9f) * 100:0}%");
            yield return null;
        }
    }

    private static bool IsSceneValid(string sceneName)
    {
        int count = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                return true;
        }
        return false;
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private void ShowWelcomeMessage(string message)
    {
        if (welcomeText == null) return;
        welcomeText.text = message;
        welcomeText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    private void ShowStatusMessage(string message, bool isError)
    {
        if (statusText == null) return;
        statusText.text  = message;
        statusText.color = isError ? Color.red : Color.green;

        if (!string.IsNullOrEmpty(message))
            StartCoroutine(ClearStatusAfterDelay(5f));
    }

    private IEnumerator ClearStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (statusText != null && statusText.text != null)
        {
            // Only clear if it's not a welcome message
            if (!statusText.text.Contains("Welcome"))
                statusText.text = "";
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (signInButton       != null) signInButton.interactable       = interactable;
        if (signUpButton       != null) signUpButton.interactable       = interactable;
        if (showPasswordButton != null) showPasswordButton.interactable = interactable;
    }
}