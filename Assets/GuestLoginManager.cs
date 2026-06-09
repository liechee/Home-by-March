using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;

public class GuestLoginManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField usernameDisplayText;
    [SerializeField] private TMP_InputField usernameInputField;
    [SerializeField] private Button generateNewButton;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button updateUsernameButton;

    [Header("Scene Loading")]
    [SerializeField] private string mainScreenSceneName = "Main Screen";

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

    public string CurrentUsername => currentSystemGeneratedUsername;
    public string SavedUsername => savedUsername;
    public bool IsInitialized => isInitialized;
    private PlayerData playerData;

    private void Awake()
    {
        if (generateOnAwake)
            InitializeAndGenerateUsername();
    }

    private void Start()
    {
        if (!isInitialized && generateOnStart)
            InitializeAndGenerateUsername();
    }

    private void InitializeAndGenerateUsername()
    {
        ClearStaleGuestLoginDraft();

        usernameGenerator = FindObjectOfType<GuestUsernameGenerator>();

        if (usernameGenerator == null)
        {
            Debug.LogError("GuestUsernameGenerator not found in scene! Creating one...");
            GameObject generatorObj = new GameObject("GuestUsernameGenerator");
            usernameGenerator = generatorObj.AddComponent<GuestUsernameGenerator>();
        }

        if (playerData == null)
        {
            playerData = FindObjectOfType<PlayerData>();
            if (playerData == null)
                Debug.Log("GuestLoginManager: PlayerData not found in scene.");
        }

        SetupButtons();
        SetupInputField();
        GenerateAndDisplayUsername();

        isInitialized = true;
        Debug.Log("GuestLoginManager initialized and ready");
    }

    private void ClearStaleGuestLoginDraft()
    {
        if (AuthManager.Instance != null && AuthManager.Instance.IsSignedIn)
            return;

        PlayerPrefs.DeleteKey("LastGuestUsername");
        PlayerPrefs.DeleteKey("LastLoginMethod");
        PlayerPrefs.Save();
    }

    private void CommitGuestIdentity(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return;

        savedUsername = username.Trim();
        SyncPlayerDataName(savedUsername);
        PlayerPrefs.SetString("LastGuestUsername", savedUsername);
        PlayerPrefs.SetString("LastLoginMethod", "Guest");
        PlayerPrefs.Save();
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
            SetUpdateButtonState(false);
        }
    }

    private void SetupInputField()
    {
        if (usernameInputField != null)
        {
            usernameInputField.onValueChanged.AddListener(OnUsernameInputChanged);
            usernameInputField.onSubmit.AddListener(OnUsernameSubmit);
        }
    }

    private void OnUsernameInputChanged(string newText)
    {
        if (!isInitialized) return;

        bool shouldEnable = !string.IsNullOrEmpty(newText) && newText != currentSystemGeneratedUsername;
        SetUpdateButtonState(shouldEnable);

        if (!string.IsNullOrEmpty(newText) && usernameGenerator != null && usernameGenerator.IsUsernameTaken(newText))
        {
            if (usernameInputField.targetGraphic != null)
                usernameInputField.targetGraphic.color = Color.red;

            if (shouldEnable)
                SetUpdateButtonState(false);
        }
        else
        {
            if (usernameInputField.targetGraphic != null)
                usernameInputField.targetGraphic.color = Color.white;
        }
    }

    private void OnUsernameSubmit(string text)
    {
        if (updateUsernameButton != null && updateUsernameButton.interactable)
            OnUpdateUsername();
    }

    private void GenerateAndDisplayUsername()
    {
        currentSystemGeneratedUsername = usernameGenerator.GenerateGuestUsername();
        originalUsername = currentSystemGeneratedUsername;

        if (usernameDisplayText != null)
            usernameDisplayText.text = $"System Generated: {currentSystemGeneratedUsername}";

        if (usernameInputField != null)
            usernameInputField.text = currentSystemGeneratedUsername;

        SetUpdateButtonState(false);
        Debug.Log($"Generated guest username: {currentSystemGeneratedUsername}");
    }

    private void OnUpdateUsername()
    {
        if (isProcessingLogin) return;

        if (usernameInputField == null || string.IsNullOrEmpty(usernameInputField.text))
            return;

        string newUsername = usernameInputField.text.Trim();

        if (usernameGenerator.IsUsernameTaken(newUsername))
        {
            Debug.LogWarning($"Username '{newUsername}' is already taken!");
            if (usernameDisplayText != null)
            {
                usernameDisplayText.text = $"Error: '{newUsername}' is already taken!";
                StartCoroutine(ResetDisplayTextAfterDelay(2f));
            }
            return;
        }

        CommitGuestIdentity(newUsername);
        currentSystemGeneratedUsername = newUsername;
        originalUsername = newUsername;
        usernameGenerator.RegisterUsername(currentSystemGeneratedUsername);

        if (usernameDisplayText != null)
            usernameDisplayText.text = $"Username Saved: {currentSystemGeneratedUsername}";

        SetUpdateButtonState(false);

        if (usernameInputField != null)
            usernameInputField.interactable = false;

        if (generateNewButton != null)
            generateNewButton.interactable = false;

        Debug.Log($"Username updated and saved to: {currentSystemGeneratedUsername}");

        LoadMainScreen();
    }

    private void SyncPlayerDataName(string username)
    {
        if (string.IsNullOrEmpty(username)) return;

        if (playerData == null)
            playerData = FindObjectOfType<PlayerData>();

        if (playerData != null)
        {
            playerData.ChangePlayerName(username);
            return;
        }

        try
        {
            if (AuthManager.Instance == null)
            {
                Debug.LogWarning("GuestLoginManager: AuthManager.Instance is null.");
            }
            else
            {
                Debug.Log($"GuestLoginManager: calling AuthManager.SetGuestSessionAsync");
                _ = AuthManager.Instance.SetGuestSessionAsync(username);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"GuestLoginManager: SetGuestSessionAsync failed: {e.Message}");
        }
    }

    private void OnGenerateNewUsername()
    {
        if (isProcessingLogin) return;

        currentSystemGeneratedUsername = usernameGenerator.GenerateGuestUsername();
        originalUsername = currentSystemGeneratedUsername;

        if (usernameDisplayText != null)
            usernameDisplayText.text = $"System Generated: {currentSystemGeneratedUsername}";

        if (usernameInputField != null)
            usernameInputField.text = currentSystemGeneratedUsername;

        SetUpdateButtonState(false);
        Debug.Log($"New system username generated: {currentSystemGeneratedUsername}");
    }
    private void OnLoginAsGuest()
    {
        if (isProcessingLogin) return;

        if (!string.IsNullOrEmpty(currentSystemGeneratedUsername))
        {
            CommitGuestIdentity(currentSystemGeneratedUsername);
            Debug.Log($"Logging in as guest: {currentSystemGeneratedUsername}");

            if (usernameDisplayText != null)
                usernameDisplayText.text = $"{currentSystemGeneratedUsername}...";

            SetUIInteractable(false);

            // Initialize step counter for guest BEFORE loading main screen
            StartCoroutine(InitializeStepCounterAndLoadMainScreen());
        }
    }

    private IEnumerator InitializeStepCounterAndLoadMainScreen()
    {
        Debug.Log("[GuestLogin] Initializing step counter for guest...");
        PlayerPrefs.DeleteKey("HasLoggedOut");
        PlayerPrefs.SetInt("IsGuestSession", 1);
        PlayerPrefs.SetInt("SuppressCloudRestore", 1);
        PlayerPrefs.SetInt("SuppressStepQuery", 0);
        PlayerPrefs.SetString("LastLoginMethod", "Guest");
        PlayerPrefs.SetInt("GuestLoginPending", 1);
        PlayerPrefs.Save();

        // Find or create OverallStepCounter
        OverallStepCounter stepCounter = FindObjectOfType<OverallStepCounter>(true);

        if (stepCounter != null)
        {
            // stepCounter = FindObjectOfType<OverallStepCounter>(true);
            // if (stepCounter == null)
            // {
            //     Debug.LogWarning("[GuestLogin] OverallStepCounter not found, creating one...");
            //     // GameObject stepCounterObj = new GameObject("OverallStepCounter");
            //     // stepCounter = stepCounterObj.AddComponent<OverallStepCounter>();
            //     stepCounter.PrepareForGuestLogin();
            //     stepCounter.InitializeForGuestLogin();
            // }
            stepCounter.PrepareForGuestLogin();

            // ← Step 2: initialize and start counting
            stepCounter.InitializeForGuestLogin();

            Debug.Log("[GuestLogin] InitializeForGuestLogin called.");
        }
        else
        {
            Debug.LogWarning("[GuestLogin] Still not found — setting PlayerPrefs flags only");
            PlayerPrefs.DeleteKey("HasLoggedOut");
            PlayerPrefs.SetInt("IsGuestSession", 1);
            PlayerPrefs.SetInt("SuppressCloudRestore", 1);
            PlayerPrefs.SetInt("SuppressStepQuery", 0);
            PlayerPrefs.SetString("LastLoginMethod", "Guest");
            PlayerPrefs.Save();
        }

        // // Initialize step counter for guest login
        // stepCounter.InitializeForGuestLogin();

        // // Wait a moment for the step query to complete
        // yield return new WaitForSeconds(0.5f);
        yield return new WaitForSeconds(0.5f);

        Debug.Log("[GuestLogin] Step counter initialized, loading main screen...");
        LoadMainScreen();
    }

    // ── Scene Loading ─────────────────────────────────────────────────────────

    private void LoadMainScreen()
    {
        if (isProcessingLogin) return;
        isProcessingLogin = true;

        Debug.Log($"[GuestLoginManager] Loading main screen: '{mainScreenSceneName}'");
        SceneManager.LoadScene(mainScreenSceneName);
    }

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

    private void SetUpdateButtonState(bool isEnabled)
    {
        if (updateUsernameButton != null && !isProcessingLogin)
        {
            updateUsernameButton.interactable = isEnabled;

            var buttonColors = updateUsernameButton.colors;
            buttonColors.normalColor = isEnabled ? enabledButtonColor : disabledButtonColor;
            updateUsernameButton.colors = buttonColors;
        }
    }

    private IEnumerator ResetDisplayTextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (usernameDisplayText != null)
            usernameDisplayText.text = $"{currentSystemGeneratedUsername}";
    }

    public void ForceGenerateNewUsername()
    {
        if (usernameGenerator != null)
            GenerateAndDisplayUsername();
        else
        {
            Debug.LogWarning("Username generator not initialized yet!");
            InitializeAndGenerateUsername();
        }
    }

    public string GetCurrentUsername() => currentSystemGeneratedUsername;
    public string GetSavedUsername() => savedUsername;

    public bool SetCustomUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;

        username = username.Trim();

        if (usernameGenerator.IsUsernameTaken(username)) return false;

        currentSystemGeneratedUsername = username;
        originalUsername = username;
        usernameGenerator.RegisterUsername(currentSystemGeneratedUsername);

        if (usernameDisplayText != null)
            usernameDisplayText.text = $"{currentSystemGeneratedUsername}";

        if (usernameInputField != null)
            usernameInputField.text = currentSystemGeneratedUsername;

        SetUpdateButtonState(false);
        return true;
    }
}