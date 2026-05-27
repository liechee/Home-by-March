using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using System.Collections.Generic;

/// <summary>
/// Central authentication hub.
///
/// Responsibilities:
///   - Initialize Unity Services once.
///   - Restore cached session on startup (persistent sign-in).
///   - Sign in / sign up with username+password provider.
///   - Link a guest account to a username+password account.
///   - Save and load player profile to/from Cloud Save.
///   - Save and load full player game data to/from Cloud Save.
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
    private const string CloudKeyPlayerData = "playerData";

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired whenever auth state changes (signed in, signed out, guest, etc.).</summary>
    public event Action OnStateChanged;

    /// <summary>Fired when player data is loaded from cloud.</summary>
    public event Action OnPlayerDataLoaded;

    /// <summary>Fired when player data is saved to cloud.</summary>
    public event Action OnPlayerDataSaved;

    // ── Public state ──────────────────────────────────────────────────────────

    public bool IsReady { get; private set; }
    public bool IsSignedIn => AuthenticationService.Instance?.IsSignedIn ?? false;
    public bool IsGuest => !string.IsNullOrEmpty(GuestName) && !IsSignedIn;
    public string GuestName { get; private set; }

    /// <summary>Cloud-loaded username (null until LoadProfileFromCloud completes).</summary>
    public string CloudUsername { get; private set; }

    /// <summary>Reference to the PlayerData component</summary>
    public PlayerData CurrentPlayerData { get; private set; }

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

        // Find PlayerData reference
        FindPlayerData();

        _ = InitializeAsync();
    }

    private void FindPlayerData()
    {
        CurrentPlayerData = FindObjectOfType<PlayerData>();
        if (CurrentPlayerData == null)
        {
            Debug.LogWarning("[AuthManager] PlayerData not found in scene.");
        }
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

            Debug.Log($"[AuthManager] Session restored for player: {GetPlayerId()}");

            // Load the cloud profile so CloudUsername is populated.
            await LoadProfileFromCloud();
            
            // Load player data to PlayerData component
            await LoadPlayerDataToPlayerData();
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

    // ── Player ID Methods ─────────────────────────────────────────────────────

    /// <summary>
    /// Gets the current player ID from Unity Authentication
    /// </summary>
    public string GetPlayerId()
    {
        if (AuthenticationService.Instance != null && !string.IsNullOrEmpty(AuthenticationService.Instance.PlayerId))
        {
            return AuthenticationService.Instance.PlayerId;
        }
        
        // For guest or offline fallback
        if (IsGuest && !string.IsNullOrEmpty(GuestName))
        {
            return $"guest_{GuestName}";
        }
        
        // Fallback to device ID
        return SystemInfo.deviceUniqueIdentifier;
    }

    /// <summary>
    /// Gets the current player name (username or guest name)
    /// </summary>
    public string GetCurrentPlayerName()
    {
        if (IsSignedIn && !string.IsNullOrEmpty(CloudUsername))
            return CloudUsername;
        if (IsGuest && !string.IsNullOrEmpty(GuestName))
            return GuestName;
        if (CurrentPlayerData != null && !string.IsNullOrEmpty(CurrentPlayerData.playerName))
            return CurrentPlayerData.playerName;
        return "Player";
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
            // Ensure any existing guest session is cleared
            GuestName = null;
            
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);

            // Load profile from cloud first to get existing data
            await LoadProfileFromCloud();
            
            // If cloud profile doesn't exist, save it
            if (string.IsNullOrEmpty(CloudUsername))
            {
                await SaveProfileToCloud(username, "UsernamePassword");
            }
            
            // Load player data from cloud to PlayerData component
            await LoadPlayerDataToPlayerData();

            // Update PlayerData name
            if (CurrentPlayerData != null)
            {
                CurrentPlayerData.ChangePlayerName(username);
            }

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

            // Clear guest data
            GuestName = null;
            CloudUsername = null;

            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);

            await SaveProfileToCloud(username, "UsernamePassword");
            
            // Update PlayerData with username
            if (CurrentPlayerData != null)
            {
                CurrentPlayerData.ChangePlayerName(username);
                await SavePlayerDataToCloud();
            }

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
    /// </summary>
    public async Task<AuthResult> UpgradeGuestToAccountAsync(string username, string password)
    {
        if (!AuthenticationService.Instance.IsSignedIn)
            return AuthResult.Fail("No active session to upgrade. Please log in first.");

        try
        {
            // Save current PlayerData before upgrade
            var currentPlayerData = CurrentPlayerData;
            
            await AuthenticationService.Instance.AddUsernamePasswordAsync(username, password);

            // Overwrite the cloud profile with the new permanent identity
            await SaveProfileToCloud(username, "UsernamePassword");
            
            // Preserve player data
            if (currentPlayerData != null && !string.IsNullOrEmpty(currentPlayerData.playerName))
            {
                currentPlayerData.ChangePlayerName(username);
                await SavePlayerDataToCloud();
            }

            GuestName = null;

            PlayerPrefs.SetString("LastSignedInPlayer", username);
            PlayerPrefs.SetInt(PrefPlayerSignedIn, 1);
            PlayerPrefs.DeleteKey("LastGuestUsername");
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
            // Clear any existing cloud username
            CloudUsername = null;
            
            // Create a real anonymous Unity session so the player ID exists
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            GuestName = guestUsername;
            
            // Update PlayerData with guest name
            if (CurrentPlayerData != null)
            {
                CurrentPlayerData.ChangePlayerName(guestUsername);
                await SavePlayerDataToCloud();
            }
            
            PlayerPrefs.SetString("LastGuestUsername", guestUsername);
            PlayerPrefs.SetString("LastLoginMethod", "Guest");
            // Don't set PrefPlayerSignedIn for guests
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
        // Save current PlayerData before signing out
        if (CurrentPlayerData != null)
        {
            _ = SavePlayerDataToCloud();
        }
        
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

    // ── Cloud Save - Profile ──────────────────────────────────────────────────

    /// <summary>
    /// Saves the player's username and identity metadata to Unity Cloud Save.
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
                { CloudKeyPlayerId,    GetPlayerId() },
                { CloudKeyLoginMethod, loginMethod },
                { CloudKeyCreatedAt,   DateTime.UtcNow.ToString("o") }
            };

            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            CloudUsername = username;

            Debug.Log($"[AuthManager] Profile saved to cloud for: {username} (ID: {GetPlayerId()})");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AuthManager] Cloud save failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the player's profile from Unity Cloud Save.
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

            if (result.TryGetValue(CloudKeyUsername, out var usernameItem))
            {
                CloudUsername = usernameItem.Value.GetAs<string>();
                Debug.Log($"[AuthManager] Cloud profile loaded — username: {CloudUsername}, ID: {GetPlayerId()}");
            }
            else
            {
                CloudUsername = null;
                Debug.Log("[AuthManager] No existing cloud profile found.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AuthManager] Cloud load failed: {ex.Message}");
            CloudUsername = null;
        }
    }

    // ── Cloud Save - Player Data (Compatible with PlayerData component) ───────

    /// <summary>
    /// Saves PlayerData to Unity Cloud Save
    /// </summary>
    public async Task SavePlayerDataToCloud()
    {
        if (CurrentPlayerData == null)
        {
            Debug.LogWarning("[AuthManager] Cannot save player data — PlayerData is null.");
            FindPlayerData();
            if (CurrentPlayerData == null) return;
        }
        
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.LogWarning("[AuthManager] Cannot save player data — player not signed in.");
            return;
        }

        try
        {
            // Build save data from PlayerData component
            var playerSaveData = new PlayerDataSaver
            {
                playerName = CurrentPlayerData.playerName,
                level = CurrentPlayerData.level,
                health = CurrentPlayerData.health,
                attack = CurrentPlayerData.attack,
                defense = CurrentPlayerData.defense,
                cooldown = CurrentPlayerData.cooldown,
                movementSpeed = CurrentPlayerData.movementSpeed,
                gold = CurrentPlayerData.gold,
                attackSpeed = CurrentPlayerData.attackSpeed
            };

            var data = new Dictionary<string, object>
            {
                { CloudKeyPlayerData, JsonUtility.ToJson(playerSaveData) }
            };

            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            
            Debug.Log($"[AuthManager] PlayerData saved to cloud for: {CurrentPlayerData.playerName} (Level: {CurrentPlayerData.level}, Gold: {CurrentPlayerData.gold})");
            OnPlayerDataSaved?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AuthManager] Failed to save player data: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads PlayerData from Unity Cloud Save and applies to PlayerData component
    /// </summary>
    public async Task LoadPlayerDataToPlayerData()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.LogWarning("[AuthManager] Cannot load player data — player not signed in.");
            return;
        }

        if (CurrentPlayerData == null)
        {
            Debug.LogWarning("[AuthManager] Cannot load player data — PlayerData component not found.");
            FindPlayerData();
            if (CurrentPlayerData == null) return;
        }

        try
        {
            var keys = new HashSet<string> { CloudKeyPlayerData };
            var result = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

            if (result.TryGetValue(CloudKeyPlayerData, out var dataItem) && dataItem.Value != null)
            {
                string jsonData = dataItem.Value.GetAs<string>();
                if (!string.IsNullOrEmpty(jsonData))
                {
                    var loadedData = JsonUtility.FromJson<PlayerDataSaver>(jsonData);
                    
                    if (loadedData != null)
                    {
                        // Apply loaded data to PlayerData component
                        CurrentPlayerData.playerName = loadedData.playerName;
                        CurrentPlayerData.level = loadedData.level;
                        CurrentPlayerData.health = loadedData.health;
                        CurrentPlayerData.attack = loadedData.attack;
                        CurrentPlayerData.defense = loadedData.defense;
                        CurrentPlayerData.cooldown = loadedData.cooldown;
                        CurrentPlayerData.movementSpeed = loadedData.movementSpeed;
                        CurrentPlayerData.gold = loadedData.gold;
                        CurrentPlayerData.attackSpeed = loadedData.attackSpeed;
                        
                        CurrentPlayerData.UpdateCurrentStats();
                        CurrentPlayerData.SavePlayerData(); // Save to local file
                        
                        Debug.Log($"[AuthManager] PlayerData loaded from cloud for: {CurrentPlayerData.playerName} (Level: {CurrentPlayerData.level})");
                        OnPlayerDataLoaded?.Invoke();
                        return;
                    }
                }
            }
            
            // No cloud data found, save current PlayerData to cloud
            Debug.Log("[AuthManager] No existing PlayerData in cloud. Saving current data.");
            await SavePlayerDataToCloud();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AuthManager] Failed to load player data: {ex.Message}");
        }
    }

    /// <summary>
    /// Force sync PlayerData to cloud (call when PlayerData changes)
    /// </summary>
    public async Task SyncPlayerDataToCloud()
    {
        if (CurrentPlayerData != null && IsSignedIn)
        {
            await SavePlayerDataToCloud();
        }
    }

    // ── SDK event relays ──────────────────────────────────────────────────────

    private void OnSdkSignedIn() 
    {
        Debug.Log("[AuthManager] SDK Signed In event received.");
        OnStateChanged?.Invoke();
    }
    
    private void OnSdkSignedOut() 
    {
        Debug.Log("[AuthManager] SDK Signed Out event received.");
        OnStateChanged?.Invoke();
    }

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
        10000 => "Session error. Please sign out and try again.",
        _ => $"Sign in failed: {fallback}"
    };

    private static string MapSignUpError(int code, string fallback) => code switch
    {
        1000 => "Invalid parameters. Please check your inputs.",
        1001 => "Password does not meet requirements (min 6 characters).",
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