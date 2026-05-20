using System;
using System.Threading.Tasks;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Unity.Services.Authentication.PlayerAccounts.Samples
{
    /// <summary>
    /// Singleton that owns all authentication state and persists across scenes.
    /// Scene UIs subscribe to OnStateChanged and read the public properties.
    ///
    /// Login flow summary:
    ///   Scene1LoginUI  →  writes PrefLoginMode  →  loads Scene 2
    ///   AuthManager.Awake  →  InitAsync  →  RestoreSessionAsync reads PrefLoginMode
    ///   Scene2AuthUI  →  waits for IsReady, then subscribes to OnStateChanged
    /// </summary>
    public class AuthManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────────

        public static AuthManager Instance { get; private set; }

        // ── PlayerPrefs keys (shared across scripts) ──────────────────────────────

        public const string PrefLoginMode   = "PendingLoginMode";   // "Guest" | "Unity"
        public const string PrefGuestName   = "GuestName";
        public const string PrefHasLoggedOut = "HasLoggedOut";       // 1 = explicit logout
        public const string PrefPlayerSignedIn = "PlayerSignedIn";  // 1 = confirmed cloud session

        // ── Login mode ────────────────────────────────────────────────────────────

        public enum LoginMode { None, Guest, UnityAccount }

        public LoginMode CurrentMode { get; private set; } = LoginMode.None;

        // Convenience booleans — always check these, never CurrentMode directly in UI.
        public bool IsGuest     => CurrentMode == LoginMode.Guest;
        public bool IsSignedIn  => CurrentMode == LoginMode.UnityAccount
                                   && AuthenticationService.Instance.IsSignedIn;

        /// <summary>True once InitAsync has fully completed. UIs must wait on this.</summary>
        public bool IsReady { get; private set; }

        public string GuestName   { get; private set; } = "";
        public string ExternalIds { get; private set; } = "";
        public string PlayerId    => AuthenticationService.Instance.IsSignedIn
                                        ? AuthenticationService.Instance.PlayerId : "";

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fired whenever auth state changes. Subscribe in OnEnable, unsub in OnDisable.</summary>
        public event Action OnStateChanged;

        // ── Private fields ────────────────────────────────────────────────────────

        private bool _isSigningIn;
        private bool _servicesReady;

        /// <summary>
        /// True while AuthManager (or any caller) is mid-sign-in.
        /// Scene1LoginUI must set this to true before calling SignInWithUnityAsync
        /// so that AuthManager's concurrent TrySilentRestoreAsync backs off.
        /// </summary>
        public bool IsSigningIn
        {
            get => _isSigningIn;
            set => _isSigningIn = value;
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _ = InitAsync();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Initialisation ────────────────────────────────────────────────────────

        private async Task InitAsync()
        {
            await UnityServices.InitializeAsync();
            _servicesReady = true;

            // Subscribe to service events before any restore attempt.
            AuthenticationService.Instance.SignedIn    += OnAuthSignedIn;
            AuthenticationService.Instance.SignedOut   += OnAuthSignedOut;
            AuthenticationService.Instance.SignInFailed += OnAuthSignInFailed;
            AuthenticationService.Instance.Expired     += OnAuthExpired;
            PlayerAccountService.Instance.SignedIn     += OnPlayerAccountSignedIn;

            await RestoreSessionAsync();

            IsReady = true;
            Debug.Log("[AuthManager] Ready. Mode=" + CurrentMode);
            NotifyStateChanged();
        }

        /// <summary>
        /// Reads the handoff key written by Scene1LoginUI and restores the
        /// matching session. Falls back to silent token restore or guest restore
        /// when no handoff key is present (app relaunch / deep-link to Scene 2).
        /// </summary>
        private async Task RestoreSessionAsync()
        {
            string pendingMode = PlayerPrefs.GetString(PrefLoginMode, "");
            PlayerPrefs.DeleteKey(PrefLoginMode);   // consume immediately — never leave stale

            switch (pendingMode)
            {
                case "Guest":
                    GuestName = PlayerPrefs.GetString(PrefGuestName, "Guest");
                    CurrentMode = LoginMode.Guest;
                    Debug.Log($"[AuthManager] Guest handoff: {GuestName}");
                    return;

                case "Unity":
                    // Scene1 already completed the sign-in; the service should be signed in.
                    if (AuthenticationService.Instance.IsSignedIn)
                    {
                        SetUnityAccountMode();
                        Debug.Log("[AuthManager] Unity handoff: already signed in.");
                        return;
                    }
                    // Slow device edge-case: wait for the SignedIn event instead of polling.
                    Debug.LogWarning("[AuthManager] Unity handoff but not yet signed in — waiting for event.");
                    return;
            }

            // ── No handoff key: app relaunch or direct Scene 2 entry ──────────────

            // Already signed in from a previous session this process lifetime.
            if (AuthenticationService.Instance.IsSignedIn)
            {
                SetUnityAccountMode();
                Debug.Log("[AuthManager] Relaunch: service already signed in.");
                return;
            }

            // Explicit logout flag means the player intentionally signed out — do not auto-restore.
            bool explicitLogout = PlayerPrefs.GetInt(PrefHasLoggedOut, 0) == 1;

            if (!explicitLogout && AuthenticationService.Instance.SessionTokenExists)
            {
                // A saved session token exists — attempt a silent restore using the
                // PlayerAccount access token if available, otherwise anonymous fallback.
                await TrySilentRestoreAsync();
                return;
            }

            // Restore a saved guest session.
            if (PlayerPrefs.HasKey(PrefGuestName))
            {
                GuestName = PlayerPrefs.GetString(PrefGuestName, "Guest");
                CurrentMode = LoginMode.Guest;
                Debug.Log($"[AuthManager] Relaunch: guest session restored: {GuestName}");
                return;
            }

            Debug.Log("[AuthManager] No session to restore.");
            CurrentMode = LoginMode.None;
        }

        /// <summary>
        /// Silently restores a saved Unity session token.
        /// Backs off immediately if another caller (e.g. Scene1LoginUI) has already
        /// set IsSigningIn — two concurrent calls to SignInWithUnityAsync throw
        /// "Invalid state: player is already signing in".
        /// </summary>
        private async Task TrySilentRestoreAsync()
        {
            // If Scene1LoginUI (or anyone else) already owns the sign-in slot, skip.
            // The SignedIn event will fire and OnAuthSignedIn will pick up the result.
            if (_isSigningIn)
            {
                Debug.Log("[AuthManager] Silent restore skipped — sign-in already in flight.");
                return;
            }

            _isSigningIn = true;
            try
            {
                Debug.Log("[AuthManager] Silent restore: attempting...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

                if (AuthenticationService.Instance.IsSignedIn)
                {
                    SetUnityAccountMode();
                    PlayerPrefs.DeleteKey(PrefHasLoggedOut);
                    PlayerPrefs.Save();
                    Debug.Log("[AuthManager] Silent restore succeeded.");
                }
            }
            catch (RequestFailedException ex)
            {
                Debug.LogWarning($"[AuthManager] Silent restore failed: {ex.Message}");
                CurrentMode = LoginMode.None;
            }
            finally
            {
                _isSigningIn = false;
            }
        }

        // ── Auth service event handlers ───────────────────────────────────────────

        private void OnAuthSignedIn()
        {
            SetUnityAccountMode();
            Debug.Log($"[AuthManager] SignedIn — PlayerID: {PlayerId}");
            NotifyStateChanged();
        }

        private void OnAuthSignedOut()
        {
            Debug.Log("[AuthManager] AuthService: signed out.");
            // Only demote if we were in Unity mode; a LogOut wipe sets mode itself.
            if (CurrentMode == LoginMode.UnityAccount)
                CurrentMode = LoginMode.None;
            ExternalIds = "";
            NotifyStateChanged();
        }

        private void OnAuthSignInFailed(RequestFailedException err)
        {
            // Ignore the transient race where another caller is already signing in.
            if (err.Message != null && err.Message.IndexOf("already signing in", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Debug.LogWarning($"[AuthManager] Ignored sign-in race: {err.Message}");
                return;
            }

            Debug.LogError($"[AuthManager] Sign-in failed: {err}");
            CurrentMode = LoginMode.None;
            NotifyStateChanged();
        }

        private void OnAuthExpired()
        {
            Debug.LogWarning("[AuthManager] Session expired.");
            CurrentMode = LoginMode.None;
            ExternalIds = "";
            NotifyStateChanged();
        }

        private void OnPlayerAccountSignedIn()
        {
            // Fires when PlayerAccountService auto-returns from the portal.
            // Guard: don't start a second sign-in if one is already in flight.
            if (!_isSigningIn && !AuthenticationService.Instance.IsSignedIn)
                _ = SignInWithUnityInternalAsync();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Opens the Unity Account portal, then exchanges the access token for a
        /// Unity Auth session when the player returns to the app.
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
                    await ExchangeTokenAsync();
                }
            }
            catch (RequestFailedException ex) { Debug.LogException(ex); }
            finally { _isSigningIn = false; NotifyStateChanged(); }
        }

        /// <summary>
        /// Lightweight in-memory sign-out used by AuthManager internally.
        /// For a full local data wipe + scene reload, call LogOutManager.LogoutAndRestart().
        /// </summary>
        public void SignOut()
        {
            GuestName   = "";
            ExternalIds = "";
            CurrentMode = LoginMode.None;

            PlayerPrefs.DeleteKey(PrefGuestName);
            PlayerPrefs.DeleteKey(PrefLoginMode);
            PlayerPrefs.DeleteKey(PrefPlayerSignedIn);
            PlayerPrefs.SetInt(PrefHasLoggedOut, 1);
            PlayerPrefs.Save();

            // Sign out PlayerAccountService first (owns the portal session). 
            if (PlayerAccountService.Instance.IsSignedIn)
                PlayerAccountService.Instance.SignOut();

            // Then sign out AuthenticationService and clear the on-disk session token.
            if (AuthenticationService.Instance.IsSignedIn ||
                AuthenticationService.Instance.SessionTokenExists)
                AuthenticationService.Instance.SignOut(clearCredentials: true);

            Debug.Log("[AuthManager] SignOut complete — token cleared, logout flag set.");
            NotifyStateChanged();
        }

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

        public void OpenAccountPortal()
            => Application.OpenURL(PlayerAccountService.Instance.AccountPortalUrl);

        // ── Private helpers ───────────────────────────────────────────────────────

        private async Task SignInWithUnityInternalAsync()
        {
            _isSigningIn = true;
            try { await ExchangeTokenAsync(); }
            catch (RequestFailedException ex) { Debug.LogException(ex); }
            finally { _isSigningIn = false; NotifyStateChanged(); }
        }

        /// <summary>
        /// Clears any existing Auth session, then exchanges the PlayerAccount
        /// access token for a fresh Unity Auth session token saved to disk.
        /// </summary>
        private async Task ExchangeTokenAsync()
        {
            string token = PlayerAccountService.Instance.AccessToken;
            if (string.IsNullOrEmpty(token))
            {
                Debug.LogWarning("[AuthManager] ExchangeTokenAsync: access token empty.");
                return;
            }

            if (AuthenticationService.Instance.IsSignedIn)
            {
                GuestName = "";
                PlayerPrefs.DeleteKey(PrefGuestName);
                PlayerPrefs.DeleteKey(PrefHasLoggedOut);
                PlayerPrefs.Save();
                return;
            }

            // Clear any stale credentials before signing in with the new token.
            if (AuthenticationService.Instance.IsSignedIn ||
                AuthenticationService.Instance.SessionTokenExists)
            {
                AuthenticationService.Instance.SignOut(clearCredentials: true);
                await Task.Yield();     // let the SDK finish clearing before re-signing
            }

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
                        float waited = 0f;
                        const float waitTimeout = 5f;
                        while (!AuthenticationService.Instance.IsSignedIn && waited < waitTimeout)
                        {
                            await Task.Yield();
                            waited += Time.unscaledDeltaTime;
                        }

                        if (AuthenticationService.Instance.IsSignedIn)
                        {
                            signInSucceeded = true;
                            break;
                        }

                        continue;
                    }

                    throw;
                }
            }

            if (!signInSucceeded)
            {
                Debug.LogWarning("[AuthManager] ExchangeTokenAsync: sign-in did not complete.");
                return;
            }

            // Session token is now saved to disk; clear the logout flag.
            GuestName = "";
            PlayerPrefs.DeleteKey(PrefGuestName);
            PlayerPrefs.DeleteKey(PrefHasLoggedOut);
            PlayerPrefs.Save();
        }

        private void SetUnityAccountMode()
        {
            CurrentMode = LoginMode.UnityAccount;
            ExternalIds = BuildExternalIds(AuthenticationService.Instance.PlayerInfo);
        }

        private static string BuildExternalIds(PlayerInfo info)
        {
            if (info?.Identities == null) return "None";
            var sb = new System.Text.StringBuilder();
            foreach (var id in info.Identities)
                sb.Append(" ").Append(id.TypeId);
            return sb.ToString();
        }

        private void NotifyStateChanged() => OnStateChanged?.Invoke();
    }
}