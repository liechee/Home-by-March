using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoginForm : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Status UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text welcomeText;

    [Header("Login Panel")]
    [SerializeField] private TMP_InputField usernameInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private Button signInButton;
    [SerializeField] private Button signUpButton;

    [Header("Password Visibility")]
    [SerializeField] private Button showPasswordButton;
    [SerializeField] private Sprite eyeOpenSprite;
    [SerializeField] private Sprite eyeClosedSprite;
    [SerializeField] private Image eyeIconImage;

    [Header("Scene Loading")]
    [SerializeField] private string mainScreenSceneName = "Main Screen";
    [SerializeField] private string signUpSceneName = "SignUpScene";

    // ── Private state ─────────────────────────────────────────────────────────

    private GuestUsernameGenerator usernameGenerator;
    private string currentGuestUsername;
    private bool isProcessing;
    private bool isPasswordVisible;
    private bool isLoadingScene;
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
        FindPlayerData();
        SetupPasswordToggle();
        SetupButtons();

        isLoadingScene = false;

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
            FindPlayerData();

        resolvedPlayerData = playerData;
        return resolvedPlayerData != null;
    }

    // ── Auth-ready coroutine ──────────────────────────────────────────────────

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

    private void SyncPlayerDataName(string username)
    {
        if (string.IsNullOrEmpty(username)) return;

        if (TryGetPlayerData(out var resolvedPlayerData))
        {
            resolvedPlayerData.ChangePlayerName(username);
            Debug.Log($"[LoginForm] Synced player name to PlayerData: {username}");
        }
    }

    // ── Auth state handler ────────────────────────────────────────────────────

    private void OnAuthStateChanged()
    {
        if (AuthManager.Instance == null) return;

        if (AuthManager.Instance.IsSignedIn && !AuthManager.Instance.IsInteractiveAuthInProgress)
        {
            string name = AuthManager.Instance.CloudUsername
                          ?? AuthManager.Instance.GuestName
                          ?? AuthManager.Instance.CurrentPlayerData?.playerName;

            bool isExplicitGuestLogin = PlayerPrefs.GetString("LastLoginMethod", string.Empty) == "Guest"
                                        && !string.IsNullOrWhiteSpace(AuthManager.Instance.GuestName);
            bool isExplicitAccountLogin = PlayerPrefs.GetInt(AuthManager.PrefPlayerSignedIn, 0) == 1
                                          && !string.IsNullOrWhiteSpace(name);

            if (string.IsNullOrWhiteSpace(name) || (!isExplicitGuestLogin && !isExplicitAccountLogin))
            {
                Debug.LogWarning("[LoginForm] No explicit login state or valid player name. Staying on login screen.");
                ShowWelcomeMessage("");
                isProcessing = false;
                SetButtonsInteractable(true);
                return;
            }

            SyncPlayerDataName(name);
            ShowWelcomeMessage($"Welcome back, {name}!");

            if (!isLoadingScene && !IsInMainScene())
                _ = LoadCloudStepsThenNavigate();
        }
        else
        {
            ShowWelcomeMessage("");
            isProcessing = false;
            SetButtonsInteractable(true);
        }
    }

    // ── Helper to check if already in main scene ──────────────────────────────

    private bool IsInMainScene()
    {
        return SceneManager.GetActiveScene().name == mainScreenSceneName;
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

        if (AuthManager.Instance.IsSignedIn)
        {
            Debug.Log("[LoginForm] Existing session detected. Signing out before new sign in.");
            AuthManager.Instance.SignOut();
            await Task.Delay(100);
        }

        AuthResult result = await AuthManager.Instance.SignInAsync(username, password);

        if (result.IsSuccess)
        {
            ShowStatusMessage("Sign in successful!", false);
            // ← Directly trigger cloud load on the persistent step counter
            OverallStepCounter stepCounter = FindObjectOfType<OverallStepCounter>(true);
            if (stepCounter != null)
            {
                PlayerPrefs.DeleteKey("SuppressCloudRestore");
                PlayerPrefs.DeleteKey("HasLoggedOut");
                PlayerPrefs.SetString("LastLoginMethod", "UsernamePassword");
                PlayerPrefs.SetInt("IsGuestSession", 0);
                PlayerPrefs.Save();

                stepCounter.RestartAsNewSession();
            }
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

        if (AuthManager.Instance != null && AuthManager.Instance.IsSignedIn)
        {
            Debug.Log("[LoginForm] Signing out existing session before sign-up.");
            AuthManager.Instance.SignOut();
        }

        if (!string.IsNullOrEmpty(signUpSceneName))
            SceneManager.LoadScene(signUpSceneName);
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
        if (isPasswordVisible && eyeOpenSprite != null) eyeIconImage.sprite = eyeOpenSprite;
        if (!isPasswordVisible && eyeClosedSprite != null) eyeIconImage.sprite = eyeClosedSprite;
    }

    // ── Cloud step pre-load ─────────────────────────────────────────────────────────────

    private async Task LoadCloudStepsThenNavigate()
    {
        // OverallStepCounter stepCounter = FindObjectOfType<OverallStepCounter>();
        // if (stepCounter != null && !stepCounter.cloudLoaded)
        // {
        //     Debug.Log("[LoginForm] Pre-loading cloud step data before scene transition…");
        //     await stepCounter.LoadStepDataFromCloud();
        //     Debug.Log("[LoginForm] Cloud step data loaded.");
        // }

        LoadMainScreen();
    }

    // ── Scene loading ─────────────────────────────────────────────────────────

    private void LoadMainScreen()
    {
        if (isLoadingScene) return;
        isLoadingScene = true;

        ShowStatusMessage("Loading game…", false);
        Debug.Log($"[LoginForm] Loading main screen: '{mainScreenSceneName}'");

        SceneManager.LoadScene(mainScreenSceneName);
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
        statusText.text = message;
        statusText.color = isError ? Color.red : Color.green;

        if (!string.IsNullOrEmpty(message))
            StartCoroutine(ClearStatusAfterDelay(5f));
    }

    private IEnumerator ClearStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (statusText != null && !statusText.text.Contains("Welcome"))
            statusText.text = "";
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (signInButton != null) signInButton.interactable = interactable;
        if (signUpButton != null) signUpButton.interactable = interactable;
        if (showPasswordButton != null) showPasswordButton.interactable = interactable;
    }
}