using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Unity.Services.Authentication.PlayerAccounts.Samples
{
    /// <summary>
    /// Scene 2 auth UI — mirrors the PlayerAccountsDemo pattern.
    ///
    /// Works in two modes depending on how the player arrived:
    ///
    ///   GUEST MODE  (CurrentMode == Guest)
    ///     • Shows guest name in status text
    ///     • Shows  [Sign In]  button — opens Unity Account portal so they can save progress
    ///     • Shows  [Sign Out] button — returns to Scene 1
    ///     • After successful portal sign-in, promotes to SIGNED-IN MODE automatically
    ///
    ///   SIGNED-IN MODE  (CurrentMode == UnityAccount)
    ///     • Shows player ID + external IDs in status text
    ///     • Hides  [Sign In]  button
    ///     • Shows  [Sign Out] button
    ///     • Triggers cloud data loads (steps, prefs, player data)
    ///
    /// This script also calls PlayerAccountsDemo.SetExternalUiTargets() if that
    /// component is present in the scene, so both UIs stay in sync exactly like
    /// the original PlayerAccountsDemo dual-UI pattern.
    ///
    /// Required UI (all must be active in hierarchy by default — script controls visibility):
    ///   m_StatusText      TMP_Text    — shows current auth state
    ///   m_SignOutBtn      GameObject  — visible in both guest and signed-in modes
    ///   m_SignInBtn       GameObject  — visible in guest mode only; hidden once signed in
    ///   m_WaitingText     TMP_Text    — shown while waiting for portal to return (optional)
    /// </summary>
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
                RefreshUI();
            }
            // Normal path: AuthManager fires NotifyStateChanged() immediately after
            // IsReady = true, which calls OnAuthStateChanged → RefreshUI for us.
        }

        // ── Auth state → UI ───────────────────────────────────────────────────────

        private void OnAuthStateChanged()
        {
            RefreshUI();

            // Whenever we land on a signed-in state, trigger cloud loads
            if (AuthManager.Instance != null && AuthManager.Instance.IsSignedIn)
            {
                if (_waitingForPortalReturn)
                {
                    _waitingForPortalReturn = false;
                    SetWaitingText(false);
                }
                TriggerCloudLoads();
            }
        }

        private void RefreshUI()
        {
            if (AuthManager.Instance == null) return;

            bool isGuest    = AuthManager.Instance.IsGuest;
            bool isSignedIn = AuthManager.Instance.IsSignedIn;
            bool hasSession = isGuest || isSignedIn;

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
                m_SignOutBtn.SetActive(hasSession);

            // Sign-in: visible in guest mode only; hidden once they have an account
            if (m_SignInBtn != null)
                m_SignInBtn.SetActive(isGuest && !_waitingForPortalReturn);
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

        private void TriggerCloudLoads()
        {
            PlayerPrefs.SetInt("PlayerSignedIn", 1);
            PlayerPrefs.Save();

            if (m_StepCounter != null && !m_StepCounter.cloudLoaded)
            {
                Debug.Log("[Scene2AuthUI] Triggering step data cloud load.");
                _ = m_StepCounter.LoadStepDataFromCloud();
            }

            if (m_SyncButton != null)
            {
                Debug.Log("[Scene2AuthUI] Triggering non-step cloud load.");
                m_SyncButton.LoadFromCloud();
            }

            if (m_PlayerData != null)
            {
                Debug.Log("[Scene2AuthUI] Triggering player data cloud load.");
                m_PlayerData.LoadPlayerDataFromCloud();
            }
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