using System.Collections;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene 1 sign-up UI — username/password provider.
///
/// Responsibilities:
///   - Handle new account creation through AuthManager.
///   - Validate input before forwarding to AuthManager.
///   - Navigate back to login scene on success or cancel.
///
/// This script owns NO auth logic — everything goes through AuthManager.
/// Guest account upgrade is handled exclusively by AccountHubUI in Scene 2.
/// </summary>
public class SignUpForm : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Status UI")]
    [SerializeField] private TMP_Text statusText;

    [Header("Sign Up Panel")]
    [SerializeField] private TMP_InputField usernameInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private TMP_InputField confirmPasswordInputField;
    [SerializeField] private Button         signUpButton;
    [SerializeField] private Button         backToLoginButton;

    [Header("Password Visibility")]
    [SerializeField] private Button showPasswordButton;
    [SerializeField] private Sprite eyeOpenSprite;
    [SerializeField] private Sprite eyeClosedSprite;
    [SerializeField] private Image  passwordEyeIcon;

    [Header("Scene Loading")]
    [SerializeField] private string loginScreenSceneName = "LoginScreen";

    // ── Private state ─────────────────────────────────────────────────────────

    private bool isProcessing;
    private bool isPasswordVisible;

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

        // Disable UI until AuthManager is ready.
        SetUIInteractable(false);

        StartCoroutine(WaitForAuthThenEnable());
    }

    // ── Auth-ready coroutine ──────────────────────────────────────────────────

    /// <summary>
    /// Polls until AuthManager.IsReady before enabling the form.
    /// Handles the race between this MonoBehaviour's Start and AuthManager's async init.
    /// </summary>
    private IEnumerator WaitForAuthThenEnable()
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
        {
            Debug.LogWarning("[SignUpForm] Timed out waiting for AuthManager.IsReady.");
            ShowStatusMessage("Service unavailable. Check internet connection.", true);
            yield break;
        }

        // If a signed-in player somehow lands here, send them back.
        if (AuthManager.Instance.IsSignedIn)
        {
            OnBackToLoginClicked();
            yield break;
        }

        SetUIInteractable(true);
    }

    // ── Auth state handler ────────────────────────────────────────────────────

    private void OnAuthStateChanged()
    {
        // If sign-up succeeded and the player is now signed in, return to login.
        if (AuthManager.Instance != null && AuthManager.Instance.IsSignedIn && !isProcessing)
            OnBackToLoginClicked();
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    private void SetupButtons()
    {
        signUpButton?.onClick.AddListener(OnSignUpClicked);
        backToLoginButton?.onClick.AddListener(OnBackToLoginClicked);
    }

    private void SetupPasswordToggle()
    {
        showPasswordButton?.onClick.AddListener(TogglePasswordVisibility);

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

    // ── Sign up ───────────────────────────────────────────────────────────────

    private async void OnSignUpClicked()
    {
        if (isProcessing) return;

        string username        = usernameInputField?.text?.Trim();
        string password        = passwordInputField?.text;
        string confirmPassword = confirmPasswordInputField?.text;

        if (!ValidateUsername(username)) return;
        if (!ValidatePassword(password)) return;

        if (password != confirmPassword)
        {
            ShowStatusMessage("Passwords do not match.", true);
            return;
        }

        isProcessing = true;
        SetUIInteractable(false);
        ShowStatusMessage("Creating account…", false);

        AuthResult result = await AuthManager.Instance.SignUpAsync(username, password);

        if (result.IsSuccess)
        {
            ShowStatusMessage("Account created successfully!", false);
            await Task.Delay(1500);
            OnBackToLoginClicked();
        }
        else
        {
            ShowStatusMessage(result.ErrorMessage, true);
            isProcessing = false;
            SetUIInteractable(true);
        }
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void OnBackToLoginClicked()
    {
        if (usernameInputField        != null) usernameInputField.text        = "";
        if (passwordInputField        != null) passwordInputField.text        = "";
        if (confirmPasswordInputField != null) confirmPasswordInputField.text = "";

        if (!string.IsNullOrEmpty(loginScreenSceneName))
            UnityEngine.SceneManagement.SceneManager.LoadScene(loginScreenSceneName);
        else
            gameObject.SetActive(false);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private bool ValidateUsername(string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            ShowStatusMessage("Username is required.", true);
            return false;
        }
        if (username.Length < 3 || username.Length > 20)
        {
            ShowStatusMessage("Username must be 3–20 characters.", true);
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
        if (password.Length < 6)
        {
            ShowStatusMessage("Password must be at least 6 characters.", true);
            return false;
        }
        if (password.Length > 30)
        {
            ShowStatusMessage("Password must be less than 30 characters.", true);
            return false;
        }
        return true;
    }

    // ── Password toggle ───────────────────────────────────────────────────────

    private void TogglePasswordVisibility()
    {
        isPasswordVisible = !isPasswordVisible;

        TMP_InputField.ContentType type = isPasswordVisible
            ? TMP_InputField.ContentType.Standard
            : TMP_InputField.ContentType.Password;

        if (passwordInputField != null)
        {
            passwordInputField.contentType = type;
            passwordInputField.ForceLabelUpdate();
        }

        if (confirmPasswordInputField != null)
        {
            confirmPasswordInputField.contentType = type;
            confirmPasswordInputField.ForceLabelUpdate();
        }

        UpdateEyeIcon();
    }

    private void UpdateEyeIcon()
    {
        if (passwordEyeIcon == null) return;
        if (isPasswordVisible  && eyeOpenSprite  != null) passwordEyeIcon.sprite = eyeOpenSprite;
        if (!isPasswordVisible && eyeClosedSprite != null) passwordEyeIcon.sprite = eyeClosedSprite;
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

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

    private void SetUIInteractable(bool interactable)
    {
        if (signUpButton              != null) signUpButton.interactable              = interactable;
        if (backToLoginButton         != null) backToLoginButton.interactable         = interactable;
        if (showPasswordButton        != null) showPasswordButton.interactable        = interactable;
        if (usernameInputField        != null) usernameInputField.interactable        = interactable;
        if (passwordInputField        != null) passwordInputField.interactable        = interactable;
        if (confirmPasswordInputField != null) confirmPasswordInputField.interactable = interactable;
    }
}