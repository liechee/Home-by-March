using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene 1 login UI — username/password provider.
///
/// Responsibilities:
///   - Reflect auth state via AuthManager.OnStateChanged.
///   - Handle username/password sign-in through AuthManager.
///   - Handle guest login (creates anonymous Unity session via AuthManager).
///   - Navigate to sign-up scene or main scene.
///
/// This script owns NO auth logic — everything goes through AuthManager.
/// Guest account upgrade is handled exclusively by AccountHubUI in Scene 2.
/// </summary>
public class LoginForm : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Status UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text welcomeText;

    [Header("Login Panel")]
    [SerializeField] private GameObject      loginPanel;
    [SerializeField] private TMP_InputField  usernameInputField;
    [SerializeField] private TMP_InputField  passwordInputField;
    [SerializeField] private Button          signInButton;
    [SerializeField] private Button          signUpButton;

    [Header("Password Visibility")]
    [SerializeField] private Button showPasswordButton;
    [SerializeField] private Sprite eyeOpenSprite;
    [SerializeField] private Sprite eyeClosedSprite;
    [SerializeField] private Image  eyeIconImage;

    [Header("Guest Panel")]
    [SerializeField] private GameObject     guestPanel;
    [SerializeField] private TMP_Text       guestUsernameText;
    [SerializeField] private TMP_InputField guestUsernameInputField;
    [SerializeField] private Button         guestLoginButton;
    [SerializeField] private Button         generateGuestUsernameButton;
    [SerializeField] private Button         updateGuestUsernameButton;

    [Header("Scene Loading")]
    [SerializeField] private string mainScreenSceneName    = "MainScreen";
    [SerializeField] private string loadingScreenSceneName = "LoadingScreen";
    [SerializeField] private string signUpSceneName        = "SignUpScene";
    [SerializeField] private float  loadingScreenDuration  = 2f;

    // ── Private state ─────────────────────────────────────────────────────────

    private GuestUsernameGenerator usernameGenerator;
    private string currentGuestUsername;
    private bool   isProcessing;
    private bool   isPasswordVisible;

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
        SetupPasswordToggle();
        SetupButtons();
        SetupGuestGenerator();

        // Hide everything until auth state is known.
        loginPanel?.SetActive(false);
        guestPanel?.SetActive(false);

        // Re-subscribe in case OnEnable fired before AuthManager.Awake completed.
        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.OnStateChanged -= OnAuthStateChanged;
            AuthManager.Instance.OnStateChanged += OnAuthStateChanged;
        }

        StartCoroutine(WaitForAuthThenRefresh());
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

    // ── Auth state handler ────────────────────────────────────────────────────

    private void OnAuthStateChanged()
    {
        if (AuthManager.Instance == null) return;

        if (AuthManager.Instance.IsSignedIn)
        {
            // Session was restored — skip login form and go straight to game.
            string name = AuthManager.Instance.CloudUsername
                          ?? PlayerPrefs.GetString("LastSignedInPlayer", "Player");

            ShowWelcomeMessage($"Welcome back, {name}!");
            loginPanel?.SetActive(false);
            guestPanel?.SetActive(false);

            if (!isProcessing)
                StartCoroutine(LoadMainScreen());
        }
        else
        {
            // No session — show login and guest panels.
            loginPanel?.SetActive(true);
            guestPanel?.SetActive(true);
            ShowWelcomeMessage("");

            // Restore last guest name if available.
            if (string.IsNullOrEmpty(currentGuestUsername))
            {
                string saved = PlayerPrefs.GetString("LastGuestUsername", "");
                if (!string.IsNullOrEmpty(saved))
                {
                    currentGuestUsername = saved;
                    if (guestUsernameInputField != null)
                        guestUsernameInputField.text = saved;
                    if (guestUsernameText != null)
                        guestUsernameText.text = $"Guest: {saved}";
                }
                else
                {
                    GenerateGuestUsername();
                }
            }
        }
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    private void SetupButtons()
    {
        signInButton?.onClick.AddListener(OnSignInClicked);
        signUpButton?.onClick.AddListener(OnSignUpClicked);
        guestLoginButton?.onClick.AddListener(OnGuestLoginClicked);
        generateGuestUsernameButton?.onClick.AddListener(OnGenerateGuestUsername);
        updateGuestUsernameButton?.onClick.AddListener(OnUpdateGuestUsername);

        if (guestUsernameInputField != null)
            guestUsernameInputField.onValueChanged.AddListener(OnGuestUsernameInputChanged);
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
        if (isProcessing) return;

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

        AuthResult result = await AuthManager.Instance.SignInAsync(username, password);

        if (result.IsSuccess)
        {
            // OnAuthStateChanged fires automatically and loads the main screen.
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
        if (!string.IsNullOrEmpty(signUpSceneName))
            UnityEngine.SceneManagement.SceneManager.LoadScene(signUpSceneName);
    }

    // ── Guest login ───────────────────────────────────────────────────────────

    private async void OnGuestLoginClicked()
    {
        if (isProcessing) return;

        string guestName = GetCurrentGuestUsername();
        if (string.IsNullOrEmpty(guestName))
        {
            ShowStatusMessage("Please enter or generate a guest username.", true);
            return;
        }

        isProcessing = true;
        SetButtonsInteractable(false);
        ShowStatusMessage($"Logging in as guest: {guestName}…", false);

        // Creates a real anonymous Unity session so guest data can be
        // upgraded to a full account later in AccountHubUI (Scene 2).
        AuthResult result = await AuthManager.Instance.SetGuestSessionAsync(guestName);

        if (result.IsSuccess)
            StartCoroutine(LoadMainScreen());
        else
        {
            ShowStatusMessage(result.ErrorMessage, true);
            isProcessing = false;
            SetButtonsInteractable(true);
        }
    }

    // ── Guest username UI ─────────────────────────────────────────────────────

    private void GenerateGuestUsername()
    {
        if (usernameGenerator == null) return;

        currentGuestUsername = usernameGenerator.GenerateGuestUsername();

        if (guestUsernameInputField != null)
            guestUsernameInputField.text = currentGuestUsername;

        if (guestUsernameText != null)
            guestUsernameText.text = $"Guest: {currentGuestUsername}";

        if (updateGuestUsernameButton != null)
            updateGuestUsernameButton.interactable = false;
    }

    private void OnGenerateGuestUsername()
    {
        GenerateGuestUsername();
        ShowStatusMessage("New guest username generated!", false);
    }

    private void OnGuestUsernameInputChanged(string newText)
    {
        if (updateGuestUsernameButton != null)
            updateGuestUsernameButton.interactable =
                !string.IsNullOrEmpty(newText) && newText != currentGuestUsername;
    }

    private void OnUpdateGuestUsername()
    {
        if (guestUsernameInputField == null || string.IsNullOrEmpty(guestUsernameInputField.text))
            return;

        currentGuestUsername = guestUsernameInputField.text.Trim();

        if (guestUsernameText != null)
            guestUsernameText.text = $"Guest: {currentGuestUsername}";

        ShowStatusMessage($"Guest username set to: {currentGuestUsername}", false);

        if (updateGuestUsernameButton != null)
            updateGuestUsernameButton.interactable = false;
    }

    private string GetCurrentGuestUsername()
    {
        if (guestUsernameInputField != null && !string.IsNullOrEmpty(guestUsernameInputField.text))
            return guestUsernameInputField.text.Trim();
        return currentGuestUsername;
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
        ShowStatusMessage("Loading game…", false);

        if (!string.IsNullOrEmpty(loadingScreenSceneName))
        {
            yield return StartCoroutine(LoadSceneAsync(loadingScreenSceneName));
            yield return new WaitForSeconds(loadingScreenDuration);
        }

        yield return StartCoroutine(LoadSceneAsync(mainScreenSceneName));
        isProcessing = false;
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) { Debug.LogError("[LoginForm] Scene name is empty!"); yield break; }
        if (!IsSceneValid(sceneName))        { Debug.LogError($"[LoginForm] Scene '{sceneName}' not in build settings!"); yield break; }

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
        if (statusText != null) statusText.text = "";
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (signInButton              != null) signInButton.interactable              = interactable;
        if (signUpButton              != null) signUpButton.interactable              = interactable;
        if (guestLoginButton          != null) guestLoginButton.interactable          = interactable;
        if (showPasswordButton        != null) showPasswordButton.interactable        = interactable;
        if (generateGuestUsernameButton != null) generateGuestUsernameButton.interactable = interactable;
        if (updateGuestUsernameButton   != null) updateGuestUsernameButton.interactable   = interactable;
    }
}