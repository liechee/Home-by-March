using System.Threading.Tasks;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Unity.Services.Authentication.PlayerAccounts.Samples
{
    /// <summary>
    /// Scene 1 — login gate. Players reach this only on first launch or after sign-out.
    ///
    /// Two paths:
    ///   Guest  → player types a name → saved to PlayerPrefs → load Scene 2
    ///   Unity  → opens PlayerAccount portal → on return writes "Unity" to PlayerPrefs → load Scene 2
    ///
    /// AuthManager does NOT exist here. All communication to Scene 2 is via PlayerPrefs.
    ///
    /// Required UI:
    ///   GuestNameInput  (TMP_InputField)
    ///   PlayAsGuestBtn  (Button)
    ///   SignInBtn       (Button)
    ///   StatusText      (TMP_Text)  — optional feedback label
    /// </summary>
    public class Scene1LoginUI : MonoBehaviour
    {
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
        private bool _transitioningToScene2 = false;

        // ─────────────────────────────────────────────────────────────────────────

        private async void Start()
        {
            SetStatus("Loading…");

            m_PlayAsGuestBtn?.onClick.AddListener(OnPlayAsGuestClicked);
            m_SignInBtn?.onClick.AddListener(OnSignInClicked);

            // InitializeAsync is idempotent — safe to call in both scenes
            await UnityServices.InitializeAsync();
            _servicesReady = true;

            // Handles sign-in completion when the account portal resumes the app.
            PlayerAccountService.Instance.SignedIn += OnPlayerAccountSignedIn;

            // Leave the login screen up until the player explicitly chooses a path.
            // Scene 2 handles any state restoration after the player makes a choice.
            SetStatus("");
            SetButtonsInteractable(true);
        }

        private void OnDestroy()
        {
            if (_servicesReady)
                PlayerAccountService.Instance.SignedIn -= OnPlayerAccountSignedIn;
        }

        // ── Button handlers ───────────────────────────────────────────────────────

        private void OnPlayAsGuestClicked()
        {
            string name = m_GuestNameInput != null ? m_GuestNameInput.text.Trim() : "";
            if (string.IsNullOrEmpty(name))
            {
                SetStatus("Please enter a name to continue.");
                return;
            }

            PlayerPrefs.SetString(AuthManager.PrefLoginMode, "Guest");
            PlayerPrefs.SetString(AuthManager.PrefGuestName, name);
            PlayerPrefs.Save();
            GoToScene2();
        }

        private async void OnSignInClicked()
        {
            if (!_servicesReady) return;

            SetStatus("Opening sign-in…");
            SetButtonsInteractable(false);

            try
            {
                if (!PlayerAccountService.Instance.IsSignedIn)
                    await PlayerAccountService.Instance.StartSignInAsync();

                if (PlayerAccountService.Instance.IsSignedIn)
                {
                    ContinueToScene2WithUnityMode();
                }
                else
                {
                    SetStatus("Sign-in was cancelled. Try again.");
                    SetButtonsInteractable(true);
                }
            }
            catch (RequestFailedException ex)
            {
                Debug.LogException(ex);
                SetStatus("Sign-in failed. Try again.");
                SetButtonsInteractable(true);
            }
        }

        private void OnPlayerAccountSignedIn()
        {
            ContinueToScene2WithUnityMode();
        }

        private void ContinueToScene2WithUnityMode()
        {
            if (_transitioningToScene2) return;
            _transitioningToScene2 = true;

            PlayerPrefs.SetString(AuthManager.PrefLoginMode, "Unity");
            PlayerPrefs.Save();
            GoToScene2();
        }

        // ── Navigation ────────────────────────────────────────────────────────────

        private void GoToScene2()
        {
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