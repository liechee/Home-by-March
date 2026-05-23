using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene 2 account UI — username/password provider.
///
/// Responsibilities:
///   - Subscribe to AuthManager.OnStateChanged and reflect state in the UI.
///   - Show logout button ONLY when the player has a registered account.
///   - Show sign-in and register panels ONLY when the player is a guest.
///   - Route all auth actions through AuthManager.
///
/// This script owns NO auth logic — everything goes through AuthManager.
/// </summary>
public class AccountHubUI : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Status UI")]
    [SerializeField] TMP_Text m_StatusText;

    [Header("Buttons")]
    [SerializeField] GameObject m_SignOutBtn;
    [SerializeField] GameObject m_SignInBtn;
    [SerializeField] GameObject m_RegisterBtn;
    [SerializeField] GameObject m_ButtonContainer;

    [Header("Optional")]
    [SerializeField] TMP_Text m_WaitingText;

    [Header("Sign In Panel")]
    [Tooltip("Shown when a guest wants to sign into an existing account.")]
    [SerializeField] GameObject     m_SignInPanel;
    [SerializeField] TMP_InputField m_SignInUsernameField;
    [SerializeField] TMP_InputField m_SignInPasswordField;
    [SerializeField] Button         m_SignInConfirmBtn;
    [SerializeField] Button         m_SignInEyeBtn;
    [SerializeField] Image          m_SignInEyeIcon;
    [SerializeField] TMP_Text       m_SignInStatusText;

    [Header("Register Panel")]
    [Tooltip("Shown when a guest wants to upgrade their session to a permanent account.")]
    [SerializeField] GameObject     m_RegisterPanel;
    [SerializeField] TMP_InputField m_RegisterUsernameField;
    [SerializeField] TMP_InputField m_RegisterPasswordField;
    [SerializeField] TMP_InputField m_RegisterConfirmPasswordField;
    [SerializeField] Button         m_RegisterConfirmBtn;
    [SerializeField] Button         m_RegisterEyeBtn;
    [SerializeField] Image          m_RegisterEyeIcon;
    [SerializeField] TMP_Text       m_RegisterStatusText;

    [Header("Shared Eye Sprites")]
    [SerializeField] Sprite m_EyeOpenSprite;
    [SerializeField] Sprite m_EyeClosedSprite;

    // ── Private state ─────────────────────────────────────────────────────────

    private bool _isProcessing;
    private bool _isSignInPasswordVisible;
    private bool _isRegisterPasswordVisible;
    private bool _cloudLoadTriggered;

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
        SetWaitingText(false);

        // Hide everything until we know the auth state (avoids a one-frame flicker).
        m_SignOutBtn?.SetActive(false);
        m_SignInBtn?.SetActive(false);
        m_RegisterBtn?.SetActive(false);
        m_ButtonContainer?.SetActive(false);
        m_SignInPanel?.SetActive(false);
        m_RegisterPanel?.SetActive(false);

        SetupPasswordToggles();
        SetupButtons();

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
            Debug.LogWarning("[AccountHubUI] Timed out waiting for AuthManager.IsReady.");

        OnAuthStateChanged();
    }

    // ── Auth state handler ────────────────────────────────────────────────────

    private void OnAuthStateChanged()
    {
        RefreshUI();

        // Reset cloud-load gate if we're no longer signed in.
        if (AuthManager.Instance == null || !AuthManager.Instance.IsSignedIn)
        {
            _cloudLoadTriggered = false;
            return;
        }

        // Trigger cloud load exactly once per sign-in.
        if (_cloudLoadTriggered) return;
        _cloudLoadTriggered = true;

        _ = AuthManager.Instance.LoadProfileFromCloud();
    }

    // ── UI refresh ────────────────────────────────────────────────────────────

    private void RefreshUI()
    {
        if (AuthManager.Instance == null) return;

        bool isGuest    = AuthManager.Instance.IsGuest;
        bool isSignedIn = AuthManager.Instance.IsSignedIn;

        if (m_StatusText != null)
        {
            var sb = new StringBuilder();
            if (isSignedIn)
            {
                string name = AuthManager.Instance.CloudUsername
                              ?? PlayerPrefs.GetString("LastSignedInPlayer", "Player");
                sb.AppendLine($"Signed in as: <b>{name}</b>");
                sb.AppendLine("Your progress is saved across all devices.");
            }
            else if (isGuest)
            {
                sb.AppendLine($"Playing as: <b>{AuthManager.Instance.GuestName}</b>");
                sb.AppendLine("Sign in or register to save your progress.");
            }
            else
            {
                sb.AppendLine("Your progress is not saved. Sign in or register to keep it.");
            }
            m_StatusText.text = sb.ToString();
        }

        m_ButtonContainer?.SetActive(true);

        // Logout button: registered players ONLY.
        m_SignOutBtn?.SetActive(isSignedIn);

        // Sign-in and register buttons: guests ONLY.
        m_SignInBtn?.SetActive(isGuest && !_isProcessing);
        m_RegisterBtn?.SetActive(isGuest && !_isProcessing);

        // Close panels if the player just signed in.
        if (isSignedIn)
        {
            m_SignInPanel?.SetActive(false);
            m_RegisterPanel?.SetActive(false);
            SetWaitingText(false);
        }
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    private void SetupButtons()
    {
        if (m_SignInBtn != null)
            m_SignInBtn.GetComponent<Button>()?.onClick.AddListener(OnSignInButtonClicked);

        if (m_RegisterBtn != null)
            m_RegisterBtn.GetComponent<Button>()?.onClick.AddListener(OnRegisterButtonClicked);

        if (m_SignOutBtn != null)
            m_SignOutBtn.GetComponent<Button>()?.onClick.AddListener(OnSignOutButtonClicked);

        m_SignInConfirmBtn?.onClick.AddListener(OnSignInConfirmClicked);
        m_RegisterConfirmBtn?.onClick.AddListener(OnRegisterConfirmClicked);
    }

    private void SetupPasswordToggles()
    {
        m_SignInEyeBtn?.onClick.AddListener(ToggleSignInPassword);
        m_RegisterEyeBtn?.onClick.AddListener(ToggleRegisterPassword);

        if (m_SignInPasswordField != null)
        {
            m_SignInPasswordField.contentType = TMP_InputField.ContentType.Password;
            m_SignInPasswordField.ForceLabelUpdate();
        }

        if (m_RegisterPasswordField != null)
        {
            m_RegisterPasswordField.contentType = TMP_InputField.ContentType.Password;
            m_RegisterPasswordField.ForceLabelUpdate();
        }

        if (m_RegisterConfirmPasswordField != null)
        {
            m_RegisterConfirmPasswordField.contentType = TMP_InputField.ContentType.Password;
            m_RegisterConfirmPasswordField.ForceLabelUpdate();
        }
    }

    // ── Button callbacks ──────────────────────────────────────────────────────

    /// <summary>Attach to the Sign In button's OnClick.</summary>
    public void OnSignInButtonClicked()
    {
        if (AuthManager.Instance == null) return;

        SetWaitingText(true);
        m_SignInPanel?.SetActive(true);
        m_RegisterPanel?.SetActive(false);
    }

    /// <summary>Attach to the Register button's OnClick.</summary>
    public void OnRegisterButtonClicked()
    {
        if (AuthManager.Instance == null) return;

        m_RegisterPanel?.SetActive(true);
        m_SignInPanel?.SetActive(false);
        SetWaitingText(false);
    }

    /// <summary>Attach to the Sign Out button's OnClick.</summary>
    public void OnSignOutButtonClicked()
    {
        if (AuthManager.Instance == null) return;

        AuthManager.Instance.SignOut();

        m_SignInPanel?.SetActive(false);
        m_RegisterPanel?.SetActive(false);
        SetWaitingText(false);
        ClearAllInputs();
        // RefreshUI fires automatically via AuthManager.OnStateChanged.
    }

    // ── Sign in (guest → existing account) ───────────────────────────────────

    private async void OnSignInConfirmClicked()
    {
        if (_isProcessing) return;

        string username = m_SignInUsernameField?.text?.Trim();
        string password = m_SignInPasswordField?.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowStatus(m_SignInStatusText, "Please enter username and password.", true);
            return;
        }

        _isProcessing = true;
        SetPanelInteractable(m_SignInPanel, false);
        ShowStatus(m_SignInStatusText, "Signing in…", false);

        AuthResult result = await AuthManager.Instance.SignInAsync(username, password);

        if (result.IsSuccess)
        {
            ClearAllInputs();
            SetWaitingText(false);
            // RefreshUI fires automatically via AuthManager.OnStateChanged.
        }
        else
        {
            ShowStatus(m_SignInStatusText, result.ErrorMessage, true);
        }

        _isProcessing = false;
        SetPanelInteractable(m_SignInPanel, true);
    }

    // ── Register (guest → new account, data preserved) ───────────────────────

    private async void OnRegisterConfirmClicked()
    {
        if (_isProcessing) return;

        string username        = m_RegisterUsernameField?.text?.Trim();
        string password        = m_RegisterPasswordField?.text;
        string confirmPassword = m_RegisterConfirmPasswordField?.text;

        if (!ValidateUsername(username)) return;
        if (!ValidatePassword(password)) return;

        if (password != confirmPassword)
        {
            ShowStatus(m_RegisterStatusText, "Passwords do not match.", true);
            return;
        }

        _isProcessing = true;
        SetPanelInteractable(m_RegisterPanel, false);
        ShowStatus(m_RegisterStatusText, "Creating account…", false);

        // Links credentials to the existing guest player ID — all cloud data carries over.
        AuthResult result = await AuthManager.Instance.UpgradeGuestToAccountAsync(username, password);

        if (result.IsSuccess)
        {
            ShowStatus(m_RegisterStatusText, "Account created! Your progress is saved.", false);
            await Task.Delay(1500);
            m_RegisterPanel?.SetActive(false);
            SetWaitingText(false);
            ClearAllInputs();
            // RefreshUI fires automatically via AuthManager.OnStateChanged.
        }
        else
        {
            ShowStatus(m_RegisterStatusText, result.ErrorMessage, true);
        }

        _isProcessing = false;
        SetPanelInteractable(m_RegisterPanel, true);
    }

    // ── Password toggles ──────────────────────────────────────────────────────

    private void ToggleSignInPassword()
    {
        _isSignInPasswordVisible = !_isSignInPasswordVisible;

        if (m_SignInPasswordField != null)
        {
            m_SignInPasswordField.contentType = _isSignInPasswordVisible
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;
            m_SignInPasswordField.ForceLabelUpdate();
        }

        UpdateEyeIcon(m_SignInEyeIcon, _isSignInPasswordVisible);
    }

    private void ToggleRegisterPassword()
    {
        _isRegisterPasswordVisible = !_isRegisterPasswordVisible;

        TMP_InputField.ContentType type = _isRegisterPasswordVisible
            ? TMP_InputField.ContentType.Standard
            : TMP_InputField.ContentType.Password;

        if (m_RegisterPasswordField != null)
        {
            m_RegisterPasswordField.contentType = type;
            m_RegisterPasswordField.ForceLabelUpdate();
        }

        if (m_RegisterConfirmPasswordField != null)
        {
            m_RegisterConfirmPasswordField.contentType = type;
            m_RegisterConfirmPasswordField.ForceLabelUpdate();
        }

        UpdateEyeIcon(m_RegisterEyeIcon, _isRegisterPasswordVisible);
    }

    private void UpdateEyeIcon(Image icon, bool isVisible)
    {
        if (icon == null) return;
        if (isVisible  && m_EyeOpenSprite  != null) icon.sprite = m_EyeOpenSprite;
        if (!isVisible && m_EyeClosedSprite != null) icon.sprite = m_EyeClosedSprite;
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private bool ValidateUsername(string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            ShowStatus(m_RegisterStatusText, "Username is required.", true);
            return false;
        }
        if (username.Length < 3 || username.Length > 20)
        {
            ShowStatus(m_RegisterStatusText, "Username must be 3–20 characters.", true);
            return false;
        }
        if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$"))
        {
            ShowStatus(m_RegisterStatusText, "Letters, numbers, and underscores only.", true);
            return false;
        }
        return true;
    }

    private bool ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 6)
        {
            ShowStatus(m_RegisterStatusText, "Password must be at least 6 characters.", true);
            return false;
        }
        if (password.Length > 30)
        {
            ShowStatus(m_RegisterStatusText, "Password must be less than 30 characters.", true);
            return false;
        }
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetWaitingText(bool visible)
    {
        if (m_WaitingText == null) return;
        m_WaitingText.gameObject.SetActive(visible);
        if (visible) m_WaitingText.text = "Waiting for sign-in…";
    }

    private void ShowStatus(TMP_Text target, string message, bool isError)
    {
        if (target == null) return;
        target.text  = message;
        target.color = isError ? Color.red : Color.green;

        if (!string.IsNullOrEmpty(message))
            StartCoroutine(ClearStatusAfterDelay(target, 5f));
    }

    private IEnumerator ClearStatusAfterDelay(TMP_Text target, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (target != null) target.text = "";
    }

    private void ClearAllInputs()
    {
        if (m_SignInUsernameField           != null) m_SignInUsernameField.text           = "";
        if (m_SignInPasswordField           != null) m_SignInPasswordField.text           = "";
        if (m_RegisterUsernameField         != null) m_RegisterUsernameField.text         = "";
        if (m_RegisterPasswordField         != null) m_RegisterPasswordField.text         = "";
        if (m_RegisterConfirmPasswordField  != null) m_RegisterConfirmPasswordField.text  = "";
    }

    private void SetPanelInteractable(GameObject panel, bool interactable)
    {
        if (panel == null) return;
        foreach (Selectable s in panel.GetComponentsInChildren<Selectable>())
            s.interactable = interactable;
    }
}