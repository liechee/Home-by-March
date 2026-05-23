using System;
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
    /// Scene 1 login screen.
    ///
    /// Responsibilities:
    ///   - Guest flow: validate name → write PrefGuestName + PrefLoginMode="Guest" → load Scene 2.
    ///   - Unity Account flow: open portal → exchange token → write PrefLoginMode="Unity" → load Scene 2.
    ///   - Auto-resume: on launch, restore a prior session and skip straight to Scene 2.
    ///
    /// This script does NOT persist across scenes. AuthManager1 (DontDestroyOnLoad) owns
    /// auth state after Scene 2 loads.
    /// </summary>
    public class Scene1LoginUI : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("UI References")]
        [SerializeField] TMP_InputField m_GuestNameInput;
        [SerializeField] NameChangePanel m_NameChangePanel;
        [SerializeField] Button     m_PlayButton;
        [SerializeField] Button     m_PlayAsGuestBtn;
        [SerializeField] Button     m_LogInBtn;
        [SerializeField] TMP_Text   m_StatusText;
       // [SerializeField] GameObject m_SignInPanel;
        [SerializeField] GameObject m_LoginButtonsPanel;

        [Header("Navigation")]
        [SerializeField] SceneChanger m_SceneChanger;
        [SerializeField] string m_Scene2Name = "Main Screen";

        // ── Private state ─────────────────────────────────────────────────────────

        private bool _servicesReady;
        private bool _waitingForPlayerAccountReturn;
        private bool _signInCompletionHandled;
        private bool _isCompletingSignIn;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private async void Start()
        {
            SetButtonsInteractable(false);
            SetStatus("Loading…");

            m_PlayButton?.onClick.AddListener(OnPlayClicked);
            m_PlayAsGuestBtn?.onClick.AddListener(OnPlayAsGuestClicked);
            m_LogInBtn?.onClick.AddListener(OnSignInClicked);

            await UnityServices.InitializeAsync();
            _servicesReady = true;
            PlayerAccountService.Instance.SignedIn += OnPlayerAccountSignedIn;

            // Already signed in this process lifetime (e.g. came back from Scene 2).
            if (AuthenticationService.Instance.IsSignedIn)
            {
                m_LoginButtonsPanel?.SetActive(false);
                SetButtonsInteractable(true);
                SetStatus("");
                return;
            }

            bool resumed = await TryAutoResumeAsync();
            if (resumed)
            {
                GoToScene2();   // auto-resume succeeded — skip the login UI entirely
                return;
            }

            SetButtonsInteractable(true);
            SetStatus("");
        }

        private void OnDestroy()
        {
            if (_servicesReady && PlayerAccountService.Instance != null)
                PlayerAccountService.Instance.SignedIn -= OnPlayerAccountSignedIn;
        }

        // ── Auto-resume ───────────────────────────────────────────────────────────

        /// <summary>
        /// Attempts to restore a prior session silently.
        /// Returns true if the caller should navigate straight to Scene 2.
        /// </summary>
        private async Task<bool> TryAutoResumeAsync()
        {
            // Respect an explicit logout — never auto-restore until the player
            // actively signs in or plays as a guest again.
            if (PlayerPrefs.GetInt(AuthManager1.PrefHasLoggedOut, 0) == 1)
            {
                Debug.Log("[Scene1LoginUI] Explicit logout flag set — skipping auto-resume.");
                return false;
            }

            // ── Silent Unity session restore ──────────────────────────────────────
            if (AuthenticationService.Instance.SessionTokenExists)
            {
                SetStatus("Restoring session…");
                try
                {
                    // SignInAnonymouslyAsync re-uses the on-disk session token when one
                    // exists; it does NOT create a new anonymous account in that case.
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                    if (AuthenticationService.Instance.IsSignedIn)
                    {
                        PlayerPrefs.SetString(AuthManager1.PrefLoginMode, "Unity");
                        PlayerPrefs.DeleteKey(AuthManager1.PrefHasLoggedOut);
                        PlayerPrefs.Save();
                        // m_SignInPanel?.SetActive(false);
                        m_LoginButtonsPanel?.SetActive(false);
                        Debug.Log("[Scene1LoginUI] Silent Unity restore succeeded.");
                        return true;
                    }
                }
                catch (RequestFailedException ex)
                {
                    Debug.LogWarning($"[Scene1LoginUI] Silent restore failed: {ex.Message}");
                }
            }

            // ── Guest session restore ─────────────────────────────────────────────
            string savedGuest = PlayerPrefs.GetString(AuthManager1.PrefGuestName, "").Trim();
            if (!string.IsNullOrEmpty(savedGuest))
            {
                PlayerPrefs.SetString(AuthManager1.PrefLoginMode, "Guest");
                PlayerPrefs.Save();
                // m_SignInPanel?.SetActive(false);
                Debug.Log($"[Scene1LoginUI] Guest '{savedGuest}' restored.");
                return true;
            }

            return false;
        }

        // ── Button handlers ───────────────────────────────────────────────────────

        private void OnPlayClicked()
        {
            if (HasExistingSession())
            {
                GoToScene2();
                return;
            }
            // m_SignInPanel?.SetActive(true);
            SetStatus("");
        }

        private void OnPlayAsGuestClicked()
        {
            // If a guest name is already saved, resume that session.
            string existingGuest = PlayerPrefs.GetString(AuthManager1.PrefGuestName, "").Trim();
            if (!string.IsNullOrEmpty(existingGuest))
            {
                PlayerPrefs.SetString(AuthManager1.PrefLoginMode, "Guest");
                PlayerPrefs.DeleteKey(AuthManager1.PrefHasLoggedOut);
                PlayerPrefs.Save();
                GoToScene2();
                return;
            }

            // New guest: validate and save.
            if (m_NameChangePanel != null)
            {
                if (!m_NameChangePanel.TryPrepareGuestFromInput(m_GuestNameInput, out string msg))
                {
                    SetStatus(msg);
                    return;
                }
                // TryPrepareGuestFromInput is expected to write PrefGuestName + PrefLoginMode.
            }
            else
            {
                string name = m_GuestNameInput != null ? m_GuestNameInput.text.Trim() : "";
                if (string.IsNullOrEmpty(name))
                {
                    SetStatus("Please enter a name to continue.");
                    return;
                }
                PlayerPrefs.SetString(AuthManager1.PrefLoginMode, "Guest");
                PlayerPrefs.SetString(AuthManager1.PrefGuestName, name);
                PlayerPrefs.DeleteKey(AuthManager1.PrefHasLoggedOut);
                PlayerPrefs.Save();
            }

            GoToScene2();
        }

        private async void OnSignInClicked()
        {
            if (!_servicesReady) return;

            SetStatus("Opening sign-in…");
            SetButtonsInteractable(false);
            _waitingForPlayerAccountReturn = true;
            _signInCompletionHandled       = false;
            _isCompletingSignIn            = false;

            try
            {
                // Wait for AuthManager1 to finish any concurrent init/restore so we
                // don't clash with its TrySilentRestoreAsync.
                const float initTimeout = 5f;
                float waited = 0f;
                while (AuthManager1.Instance != null &&
                       (!AuthManager1.Instance.IsReady || AuthManager1.Instance.IsSigningIn) &&
                       waited < initTimeout)
                {
                    await Task.Yield();
                    waited += Time.unscaledDeltaTime;
                }

                // Clear any stale credentials so the portal opens fresh.
                if (PlayerAccountService.Instance.IsSignedIn)
                    PlayerAccountService.Instance.SignOut();

                if (AuthenticationService.Instance.IsSignedIn ||
                    AuthenticationService.Instance.SessionTokenExists)
                    AuthenticationService.Instance.SignOut(clearCredentials: true);

                await Task.Delay(200);  // give the SDK a frame to finish clearing

                await PlayerAccountService.Instance.StartSignInAsync();

                // If the portal didn't return synchronously, prompt the player to
                // come back to the app after completing sign-in in the browser.
                if (!PlayerAccountService.Instance.IsSignedIn)
                    SetStatus("Complete sign-in in the browser, then return here.");
            }
            catch (RequestFailedException ex)
            {
                Debug.LogException(ex);
                SetStatus("Sign-in failed. Try again.");
                ResetSignInState();
                SetButtonsInteractable(true);
            }
        }

        private async void OnPlayerAccountSignedIn()
        {
            if (!_waitingForPlayerAccountReturn || _signInCompletionHandled) return;
            await CompleteUnitySignInAsync();
        }

        private async Task CompleteUnitySignInAsync()
        {
            if (_signInCompletionHandled || _isCompletingSignIn) return;
            _signInCompletionHandled = true;
            _isCompletingSignIn      = true;

            try
            {
                string token = PlayerAccountService.Instance.AccessToken;
                if (string.IsNullOrEmpty(token))
                {
                    Debug.LogWarning("[Scene1LoginUI] Access token is empty after portal return.");
                    SetStatus("Sign-in failed. Try again.");
                    ResetSignInState();
                    SetButtonsInteractable(true);
                    return;
                }

                // ── Concurrency guard ─────────────────────────────────────────────
                // AuthManager1 may have spawned a TrySilentRestoreAsync on the same
                // frame (e.g. it was destroyed and recreated by LogOutManager and its
                // new Awake saw a leftover session token). We wait for it to finish,
                // then claim the lock ourselves so it won't start a new one.
                const float lockTimeout = 5f;
                float waited = 0f;
                while (AuthManager1.Instance != null &&
                       AuthManager1.Instance.IsSigningIn &&
                       waited < lockTimeout)
                {
                    await Task.Yield();
                    waited += Time.unscaledDeltaTime;
                }

                // If AuthManager1's silent restore already signed us in, we're done.
                if (AuthenticationService.Instance.IsSignedIn)
                {
                    Debug.Log("[Scene1LoginUI] AuthManager1 already signed in during wait — skipping token exchange.");
                    PlayerPrefs.SetString(AuthManager1.PrefLoginMode, "Unity");
                    PlayerPrefs.DeleteKey(AuthManager1.PrefHasLoggedOut);
                    PlayerPrefs.Save();
                    ResetSignInState();
                    GoToScene2();
                    return;
                }

                // Claim the global sign-in slot so AuthManager1 won't race us.
                if (AuthManager1.Instance != null)
                    AuthManager1.Instance.IsSigningIn = true;

                // Clear any stale session before signing in with the fresh token.
                if (AuthenticationService.Instance.IsSignedIn ||
                    AuthenticationService.Instance.SessionTokenExists)
                {
                    AuthenticationService.Instance.SignOut(clearCredentials: true);
                    await Task.Yield();
                }

                // Attempt sign-in — if another sign-in is already in flight
                // AuthenticationService will throw. Detect that case, wait for
                // the concurrent sign-in to finish, and only retry once.
                bool signInSucceeded = false;
                int attempts = 0;
                const int maxAttempts = 2;
                while (!signInSucceeded && attempts < maxAttempts)
                {
                    attempts++;
                    try
                    {
                        await AuthenticationService.Instance.SignInWithUnityAsync(token);
                        signInSucceeded = true;
                    }
                    catch (RequestFailedException ex)
                    {
                        if (ex.Message != null && ex.Message.IndexOf("already signing in", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            float waited2 = 0f;
                            const float waitTimeout2 = 5f;
                            while (!AuthenticationService.Instance.IsSignedIn && waited2 < waitTimeout2)
                            {
                                await Task.Yield();
                                waited2 += Time.unscaledDeltaTime;
                            }
                            if (AuthenticationService.Instance.IsSignedIn)
                            {
                                signInSucceeded = true;
                                break;
                            }
                            // otherwise loop to retry once
                        }
                        else
                        {
                            throw;
                        }
                    }
                }

                if (!signInSucceeded)
                {
                    Debug.LogWarning("[Scene1LoginUI] SignInWithUnityAsync failed after retries.");
                    SetStatus("Sign-in failed. Try again.");
                    ResetSignInState();
                    SetButtonsInteractable(true);
                    return;
                }

                PlayerPrefs.SetString(AuthManager1.PrefLoginMode, "Unity");
                PlayerPrefs.DeleteKey(AuthManager1.PrefHasLoggedOut);
                PlayerPrefs.Save();

                ResetSignInState();
                GoToScene2();
            }
            catch (RequestFailedException ex)
            {
                Debug.LogException(ex);
                SetStatus("Sign-in failed. Try again.");
                ResetSignInState();
                SetButtonsInteractable(true);
            }
            finally
            {
                // Always release the global lock, even on failure.
                if (AuthManager1.Instance != null)
                    AuthManager1.Instance.IsSigningIn = false;
            }
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

        /// <summary>Returns true if a resumable session exists (Unity token or saved guest).</summary>
        private bool HasExistingSession()
        {
            if (PlayerPrefs.GetInt(AuthManager1.PrefHasLoggedOut, 0) == 1) return false;
            if (AuthenticationService.Instance.IsSignedIn)                 return true;
            if (AuthenticationService.Instance.SessionTokenExists)          return true;

            string guestName = PlayerPrefs.GetString(AuthManager1.PrefGuestName, "").Trim();
            return !string.IsNullOrEmpty(guestName);
        }

        private void ResetSignInState()
        {
            _waitingForPlayerAccountReturn = false;
            _isCompletingSignIn            = false;
        }

        private void SetStatus(string msg)
        {
            if (m_StatusText != null) m_StatusText.text = msg;
        }

        private void SetButtonsInteractable(bool on)
        {
            if (m_PlayButton    != null) m_PlayButton.interactable    = on;
            if (m_PlayAsGuestBtn != null) m_PlayAsGuestBtn.interactable = on;
            if (m_LogInBtn      != null) m_LogInBtn.interactable      = on;
        }
    }
}