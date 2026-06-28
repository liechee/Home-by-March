using System.Collections;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;

namespace Unity.Services.Authentication.PlayerAccounts.Samples
{
    /// <summary>
    /// Scene 2 auth UI.
    ///
    /// Responsibilities:
    ///   - Subscribe to AuthManager.OnStateChanged and reflect state in the UI.
    ///   - Trigger cloud data loads exactly once per sign-in.
    ///   - Route sign-out to LogOutManager (full wipe) or AuthManager (in-memory only).
    ///
    /// This script owns NO auth logic — everything goes through AuthManager.
    /// </summary>
    public class Scene2AuthUI : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("Status UI")]
        [SerializeField] TMP_Text m_StatusText;

        [Header("Buttons")]
        [SerializeField] GameObject m_SignOutBtn;
        [SerializeField] GameObject m_SignInBtn;
        [SerializeField] GameObject m_ButtonContainer;

        [Header("Optional")]
        [SerializeField] TMP_Text m_WaitingText;

        [Header("Navigation")]
        [SerializeField] SceneChanger m_SceneChanger;
        [SerializeField] string m_Scene1Name = "Entry Screen 1";

        [Header("Cloud-load targets (auto-resolved if left empty)")]
        [SerializeField] OverallStepCounter        m_StepCounter;
        [SerializeField] PlayerPrefsCloudSyncButton m_SyncButton;
        [SerializeField] PlayerData                 m_PlayerData;

        // ── Private state ─────────────────────────────────────────────────────────

        private bool _waitingForPortalReturn;
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

        private void OnApplicationFocus(bool hasFocus)
        {
            // Player returned from the Unity Account portal.
            if (hasFocus && _waitingForPortalReturn && AuthManager1.Instance != null)
                _ = AuthManager1.Instance.StartUnitySignInAsync();
        }

        private void Start()
        {
            SetWaitingText(false);

            // Hide buttons until we know the auth state (avoids a one-frame flicker).
            m_SignOutBtn?.SetActive(false);
            m_SignInBtn?.SetActive(true);
            m_ButtonContainer?.SetActive(false);

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
                Debug.LogWarning("[Scene2AuthUI] Timed out waiting for AuthManager.IsReady.");

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

            // Skip cloud load if logout is in progress (race condition guard).
            if (m_StepCounter != null && m_StepCounter.isLoggingOut)
            {
                Debug.Log("[Scene2AuthUI] OnAuthStateChanged called during logout — skipping cloud load.");
                _cloudLoadTriggered = false;
                return;
            }

            // Trigger cloud load exactly once per sign-in.
            if (_cloudLoadTriggered) return;
            _cloudLoadTriggered = true;

            if (_waitingForPortalReturn)
            {
                _waitingForPortalReturn = false;
                SetWaitingText(false);
            }

            _ = TriggerCloudLoadsAsync();
        }

        // ── UI refresh ────────────────────────────────────────────────────────────

        private void RefreshUI()
        {
            if (AuthManager.Instance == null) return;

            bool isGuest    = AuthManager.Instance.IsGuest;
            bool isSignedIn = AuthManager.Instance.IsSignedIn;
            bool hasSession = isGuest || isSignedIn;

            if (m_StatusText != null)
            {
                var sb = new StringBuilder();
                if (isSignedIn)
                {
                    sb.AppendLine("Your journey is bound. Save your progress and carry every step with you — continue your march home from any device, anywhere.");
                }
                else if (isGuest)
                {
                    sb.AppendLine($"Playing as: <b>{AuthManager.Instance.GuestName}</b>");
                    sb.AppendLine("Sign in to save your progress.");
                }
                else
                {
                    sb.AppendLine("Your progress is not yet safe. Log in to save your journey and carry every step with you — continue from any device, anywhere.");
                }
                m_StatusText.text = sb.ToString();
            }

            // Sign-out button: show whenever there is any active session.
            m_ButtonContainer?.SetActive(true);
            m_SignOutBtn?.SetActive(hasSession);

            // Sign-in button: show when not signed in and not waiting for the portal.
            m_SignInBtn?.SetActive(!isSignedIn && !_waitingForPortalReturn);
        }

        // ── Button callbacks ──────────────────────────────────────────────────────

        /// <summary>Attach to the Sign In button's OnClick.</summary>
        public async void OnSignInButtonClicked()
        {
            if (AuthManager.Instance == null) return;

            _waitingForPortalReturn = true;
            SetWaitingText(true);
            m_SignInBtn?.SetActive(false);

            await AuthManager1.Instance.StartUnitySignInAsync();
        }

        /// <summary>
        /// Attach to the Sign Out button's OnClick.
        /// Routes to LogOutManager for a full local data wipe + scene reload.
        /// </summary>
        public void OnSignOutButtonClicked()
        {
            if (AuthManager.Instance == null) return;

            LogOutManager logoutManager = FindObjectOfType<LogOutManager>();
            if (logoutManager != null)
            {
                logoutManager.LogoutAndRestart();
            }
            else
            {
                Debug.LogError("[Scene2AuthUI] LogOutManager not found — add it to the scene. " +
                               "Falling back to in-memory sign-out (no data wipe).");
                AuthManager.Instance.SignOut();
                NavigateToScene1();
            }
        }

        // ── Cloud loads ───────────────────────────────────────────────────────────

        private async Task TriggerCloudLoadsAsync()
        {
            ResolveCloudTargets();

            if (m_StepCounter != null && !m_StepCounter.cloudLoaded)
            {
                Debug.Log("[Scene2AuthUI] Loading step data from cloud.");
                await m_StepCounter.LoadStepDataFromCloud();
            }

            if (m_SyncButton != null)
            {
                Debug.Log("[Scene2AuthUI] Loading non-step data from cloud.");
                await m_SyncButton.LoadFromCloudAsync();
            }
            else if (m_PlayerData != null)
            {
                Debug.Log("[Scene2AuthUI] Loading player data from cloud.");
                await m_PlayerData.LoadPlayerDataFromCloud();
            }

            // Persist confirmation that a cloud session is active so
            // PlayerProfileStatus can read it without waiting for AuthManager.
            if (AuthManager.Instance != null && AuthManager.Instance.IsSignedIn)
            {
                PlayerPrefs.SetInt(AuthManager.PrefPlayerSignedIn, 1);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Auto-resolves any cloud-target inspector slots left unassigned.
        /// </summary>
        private void ResolveCloudTargets()
        {
            if (m_StepCounter == null) m_StepCounter = FindObjectOfType<OverallStepCounter>();
            if (m_SyncButton  == null) m_SyncButton  = FindObjectOfType<PlayerPrefsCloudSyncButton>();
            if (m_PlayerData  == null) m_PlayerData  = FindObjectOfType<PlayerData>();

            if (m_StepCounter == null) Debug.LogWarning("[Scene2AuthUI] OverallStepCounter not found.");
            if (m_SyncButton  == null) Debug.LogWarning("[Scene2AuthUI] PlayerPrefsCloudSyncButton not found.");
            if (m_PlayerData  == null) Debug.LogWarning("[Scene2AuthUI] PlayerData not found.");
        }

        // ── Navigation ────────────────────────────────────────────────────────────

        private void NavigateToScene1()
        {
            if (m_SceneChanger != null)
                m_SceneChanger.ChangeScene(m_Scene1Name);
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(m_Scene1Name);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void SetWaitingText(bool visible)
        {
            if (m_WaitingText == null) return;
            m_WaitingText.gameObject.SetActive(visible);
            if (visible) m_WaitingText.text = "Waiting for sign-in…";
        }
    }
}