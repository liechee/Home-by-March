using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Unity.Services.Authentication.PlayerAccounts.Samples
{
    public class Scene1LoginUI : MonoBehaviour
    {
        private const string Scene1SignedInKey = "SignedInFromScene1Auth";

        [Header("UI References")]
        [SerializeField] TMP_InputField m_GuestNameInput;
        [SerializeField] Button         m_PlayAsGuestBtn;
        [SerializeField] Button         m_SignInBtn;
        [SerializeField] TMP_Text       m_StatusText;

        [Header("Scene Changer")]
        [Tooltip("Drag the GameObject that has SceneChanger on it here.")]
        [SerializeField] SceneChanger m_SceneChanger;
        [Tooltip("Name of Scene 2 as it appears in Build Settings.")]
        [SerializeField] string m_Scene2Name = "Main Screen";

        private bool _servicesReady = false;
        private bool _waitingForPlayerAccountReturn = false;
        private bool _signInCompletionHandled = false;

        // ─────────────────────────────────────────────────────────────────────────

        private async void Start()
        {
            Debug.Log("[Scene1LoginUI] Start() entered.");
            SetButtonsInteractable(false);
            SetStatus("Loading…");

            m_PlayAsGuestBtn?.onClick.AddListener(OnPlayAsGuestClicked);
            m_SignInBtn?.onClick.AddListener(OnSignInClicked);

            Debug.Log("[Scene1LoginUI] Initializing Unity Services...");
            await UnityServices.InitializeAsync();
            _servicesReady = true;

            PlayerAccountService.Instance.SignedIn += OnPlayerAccountSignedIn;
            Debug.Log("[Scene1LoginUI] Unity Services ready.");

            // Auto-restore on launch:
            // 1) Unity session token (signed-in users)
            // 2) Guest handoff key (guest users)
            if (await TryAutoResumeSessionOrGuestAsync())
                return;

            // No restorable session found: keep Scene 1 visible and wait for input.
            SetStatus("");
            SetButtonsInteractable(true);
            Debug.Log("[Scene1LoginUI] Scene 1 UI enabled.");
        }

        private async System.Threading.Tasks.Task<bool> TryAutoResumeSessionOrGuestAsync()
        {
            if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
            {
                Debug.Log("[Scene1LoginUI] Explicit logout detected. Skipping auto-resume.");
                return false;
            }

            try
            {
                if (AuthenticationService.Instance.SessionTokenExists)
                {
                    SetStatus("Restoring session…");
                    Debug.Log("[Scene1LoginUI] Session token found. Attempting silent sign-in...");

                    // This SDK version does not expose SignInWithSessionTokenAsync().
                    // SignInAnonymouslyAsync will restore the cached player when a
                    // valid session token exists, without opening a browser.
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                    if (AuthenticationService.Instance.IsSignedIn)
                    {
                        PlayerPrefs.SetString(AuthManager.PrefLoginMode, "Unity");
                        PlayerPrefs.SetInt(Scene1SignedInKey, 1);
                        PlayerPrefs.Save();
                        Debug.Log("[Scene1LoginUI] Silent sign-in succeeded. Loading Scene 2...");
                        GoToScene2();
                        return true;
                    }
                }
            }
            catch (RequestFailedException ex)
            {
                // Token may be expired or invalid; continue with guest/manual login.
                Debug.LogWarning($"[Scene1LoginUI] Silent sign-in failed: {ex.Message}");
            }

            if (PlayerPrefs.HasKey(AuthManager.PrefGuestName))
            {
                string guestName = PlayerPrefs.GetString(AuthManager.PrefGuestName, "").Trim();
                if (!string.IsNullOrEmpty(guestName))
                {
                    PlayerPrefs.SetString(AuthManager.PrefLoginMode, "Guest");
                    PlayerPrefs.Save();
                    Debug.Log($"[Scene1LoginUI] Restored guest '{guestName}'. Loading Scene 2...");
                    GoToScene2();
                    return true;
                }
            }

            return false;
        }

        private void OnDestroy()
        {
            if (PlayerAccountService.Instance != null)
                PlayerAccountService.Instance.SignedIn -= OnPlayerAccountSignedIn;
        }

        // ── Button handlers ───────────────────────────────────────────────────────

        private void OnPlayAsGuestClicked()
        {
            Debug.Log("[Scene1LoginUI] Play As Guest clicked.");
            string name = m_GuestNameInput != null ? m_GuestNameInput.text.Trim() : "";
            if (string.IsNullOrEmpty(name))
            {
                SetStatus("Please enter a name to continue.");
                Debug.LogWarning("[Scene1LoginUI] Guest name is empty.");
                return;
            }
            PlayerPrefs.SetString(AuthManager.PrefLoginMode, "Guest");
            PlayerPrefs.SetString(AuthManager.PrefGuestName, name);
            PlayerPrefs.DeleteKey("HasLoggedOut");
            PlayerPrefs.Save();
            Debug.Log($"[Scene1LoginUI] Guest login saved for '{name}'. Loading Scene 2...");
            GoToScene2();
        }

        private async void OnSignInClicked()
        {
            Debug.Log("[Scene1LoginUI] Sign In clicked.");
            if (!_servicesReady) return;

            SetStatus("Opening sign-in…");
            SetButtonsInteractable(false);
            _waitingForPlayerAccountReturn = true;
            _signInCompletionHandled = false;

            try
            {
                // Step 1: open the Unity PlayerAccount portal (browser)
                Debug.Log("[Scene1LoginUI] Checking PlayerAccount sign-in state...");
                if (!PlayerAccountService.Instance.IsSignedIn)
                {
                    Debug.Log("[Scene1LoginUI] Starting PlayerAccount sign-in...");
                    await PlayerAccountService.Instance.StartSignInAsync();
                }

                if (PlayerAccountService.Instance.IsSignedIn)
                {
                    Debug.Log("[Scene1LoginUI] PlayerAccount already signed in after StartSignInAsync.");
                    await CompleteUnitySignInAndLoadSceneAsync();
                    return;
                }

                SetStatus("Complete sign-in in the browser, then return here.");
                Debug.Log("[Scene1LoginUI] Waiting for PlayerAccountService.SignedIn event.");
            }
            catch (RequestFailedException ex)
            {
                Debug.LogException(ex);
                SetStatus("Sign-in failed. Try again.");
                Debug.LogError("[Scene1LoginUI] Sign-in failed.");
                _waitingForPlayerAccountReturn = false;
                SetButtonsInteractable(true);
            }
        }

        private async void OnPlayerAccountSignedIn()
        {
            if (!_waitingForPlayerAccountReturn || _signInCompletionHandled)
                return;

            await CompleteUnitySignInAndLoadSceneAsync();
        }

        private async System.Threading.Tasks.Task CompleteUnitySignInAndLoadSceneAsync()
        {
            if (_signInCompletionHandled)
                return;

            _signInCompletionHandled = true;

            try
            {
                string token = PlayerAccountService.Instance.AccessToken;
                if (string.IsNullOrEmpty(token))
                {
                    Debug.LogWarning("[Scene1LoginUI] PlayerAccount access token is empty.");
                    SetStatus("Sign-in failed. Try again.");
                    _waitingForPlayerAccountReturn = false;
                    SetButtonsInteractable(true);
                    return;
                }

                Debug.Log("[Scene1LoginUI] PlayerAccount sign-in complete. Exchanging token for Unity Auth session...");
                await AuthenticationService.Instance.SignInWithUnityAsync(token);

                Debug.Log("[Scene1] Sign-in complete.");
                PlayerPrefs.SetString(AuthManager.PrefLoginMode, "Unity");
                PlayerPrefs.SetInt(Scene1SignedInKey, 1);
                PlayerPrefs.DeleteKey("HasLoggedOut");
                PlayerPrefs.Save();
                _waitingForPlayerAccountReturn = false;
                Debug.Log("[Scene1LoginUI] Saved Unity login mode. Loading Scene 2...");
                GoToScene2();
            }
            catch (RequestFailedException ex)
            {
                Debug.LogException(ex);
                SetStatus("Sign-in failed. Try again.");
                Debug.LogError("[Scene1LoginUI] Sign-in failed.");
                _waitingForPlayerAccountReturn = false;
                SetButtonsInteractable(true);
            }
        }

        // ── Navigation ────────────────────────────────────────────────────────────

        private void GoToScene2()
        {
            Debug.Log($"[Scene1LoginUI] Loading scene '{m_Scene2Name}'.");
            if (m_SceneChanger != null)
                m_SceneChanger.ChangeScene(m_Scene2Name);
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(m_Scene2Name);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void SetStatus(string msg)
        {
            if (m_StatusText != null) m_StatusText.text = msg;
        }

        private void SetButtonsInteractable(bool on)
        {
            if (m_PlayAsGuestBtn != null) m_PlayAsGuestBtn.interactable = on;
            if (m_SignInBtn      != null) m_SignInBtn.interactable      = on;
        }
    }
}