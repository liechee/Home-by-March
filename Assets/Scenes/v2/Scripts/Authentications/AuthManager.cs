using System;
using System.Threading.Tasks;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Unity.Services.Authentication.PlayerAccounts.Samples
{
    /// <summary>
    /// Placed in Scene 2 only. No DontDestroyOnLoad.
    ///
    /// Responsibilities:
    ///   • Initialize Unity Services
    ///   • Restore guest name OR complete Unity sign-in from what Scene 1 wrote to PlayerPrefs
    ///   • Expose IsReady so Scene2AuthUI knows when it's safe to draw the UI
    ///   • Fire OnStateChanged whenever auth state changes so all listeners refresh
    ///
    /// PlayerPrefs handoff keys (written by Scene1LoginUI, consumed here):
    ///   "PendingLoginMode" = "Guest" | "Unity"
    ///   "GuestName"        = display name (guest path only)
    /// </summary>
    public class AuthManager : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────────
        public static AuthManager Instance { get; private set; }

        // ── State ────────────────────────────────────────────────────────────────
        public enum LoginMode { None, Guest, UnityAccount }

        public LoginMode CurrentMode { get; private set; } = LoginMode.None;

        /// <summary>Guest: entered with a name but no Unity account.</summary>
        public bool IsGuest    => CurrentMode == LoginMode.Guest;

        /// <summary>Fully signed into a Unity account.</summary>
        public bool IsSignedIn => CurrentMode == LoginMode.UnityAccount
                                  && AuthenticationService.Instance.IsSignedIn;

        /// <summary>True the moment InitAsync finishes — safe to read all state after this.</summary>
        public bool IsReady { get; private set; } = false;

        public string GuestName { get; private set; } = "";
        public string ExternalIds { get; private set; } = "";
        public string PlayerId  => AuthenticationService.Instance.IsSignedIn
                                   ? AuthenticationService.Instance.PlayerId : "";

        // ── Events ───────────────────────────────────────────────────────────────
        /// <summary>
        /// Fired after every auth state change, including the initial load.
        /// Scene2AuthUI subscribes here to refresh its UI.
        /// </summary>
        public event Action OnStateChanged;

        // ── Internal ─────────────────────────────────────────────────────────────
        private bool _isSigningIn   = false;
        private bool _servicesReady = false;

        public const string PrefLoginMode = "PendingLoginMode";
        public const string PrefGuestName = "GuestName";

        // ─────────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _ = InitAsync();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private async Task InitAsync()
        {
            await UnityServices.InitializeAsync();
            _servicesReady = true;

            // ── Unity Auth service events ─────────────────────────────────────────
            AuthenticationService.Instance.SignedIn += () =>
            {
                CurrentMode  = LoginMode.UnityAccount;
                ExternalIds  = BuildExternalIds(AuthenticationService.Instance.PlayerInfo);
                Debug.Log($"[AuthManager] SignedIn — PlayerID: {PlayerId}");
                NotifyStateChanged();
            };

            AuthenticationService.Instance.SignedOut += () =>
            {
                Debug.Log("[AuthManager] Signed out.");
                if (CurrentMode == LoginMode.UnityAccount) CurrentMode = LoginMode.None;
                ExternalIds = "";
                NotifyStateChanged();
            };

            AuthenticationService.Instance.SignInFailed += err =>
            {
                Debug.LogError($"[AuthManager] Sign-in failed: {err}");
                NotifyStateChanged();
            };

            AuthenticationService.Instance.Expired += () =>
            {
                Debug.LogWarning("[AuthManager] Session expired.");
                CurrentMode = LoginMode.None;
                ExternalIds = "";
                NotifyStateChanged();
            };

            PlayerAccountService.Instance.SignedIn += OnPlayerAccountSignedIn;

            // ── Read Scene 1's handoff ────────────────────────────────────────────
            string pendingMode = PlayerPrefs.GetString(PrefLoginMode, "");

            if (pendingMode == "Guest")
            {
                GuestName   = PlayerPrefs.GetString(PrefGuestName, "Guest");
                CurrentMode = LoginMode.Guest;
                PlayerPrefs.DeleteKey(PrefLoginMode);
                Debug.Log($"[AuthManager] Guest session started: {GuestName}");
            }
            else if (pendingMode == "Unity" || AuthenticationService.Instance.IsSignedIn)
            {
                PlayerPrefs.DeleteKey(PrefLoginMode);

                if (AuthenticationService.Instance.IsSignedIn)
                {
                    CurrentMode = LoginMode.UnityAccount;
                    ExternalIds = BuildExternalIds(AuthenticationService.Instance.PlayerInfo);
                    Debug.Log("[AuthManager] Unity session already active.");
                }
                else
                {
                    await CompleteUnitySignInAsync();
                }
            }
            else
            {
                Debug.Log("[AuthManager] No pending login — idle.");
            }

            // IsReady = true BEFORE NotifyStateChanged so Scene2AuthUI's coroutine
            // has already unblocked and re-subscribed when the event fires.
            IsReady = true;
            Debug.Log("[AuthManager] Ready.");
            NotifyStateChanged();
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Starts Unity sign-in from Scene 2 (guest upgrading to a real account).
        /// Opens PlayerAccountService portal, then exchanges token with AuthenticationService.
        /// </summary>
        public async Task StartUnitySignInAsync()
        {
            if (_isSigningIn || !_servicesReady) return;
            _isSigningIn = true;
            try
            {
                if (!PlayerAccountService.Instance.IsSignedIn)
                    await PlayerAccountService.Instance.StartSignInAsync();

                if (PlayerAccountService.Instance.IsSignedIn &&
                    !AuthenticationService.Instance.IsSignedIn)
                    await FinishSignInWithTokenAsync();
            }
            catch (RequestFailedException ex) { Debug.LogException(ex); }
            finally { _isSigningIn = false; NotifyStateChanged(); }
        }

        /// <summary>
        /// Full sign-out — clears guest state, Unity account, and all relevant PlayerPrefs.
        /// Scene2AuthUI calls this then navigates back to Scene 1.
        /// </summary>
        public void SignOut()
        {
            GuestName   = "";
            ExternalIds = "";
            CurrentMode = LoginMode.None;

            PlayerPrefs.DeleteKey(PrefGuestName);
            PlayerPrefs.DeleteKey(PrefLoginMode);
            PlayerPrefs.DeleteKey("PlayerSignedIn");
            PlayerPrefs.Save();
            LogOutManager logoutManager = FindObjectOfType<LogOutManager>();

            if (AuthenticationService.Instance.IsSignedIn)
                AuthenticationService.Instance.SignOut();
            if (PlayerAccountService.Instance.IsSignedIn)
                PlayerAccountService.Instance.SignOut();
            
            if (logoutManager != null)
            {
                logoutManager.LogoutAndRestart();
            }

            Debug.Log("[AuthManager] Full sign-out.");
            NotifyStateChanged();
        }

        public void OpenAccountPortal()
            => Application.OpenURL(PlayerAccountService.Instance.AccountPortalUrl);

        // ── Internal helpers ─────────────────────────────────────────────────────

        private void OnPlayerAccountSignedIn()
        {
            // Fires when the portal returns and PlayerAccountService auto-resumes
            if (!_isSigningIn && !AuthenticationService.Instance.IsSignedIn)
                _ = SignInWithUnityInternalAsync();
        }

        private async Task SignInWithUnityInternalAsync()
        {
            _isSigningIn = true;
            try { await FinishSignInWithTokenAsync(); }
            catch (RequestFailedException ex) { Debug.LogException(ex); }
            finally { _isSigningIn = false; NotifyStateChanged(); }
        }

        private async Task CompleteUnitySignInAsync()
        {
            _isSigningIn = true;
            try
            {
                // First try a silent session-token restore (fastest, no browser needed).
                // Unity stores a token on-device after every successful sign-in.
                if (AuthenticationService.Instance.SessionTokenExists)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Debug.Log("[AuthManager] Session token restored silently.");
                    // SignedIn event fires → CurrentMode set → NotifyStateChanged handled there
                    return;
                }

                // No stored token — go through the full PlayerAccount portal flow
                if (!PlayerAccountService.Instance.IsSignedIn)
                    await PlayerAccountService.Instance.StartSignInAsync();

                if (PlayerAccountService.Instance.IsSignedIn)
                    await FinishSignInWithTokenAsync();
            }
            catch (RequestFailedException ex) { Debug.LogException(ex); }
            finally { _isSigningIn = false; }
        }

        private async Task FinishSignInWithTokenAsync()
        {
            if (AuthenticationService.Instance.IsSignedIn) return;

            string token = PlayerAccountService.Instance.AccessToken;
            if (string.IsNullOrEmpty(token))
            {
                Debug.LogWarning("[AuthManager] No access token.");
                return;
            }

            await AuthenticationService.Instance.SignInWithUnityAsync(token);
            Debug.Log("[AuthManager] Signed in with Unity.");

            // Guest promoted to real account — clear guest name
            GuestName = "";
            PlayerPrefs.DeleteKey(PrefGuestName);
            PlayerPrefs.Save();
        }

        private static string BuildExternalIds(PlayerInfo info)
        {
            if (info?.Identities == null) return "None";
            var sb = new System.Text.StringBuilder();
            foreach (var id in info.Identities)
                sb.Append(" " + id.TypeId);
            return sb.ToString();
        }

        private void NotifyStateChanged() => OnStateChanged?.Invoke();
    }
}