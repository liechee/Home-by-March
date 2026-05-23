using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using System.Collections.Generic;
using CloudSaveItem = Unity.Services.CloudSave.Models.Item;

/// <summary>
/// Central authentication hub.
///
/// Responsibilities:
///   - Initialize Unity Services once.
///   - Restore cached session on startup (persistent sign-in).
///   - Sign in / sign up with username+password provider.
///   - Link a guest account to a username+password account.
///   - Save and load player profile to/from Cloud Save.
///   - Broadcast state changes via OnStateChanged.
///
/// All other scripts (LoginForm, SignUpForm, GuestLoginManager) talk to
/// AuthManager — they own NO auth logic themselves.
/// </summary>
public class AuthManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static AuthManager Instance { get; private set; }

    // ── Public constants ──────────────────────────────────────────────────────

    /// <summary>PlayerPrefs key: 1 = a real account is (or was) active.</summary>
    public const string PrefPlayerSignedIn = "PlayerSignedIn";

    // ── Cloud Save keys ───────────────────────────────────────────────────────

    private const string CloudKeyUsername = "username";
    private const string CloudKeyPlayerId = "playerId";
    private const string CloudKeyLoginMethod = "loginMethod";
    private const string CloudKeyCreatedAt = "accountCreatedAt";

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired whenever auth state changes (signed in, signed out, guest, etc.).</summary>
    public event Action OnStateChanged;

    // ── Public state ──────────────────────────────────────────────────────────

    public bool IsReady { get; private set; }
    public bool IsSignedIn => AuthenticationService.Instance?.IsSignedIn ?? false;
    public bool IsGuest => !string.IsNullOrEmpty(GuestName) && !IsSignedIn;
    public string GuestName { get; private set; }

    /// <summary>Cloud-loaded username (null until LoadProfileFromCloud completes).</summary>
    public string CloudUsername { get; private set; }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _ = InitializeAsync();
    }

    // ── Initialization ────────────────────────────────────────────────────────

    /// <summary>
    /// Initializes Unity Services then attempts to restore a cached session.
    /// After this completes, IsReady = true and OnStateChanged fires.
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            // Hook SDK events so we stay in sync if the token silently refreshes.
            AuthenticationService.Instance.SignedIn += OnSdkSignedIn;
            AuthenticationService.Instance.SignedOut += OnSdkSignedOut;
            AuthenticationService.Instance.Expired += OnSdkExpired;

            await TryRestoreSessionAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"[AuthManager] Initialization failed: {e.Message}");
        }
        finally
        {
            IsReady = true;
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Attempts to silently restore the player's previous session from the
    /// cached token. This is what makes "stay signed in" work across launches.
    /// </summary>
    private async Task TryRestoreSessionAsync()
    {
        if (!AuthenticationService.Instance.SessionTokenExists)
        {
            Debug.Log("[AuthManager] No cached session token found.");
            return;
        }

        try
        {
            // SignInAnonymouslyAsync reuses the existing session token if one
            // exists — it does NOT create a new anonymous account.
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Debug.Log($"[AuthManager] Session restored for player: {AuthenticationService.Instance.PlayerId}");

            // Load the cloud profile so CloudUsername is populated.
            await LoadProfileFromCloud();
        }
        catch (AuthenticationException ex)
        {
            Debug.LogWarning($"[AuthManager] Session restore failed (token expired?): {ex.Message}");
            // Token is stale — clear it so the login form shows normally.
            AuthenticationService.Instance.ClearSessionToken();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AuthManager] Session restore failed: {ex.Message}");
        }
    }

    // ── Sign in ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Signs in with a username+password account.
    /// On success, saves the profile to cloud and fires OnStateChanged.
    /// </summary>
    public async Task<AuthResult> SignInAsync(string username, string password)
    {
        try
        {
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);

            await SaveProfileToCloud(username, "UsernamePassword");

            PlayerPrefs.SetString("LastSignedInPlayer", username);
            PlayerPrefs.SetInt(PrefPlayerSignedIn, 1);
            PlayerPrefs.Save();

            OnStateChanged?.Invoke();
            return AuthResult.Success();
        }
        catch (AuthenticationException ex)
        {
            return AuthResult.Fail(MapSignInError(ex.ErrorCode, ex.Message));
        }
        catch (RequestFailedException ex)
        {
            return AuthResult.Fail(MapRequestError(ex.ErrorCode, ex.Message));
        }
        catch (Exception ex)
        {
            return AuthResult.Fail($"Sign in failed: {ex.Message}");
        }
    }

    // ── Sign up ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new username+password account.
    /// On success, saves the profile to cloud and fires OnStateChanged.
    /// </summary>
    public async Task<AuthResult> SignUpAsync(string username, string password)
    {
        try
        {
            // Ensure no stale session interferes.
            if (AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut();
                await Task.Delay(100);
            }

            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);

            await SaveProfileToCloud(username, "UsernamePassword");

            PlayerPrefs.SetString("LastSignedInPlayer", username);
            PlayerPrefs.SetInt(PrefPlayerSignedIn, 1);
            PlayerPrefs.Save();

            OnStateChanged?.Invoke();
            return AuthResult.Success();
        }
        catch (AuthenticationException ex)
        {
            return AuthResult.Fail(MapSignUpError(ex.ErrorCode, ex.Message));
        }
        catch (RequestFailedException ex)
        {
            return AuthResult.Fail(MapSignUpRequestError(ex.ErrorCode, ex.Message));
        }
        catch (Exception ex)
        {
            return AuthResult.Fail($"Registration failed: {ex.Message}");
        }
    }

    // ── Guest → Account upgrade ───────────────────────────────────────────────

    /// <summary>
    /// Links the current guest session to a new username+password account,
    /// preserving all cloud data already saved under the guest player ID.
    ///
    /// Call this when a guest player taps "Register Account" in-game.
    /// The caller (LoginForm or SignUpForm) only needs to pass username+password.
    /// </summary>
    public async Task<AuthResult> UpgradeGuestToAccountAsync(string username, string password)
    {
        if (!AuthenticationService.Instance.IsSignedIn)
            return AuthResult.Fail("No active session to upgrade. Please log in first.");

        try
        {
            await AuthenticationService.Instance.AddUsernamePasswordAsync(username, password);

            // Overwrite the cloud profile with the new permanent identity.
            await SaveProfileToCloud(username, "UsernamePassword");

            GuestName = null;

            PlayerPrefs.SetString("LastSignedInPlayer", username);
            PlayerPrefs.SetInt(PrefPlayerSignedIn, 1);
            PlayerPrefs.Save();

            Debug.Log($"[AuthManager] Guest account upgraded to: {username}");
            OnStateChanged?.Invoke();
            return AuthResult.Success();
        }
        catch (AuthenticationException ex)
        {
            return AuthResult.Fail(MapSignUpError(ex.ErrorCode, ex.Message));
        }
        catch (RequestFailedException ex)
        {
            return AuthResult.Fail(MapSignUpRequestError(ex.ErrorCode, ex.Message));
        }
        catch (Exception ex)
        {
            return AuthResult.Fail($"Account upgrade failed: {ex.Message}");
        }
    }

    // ── Guest session ─────────────────────────────────────────────────────────

    /// <summary>
    /// Records a guest username for the current session (no Unity account created).
    /// Fires OnStateChanged so UI reflects the guest state.
    /// </summary>
    public async Task<AuthResult> SetGuestSessionAsync(string guestUsername)
    {
        try
        {
            // Create a real anonymous Unity session so the player ID exists
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            GuestName = guestUsername;
            PlayerPrefs.SetString("LastGuestUsername", guestUsername);
            PlayerPrefs.SetString("LastLoginMethod", "Guest");
            PlayerPrefs.Save();

            OnStateChanged?.Invoke();
            return AuthResult.Success();
        }
        catch (Exception ex)
        {
            return AuthResult.Fail($"Guest login failed: {ex.Message}");
        }
    }

    // ── Sign out ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Signs out and clears the session token so the next launch shows the login form.
    /// </summary>
    public void SignOut()
    {
        GuestName = null;
        CloudUsername = null;

        if (AuthenticationService.Instance.IsSignedIn)
            AuthenticationService.Instance.SignOut();

        AuthenticationService.Instance.ClearSessionToken();

        PlayerPrefs.DeleteKey(PrefPlayerSignedIn);
        PlayerPrefs.DeleteKey("LastSignedInPlayer");
        PlayerPrefs.Save();

        OnStateChanged?.Invoke();
        Debug.Log("[AuthManager] Signed out and session token cleared.");
    }

    // ── Cloud Save ────────────────────────────────────────────────────────────

    /// <summary>
    /// Saves the player's username and identity metadata to Unity Cloud Save.
    /// Called automatically after sign-in, sign-up, and guest upgrade.
    /// </summary>
    public async Task SaveProfileToCloud(string username, string loginMethod)
    {
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.LogWarning("[AuthManager] Cannot save to cloud — player not signed in.");
            return;
        }

        try
        {
            var data = new Dictionary<string, object>
            {
                { CloudKeyUsername,    username },
                { CloudKeyPlayerId,    AuthenticationService.Instance.PlayerId },
                { CloudKeyLoginMethod, loginMethod },
                { CloudKeyCreatedAt,   DateTime.UtcNow.ToString("o") }
            };

            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            CloudUsername = username;

            Debug.Log($"[AuthManager] Profile saved to cloud for: {username}");
        }
        catch (Exception ex)
        {
            // Cloud save failure is non-fatal — log and continue.
            Debug.LogWarning($"[AuthManager] Cloud save failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the player's profile from Unity Cloud Save.
    /// Populates CloudUsername so other systems can read it without waiting.
    /// </summary>
    public async Task LoadProfileFromCloud()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.LogWarning("[AuthManager] Cannot load from cloud — player not signed in.");
            return;
        }

        try
        {
            var keys = new HashSet<string>
            {
                CloudKeyUsername,
                CloudKeyPlayerId,
                CloudKeyLoginMethod,
                CloudKeyCreatedAt
            };

            var result = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

            if (result.TryGetValue(CloudKeyUsername, out CloudSaveItem usernameItem))
            {
                CloudUsername = usernameItem.Value.GetAs<string>();
                Debug.Log($"[AuthManager] Cloud profile loaded — username: {CloudUsername}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AuthManager] Cloud load failed: {ex.Message}");
        }
    }

    // ── SDK event relays ──────────────────────────────────────────────────────

    private void OnSdkSignedIn() => OnStateChanged?.Invoke();
    private void OnSdkSignedOut() => OnStateChanged?.Invoke();

    private void OnSdkExpired()
    {
        Debug.LogWarning("[AuthManager] Session token expired.");
        OnStateChanged?.Invoke();
    }

    // ── Error mapping ─────────────────────────────────────────────────────────

    private static string MapSignInError(int code, string fallback) => code switch
    {
        1000 => "Account not found. Please sign up first.",
        1001 => "Incorrect password. Please try again.",
        1002 => "Invalid username format.",
        1003 => "Account not found. Please sign up first.",
        _ => $"Sign in failed: {fallback}"
    };

    private static string MapSignUpError(int code, string fallback) => code switch
    {
        1000 => "Invalid parameters. Please check your inputs.",
        1001 => "Password does not meet requirements.",
        1002 => "Username already taken. Please choose another.",
        10000 => "A session is already active. Please sign out first.",
        _ => $"Registration failed: {fallback}"
    };

    private static string MapRequestError(int code, string fallback) => code switch
    {
        401 => "Invalid username or password.",
        403 => "Access forbidden.",
        404 => "Service unavailable. Please try again later.",
        _ => $"Request failed: {fallback}"
    };

    private static string MapSignUpRequestError(int code, string fallback) => code switch
    {
        400 => "Invalid request. Please check your information.",
        401 => "Unauthorized. Please try again.",
        403 => "Access forbidden. Please contact support.",
        409 => "Username already exists. Please choose a different username.",
        _ => $"Registration failed. (Error: {code})"
    };
}

// ── AuthResult ─────────────────────────────────────────────────────────────────

/// <summary>Simple result type returned by all AuthManager async methods.</summary>
public readonly struct AuthResult
{
    public bool IsSuccess { get; }
    public string ErrorMessage { get; }

    private AuthResult(bool ok, string msg)
    {
        IsSuccess = ok;
        ErrorMessage = msg;
    }

    public static AuthResult Success() => new AuthResult(true, null);
    public static AuthResult Fail(string msg) => new AuthResult(false, msg);
}
