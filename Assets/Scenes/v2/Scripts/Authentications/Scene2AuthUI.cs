using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Unity.Services.Authentication.PlayerAccounts.Samples
{
    public class Scene2AuthUI : MonoBehaviour
    {
        [Header("Status UI")]
        [SerializeField] TMP_Text   m_StatusText;

        [Header("Buttons (must be active in hierarchy by default)")]
        [SerializeField] GameObject m_SignOutBtn;   // visible for both guest and signed-in
        [SerializeField] GameObject m_SignInBtn;    // visible for guest only

        [Header("Optional")]
        [SerializeField] TMP_Text   m_WaitingText;  // "Waiting for sign-in…" shown during portal

        [Header("Scene Changer")]
        [Tooltip("Drag the GameObject that has SceneChanger on it here.")]
        [SerializeField] SceneChanger m_SceneChanger;
        [Tooltip("Name of Scene 1 as it appears in Build Settings.")]
        [SerializeField] string m_Scene1Name = "Scene1";

        [Header("Optional: cloud-load systems")]
        [SerializeField] OverallStepCounter         m_StepCounter;
        [SerializeField] PlayerPrefsCloudSyncButton m_SyncButton;
        [SerializeField] PlayerData                 m_PlayerData;

        private bool _waitingForPortalReturn = false;
        private bool _cloudLoadTriggeredForSignedInSession = false;

        // ─────────────────────────────────────────────────────────────────────────

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
            // App regains focus after player returns from the Unity Account portal
            if (hasFocus && _waitingForPortalReturn)
                _ = AuthManager.Instance.StartUnitySignInAsync();
        }

        private void Start()
        {
            SetWaitingText(false);

            // Ensure buttons have safe default state before auth state is known
            // (prevents showing unauthenticated buttons on first frame if scene loads faster than auth)
            if (m_SignOutBtn != null) m_SignOutBtn.SetActive(false);
            if (m_SignInBtn != null) m_SignInBtn.SetActive(false);
            Debug.Log("[Scene2AuthUI] Buttons hidden during init.");

            // Clean re-subscribe after all Awakes have run (guards against OnEnable
            // firing before AuthManager.Awake sets Instance)
            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnStateChanged -= OnAuthStateChanged;
                AuthManager.Instance.OnStateChanged += OnAuthStateChanged;
            }

            // Also register this UI as the external target on PlayerAccountsDemo
            // so both UIs mirror each other (matches SetExternalUiTargets pattern)
            var demo = FindObjectOfType<PlayerAccountsDemo>();
            if (demo != null)
                demo.SetExternalUiTargets(m_StatusText, m_SignOutBtn, m_SignInBtn);

            // Wait for AuthManager.IsReady before drawing — avoids the race condition
            // where RefreshUI() reads state before InitAsync() has finished
            StartCoroutine(WaitForAuthThenRefresh());
        }

        // ── Wait for AuthManager ──────────────────────────────────────────────────

        private IEnumerator WaitForAuthThenRefresh()
        {
            const float kTimeout = 10f;
            float elapsed = 0f;

            while (elapsed < kTimeout)
            {
                if (AuthManager.Instance != null && AuthManager.Instance.IsReady)
                    break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (elapsed >= kTimeout)
            {
                Debug.LogWarning("[Scene2AuthUI] Timed out waiting for AuthManager — forcing refresh.");
                OnAuthStateChanged();
                yield break;
            }

            // Apply the current auth state explicitly once ready.
            // This avoids missing cloud-load trigger if the initial auth event fired
            // before this component finished subscribing.
            Debug.Log($"[Scene2AuthUI] AuthManager ready. CurrentMode={AuthManager.Instance.CurrentMode}, " +
                      $"IsSignedIn={AuthManager.Instance.IsSignedIn}, " +
                      $"AuthService.IsSignedIn={Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn}");
            OnAuthStateChanged();
        }

        // ── Auth state → UI ───────────────────────────────────────────────────────

        private void OnAuthStateChanged()
        {
            RefreshUI();

            if (AuthManager.Instance == null || !AuthManager.Instance.IsSignedIn)
            {
                _cloudLoadTriggeredForSignedInSession = false;
                return;
            }

            // Whenever we land on a signed-in state, trigger cloud loads
            if (!_cloudLoadTriggeredForSignedInSession)
            {
                _cloudLoadTriggeredForSignedInSession = true;

                if (_waitingForPortalReturn)
                {
                    _waitingForPortalReturn = false;
                    SetWaitingText(false);
                }
                _ = TriggerCloudLoadsAsync();
            }
        }

        private void RefreshUI()
        {
            if (AuthManager.Instance == null) return;

            bool isGuest    = AuthManager.Instance.IsGuest;
            bool isSignedIn = AuthManager.Instance.IsSignedIn;
            bool hasSession = isGuest || isSignedIn;

            Debug.Log($"[Scene2AuthUI] RefreshUI — isGuest={isGuest}, isSignedIn={isSignedIn}, " +
                      $"hasSession={hasSession}, _waitingForPortalReturn={_waitingForPortalReturn}");

            // ── Status text — mirrors PlayerAccountsDemo.UpdateUI() logic ─────────
            if (m_StatusText != null)
            {
                var sb = new StringBuilder();

                if (isSignedIn)
                {
                    sb.AppendLine("Signed in");
                    sb.AppendLine($"ExternalIds: <b>{AuthManager.Instance.ExternalIds}</b>");
                }
                else if (isGuest)
                {
                    sb.AppendLine($"Playing as: <b>{AuthManager.Instance.GuestName}</b>");
                    sb.AppendLine("Sign in to save your progress.");
                }
                else
                {
                    sb.AppendLine("Not signed in");
                }

                m_StatusText.text = sb.ToString();
            }

            // ── Button visibility — mirrors PlayerAccountsDemo.ApplyUiState() ─────
            // Sign-out: visible whenever any session exists (guest OR signed-in)
            if (m_SignOutBtn != null)
            {
                bool signOutActive = hasSession;
                m_SignOutBtn.SetActive(signOutActive);
                Debug.Log($"[Scene2AuthUI] Sign-out button: {(signOutActive ? "ACTIVE" : "INACTIVE")}");
            }

            // Sign-in: visible whenever not signed in (guest OR none)
            if (m_SignInBtn != null)
            {
                bool signInActive = !isSignedIn && !_waitingForPortalReturn;
                m_SignInBtn.SetActive(signInActive);
                Debug.Log($"[Scene2AuthUI] Sign-in button: {(signInActive ? "ACTIVE" : "INACTIVE")}");
            }
        }

        // ── Button callbacks — wire these up in the Inspector ────────────────────

        /// <summary>Attach to the Sign In button's OnClick.</summary>
        public void OnSignInButtonClicked()
        {
            if (AuthManager.Instance == null) return;
            AuthManager.Instance.OpenAccountPortal();
            _waitingForPortalReturn = true;
            SetWaitingText(true);
            // Hide sign-in button while we wait
            if (m_SignInBtn != null) m_SignInBtn.SetActive(false);
        }

        /// <summary>Attach to the Sign Out button's OnClick.</summary>
        public void OnSignOutButtonClicked()
        {
            if (AuthManager.Instance == null) return;
            AuthManager.Instance.SignOut();
            LogOutManager logoutManager = FindObjectOfType<LogOutManager>();
            if (logoutManager != null)
            {
                logoutManager.LogoutAndRestart();
            }
        }

        // ── Cloud loads ───────────────────────────────────────────────────────────

        private async System.Threading.Tasks.Task TriggerCloudLoadsAsync()
        {
            ResolveCloudLoadTargets();

            if (m_StepCounter != null && !m_StepCounter.cloudLoaded)
            {
                Debug.Log("[Scene2AuthUI] Triggering step data cloud load.");
                await m_StepCounter.LoadStepDataFromCloud();
            }

            if (m_SyncButton != null)
            {
                Debug.Log("[Scene2AuthUI] Triggering non-step cloud load.");
                await m_SyncButton.LoadFromCloudAsync();
            }
            else if (m_PlayerData != null)
            {
                Debug.Log("[Scene2AuthUI] Triggering player data cloud load.");
                await m_PlayerData.LoadPlayerDataFromCloud();
            }

            if (AuthManager.Instance != null && AuthManager.Instance.IsSignedIn &&
                (m_StepCounter == null || m_StepCounter.cloudLoaded))
            {
                PlayerPrefs.SetInt("PlayerSignedIn", 1);
                PlayerPrefs.Save();
            }
        }

        private void ResolveCloudLoadTargets()
        {
            // Mirror PlayerAccountsDemo's FindObjectOfType pattern so auth/data loading
            // still works even if one inspector field is left unassigned.
            if (m_StepCounter == null)
                m_StepCounter = FindObjectOfType<OverallStepCounter>();

            if (m_SyncButton == null)
                m_SyncButton = FindObjectOfType<PlayerPrefsCloudSyncButton>();

            if (m_PlayerData == null)
                m_PlayerData = FindObjectOfType<PlayerData>();

            if (m_StepCounter == null)
                Debug.LogWarning("[Scene2AuthUI] OverallStepCounter not found — step cloud load skipped.");
            if (m_SyncButton == null)
                Debug.LogWarning("[Scene2AuthUI] PlayerPrefsCloudSyncButton not found — non-step cloud load skipped.");
            if (m_PlayerData == null)
                Debug.LogWarning("[Scene2AuthUI] PlayerData not found — player data cloud load skipped.");
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
            if (m_WaitingText != null)
            {
                m_WaitingText.gameObject.SetActive(visible);
                if (visible) m_WaitingText.text = "Waiting for sign-in…";
            }
        }
    }
}