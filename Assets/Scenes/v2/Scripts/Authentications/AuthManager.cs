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
    /// On Scene 2 load, reads the PlayerPrefs handoff from Scene1LoginUI:
    ///   "PendingLoginMode" = "Guest"  → restore guest name, no network call
    ///   "PendingLoginMode" = "Unity"  → AuthenticationService is already signed in
    ///                                    (Scene1LoginUI completed sign-in before loading)
    ///
    /// For session persistence across app restarts:
    ///   Unity Authentication automatically saves a session token to the device.
    ///   Scene1LoginUI restores via SignInAnonymouslyAsync() when a cached token exists.
    ///   By the time Scene 2 loads, AuthenticationService.IsSignedIn is already true.
    ///   AuthManager just reads that state — no extra sign-in calls needed here.
    ///
    /// Guest → Unity account upgrade (from Scene 2):
    ///   Guest taps Sign In → opens PlayerAccount portal → app regains focus
    ///   → StartUnitySignInAsync() exchanges the new access token with AuthenticationService
    ///   → session token is written to disk → next relaunch restores silently
    /// </summary>
    public class AuthManager : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────────
        public static AuthManager Instance { get; private set; }

        // ── State ────────────────────────────────────────────────────────────────
        public enum LoginMode { None, Guest, UnityAccount }

        public LoginMode CurrentMode { get; private set; } = LoginMode.None;

        public bool IsGuest    => CurrentMode == LoginMode.Guest;
        public bool IsSignedIn => CurrentMode == LoginMode.UnityAccount
                                  && AuthenticationService.Instance.IsSignedIn;

        /// <summary>True once InitAsync fully finishes. Scene2AuthUI waits on this.</summary>
        public bool IsReady { get; private set; } = false;

        public string GuestName   { get; private set; } = "";
        public string ExternalIds { get; private set; } = "";
        public string PlayerId    => AuthenticationService.Instance.IsSignedIn
                                     ? AuthenticationService.Instance.PlayerId : "";

        // ── Events ───────────────────────────────────────────────────────────────
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

            // ── Unity Auth events ─────────────────────────────────────────────────
            AuthenticationService.Instance.SignedIn += OnAuthSignedIn;
            AuthenticationService.Instance.SignedOut += OnAuthSignedOut;
            AuthenticationService.Instance.SignInFailed += OnAuthSignInFailed;
            AuthenticationService.Instance.Expired += OnAuthExpired;
            PlayerAccountService.Instance.SignedIn += OnPlayerAccountSignedIn;

            // ── Read Scene 1's handoff ────────────────────────────────────────────
            string pendingMode = PlayerPrefs.GetString(PrefLoginMode, "");

            if (pendingMode == "Guest")
            {
                // No network call needed — just restore the name
                GuestName   = PlayerPrefs.GetString(PrefGuestName, "Guest");
                CurrentMode = LoginMode.Guest;
                PlayerPrefs.DeleteKey(PrefLoginMode);
                Debug.Log($"[AuthManager] Guest session: {GuestName}");
            }
            else if (pendingMode == "Unity")
            {
                PlayerPrefs.DeleteKey(PrefLoginMode);

                // Scene1LoginUI already completed sign-in (SignInWithUnityAsync or
                // cached-token restore) before loading this scene.
                // AuthenticationService.IsSignedIn should already be true.
                if (AuthenticationService.Instance.IsSignedIn)
                {
                    CurrentMode = LoginMode.UnityAccount;
                    ExternalIds = BuildExternalIds(AuthenticationService.Instance.PlayerInfo);
                    Debug.Log($"[AuthManager] Unity account session active. PlayerID: {PlayerId}");
                }
                else
                {
                    // Edge case: Scene loaded before auth finished (very slow device).
                    // Wait briefly for the SignedIn event to fire on its own.
                    Debug.LogWarning("[AuthManager] Unity pending but not signed in yet — waiting for event.");
                }
            }
            else
            {
                // No handoff key — could be app relaunch or direct Scene 2 launch.
                if (AuthenticationService.Instance.IsSignedIn)
                {
                    CurrentMode = LoginMode.UnityAccount;
                    ExternalIds = BuildExternalIds(AuthenticationService.Instance.PlayerInfo);
                    Debug.Log("[AuthManager] Session found without handoff key (app relaunch).");
                }
                else if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 0 &&
                         AuthenticationService.Instance.SessionTokenExists)
                {
                    try
                    {
                        Debug.Log("[AuthManager] Session token found without handoff; attempting silent restore...");
                        await AuthenticationService.Instance.SignInAnonymouslyAsync();

                        if (AuthenticationService.Instance.IsSignedIn)
                        {
                            CurrentMode = LoginMode.UnityAccount;
                            ExternalIds = BuildExternalIds(AuthenticationService.Instance.PlayerInfo);
                            Debug.Log("[AuthManager] Silent restore succeeded in Scene 2.");
                        }
                    }
                    catch (RequestFailedException ex)
                    {
                        Debug.LogWarning($"[AuthManager] Silent restore failed in Scene 2: {ex.Message}");
                    }
                }
                else if (PlayerPrefs.HasKey(PrefGuestName))
                {
                    GuestName   = PlayerPrefs.GetString(PrefGuestName, "Guest");
                    CurrentMode = LoginMode.Guest;
                    Debug.Log($"[AuthManager] Restored guest session without handoff: {GuestName}");
                }
                else
                {
                    Debug.Log("[AuthManager] No session found.");
                }
            }

            // Signal ready BEFORE firing so Scene2AuthUI is subscribed when event arrives
            IsReady = true;
            Debug.Log("[AuthManager] Ready.");
            NotifyStateChanged();
        }

        // ── Auth event handlers (extracted to allow explicit invocation) ─────────

        private void OnAuthSignedIn()
        {
            CurrentMode = LoginMode.UnityAccount;
            ExternalIds = BuildExternalIds(AuthenticationService.Instance.PlayerInfo);
            Debug.Log($"[AuthManager] SignedIn — PlayerID: {PlayerId}");
            NotifyStateChanged();
        }

        private void OnAuthSignedOut()
        {
            Debug.Log("[AuthManager] Signed out.");
            if (CurrentMode == LoginMode.UnityAccount) CurrentMode = LoginMode.None;
            ExternalIds = "";
            NotifyStateChanged();
        }

        private void OnAuthSignInFailed(RequestFailedException err)
        {
            Debug.LogError($"[AuthManager] Sign-in failed: {err}");
            NotifyStateChanged();
        }

        private void OnAuthExpired()
        {
            Debug.LogWarning("[AuthManager] Session expired.");
            CurrentMode = LoginMode.None;
            ExternalIds = "";
            NotifyStateChanged();
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Called when a guest returns from the Unity Account portal in Scene 2.
        /// Exchanges the PlayerAccount access token for a Unity Auth session,
        /// which also saves the session token to disk for future silent restores.
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
                {
                    string token = PlayerAccountService.Instance.AccessToken;
                    if (!string.IsNullOrEmpty(token))
                    {
                        await AuthenticationService.Instance.SignInWithUnityAsync(token);
                        // Session token now saved to disk — next launch restores silently
                        GuestName = "";
                        PlayerPrefs.DeleteKey(PrefGuestName);
                        PlayerPrefs.Save();
                    }
                }
            }
            catch (RequestFailedException ex) { Debug.LogException(ex); }
            finally { _isSigningIn = false; NotifyStateChanged(); }
        }

        /// <summary>
        /// Explicit sign-out — clears session token from disk.
        /// Next launch will show Scene 1 login screen.
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

            // Clear cached credentials so Scene1 cannot silently restore a prior session.
            if (AuthenticationService.Instance.IsSignedIn)
                AuthenticationService.Instance.SignOut(true);
            if (PlayerAccountService.Instance.IsSignedIn)
                PlayerAccountService.Instance.SignOut();

            Debug.Log("[AuthManager] Signed out — session token cleared.");
            NotifyStateChanged();
        }

        public void OpenAccountPortal()
            => Application.OpenURL(PlayerAccountService.Instance.AccountPortalUrl);

        public void SetGuestName(string guestName)
        {
            GuestName = guestName;

            if (string.IsNullOrWhiteSpace(guestName))
                PlayerPrefs.DeleteKey(PrefGuestName);
            else
                PlayerPrefs.SetString(PrefGuestName, guestName);

            PlayerPrefs.Save();
            NotifyStateChanged();
        }

        // ── Internal helpers ─────────────────────────────────────────────────────

        private void OnPlayerAccountSignedIn()
        {
            // Fires if PlayerAccountService resumes on its own (e.g. portal auto-return)
            if (!_isSigningIn && !AuthenticationService.Instance.IsSignedIn)
                _ = SignInWithUnityInternalAsync();
        }

        private async Task SignInWithUnityInternalAsync()
        {
            _isSigningIn = true;
            try
            {
                string token = PlayerAccountService.Instance.AccessToken;
                if (!string.IsNullOrEmpty(token))
                    await AuthenticationService.Instance.SignInWithUnityAsync(token);
            }
            catch (RequestFailedException ex) { Debug.LogException(ex); }
            finally { _isSigningIn = false; NotifyStateChanged(); }
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