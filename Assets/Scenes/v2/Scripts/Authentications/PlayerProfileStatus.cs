using System.Collections;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts.Samples;
using TMPro;
using System.Text;
using UnityEngine.SceneManagement;

/// <summary>
/// Displays a short signed-in / signed-out status string on any TMP_Text component.
///
/// Safe to place in any scene (including persistent canvases). Subscribes to both
/// AuthManager.OnStateChanged (primary) and the raw AuthenticationService events
/// (fallback for scenes that load before AuthManager is ready).
/// </summary>
public class PlayerProfileStatus : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] TMP_Text m_ProfileStatusText;

    // ── Private state ─────────────────────────────────────────────────────────

    private Coroutine _refreshCoroutine;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private async void Awake()
    {
        // Ensure Unity Services are ready before we touch AuthenticationService.
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            try { await UnityServices.InitializeAsync(); }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PlayerProfileStatus] Unity Services init failed: {ex.Message}");
            }
        }

        TrySubscribeAuthEvents();
        TrySubscribeAuthManager();
        UpdateProfileStatus();
    }

    private void OnEnable()
    {
        ResolveProfileText();
        TrySubscribeAuthEvents();
        TrySubscribeAuthManager();
        SceneManager.sceneLoaded += OnSceneLoaded;
        RequestRefresh();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeAuthEvents();
        UnsubscribeAuthManager();
        StopRefreshCoroutine();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeAuthEvents();
        UnsubscribeAuthManager();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnAuthChanged()    => RequestRefresh();
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RequestRefresh();

    // ── Refresh logic ─────────────────────────────────────────────────────────

    private void RequestRefresh()
    {
        if (!isActiveAndEnabled) return;

        ResolveProfileText();

        if (m_ProfileStatusText == null)
        {
            Debug.LogWarning("[PlayerProfileStatus] No TMP_Text target found.");
            return;
        }

        StopRefreshCoroutine();
        _refreshCoroutine = StartCoroutine(RefreshUntilAuthReady());
    }

    /// <summary>
    /// Polls for up to <c>timeout</c> seconds until AuthManager.IsReady, then
    /// performs a final status update. Updates the text on every frame so the
    /// player sees a result immediately even before the manager is ready.
    /// </summary>
    private IEnumerator RefreshUntilAuthReady()
    {
        const float timeout = 5f;
        float elapsed = 0f;

        yield return null;  // skip the frame we were requested on

        while (elapsed < timeout)
        {
            ResolveProfileText();
            if (m_ProfileStatusText == null) yield break;

            UpdateProfileStatus();

            if (AuthManager.Instance != null && AuthManager.Instance.IsReady)
                break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Final authoritative update once auth is settled.
        UpdateProfileStatus();
        _refreshCoroutine = null;
    }

    // ── Status display ────────────────────────────────────────────────────────

    private void UpdateProfileStatus()
    {
        ResolveProfileText();
        if (m_ProfileStatusText == null) return;

        bool signedIn = IsSignedInStable();

        const string kColorOpen  = "<color=#FFEE00>";
        const string kColorClose = "</color>";

        m_ProfileStatusText.text = signedIn
            ? $"{kColorOpen}Your journey is safe.{kColorClose}"
            : $"{kColorOpen}Log in to save your journey.{kColorClose}";
    }

    /// <summary>
    /// Returns true if we can confidently say the player is signed in.
    /// Checks live state first; falls back to the PrefPlayerSignedIn flag
    /// written by Scene2AuthUI after a successful cloud load.
    ///
    /// Note: does NOT fall back to PrefLoginMode because that key is deleted
    /// on every scene transition and is therefore unreliable as a status flag.
    /// </summary>
    private bool IsSignedInStable()
    {
        // 1. Live AuthManager state (most authoritative).
        if (AuthManager1.Instance != null && AuthManager1.Instance.IsReady)
            return AuthManager1.Instance.IsSignedIn;

        // 2. Raw service state (AuthManager not ready yet).
        if (UnityServices.State == ServicesInitializationState.Initialized &&
            AuthenticationService.Instance != null &&
            AuthenticationService.Instance.IsSignedIn)
            return true;

        // 3. Persisted flag written after a confirmed cloud session.
        return PlayerPrefs.GetInt(AuthManager1.PrefPlayerSignedIn, 0) == 1;
    }

    // ── Subscriptions ─────────────────────────────────────────────────────────

    private void TrySubscribeAuthEvents()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized) return;
        if (AuthenticationService.Instance == null) return;

        // Remove before adding to guarantee no duplicate subscriptions.
        AuthenticationService.Instance.SignedIn  -= OnAuthChanged;
        AuthenticationService.Instance.SignedIn  += OnAuthChanged;
        AuthenticationService.Instance.SignedOut -= OnAuthChanged;
        AuthenticationService.Instance.SignedOut += OnAuthChanged;
        AuthenticationService.Instance.Expired   -= OnAuthChanged;
        AuthenticationService.Instance.Expired   += OnAuthChanged;
    }

    private void UnsubscribeAuthEvents()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized) return;
        if (AuthenticationService.Instance == null) return;

        AuthenticationService.Instance.SignedIn  -= OnAuthChanged;
        AuthenticationService.Instance.SignedOut -= OnAuthChanged;
        AuthenticationService.Instance.Expired   -= OnAuthChanged;
    }

    private void TrySubscribeAuthManager()
    {
        if (AuthManager1.Instance == null) return;
        AuthManager1.Instance.OnStateChanged -= OnAuthChanged;
        AuthManager1.Instance.OnStateChanged += OnAuthChanged;
    }

    private void UnsubscribeAuthManager()
    {
        if (AuthManager1.Instance == null) return;
        AuthManager1.Instance.OnStateChanged -= OnAuthChanged;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void StopRefreshCoroutine()
    {
        if (_refreshCoroutine == null) return;
        StopCoroutine(_refreshCoroutine);
        _refreshCoroutine = null;
    }

    /// <summary>
    /// Tries to find a TMP_Text target in priority order:
    /// serialized field → this component → children → parent.
    /// </summary>
    private void ResolveProfileText()
    {
        if (m_ProfileStatusText != null) return;

        m_ProfileStatusText = GetComponent<TMP_Text>();
        if (m_ProfileStatusText != null) return;

        m_ProfileStatusText = GetComponentInChildren<TMP_Text>(includeInactive: true);
        if (m_ProfileStatusText != null) return;

        m_ProfileStatusText = GetComponentInParent<TMP_Text>(includeInactive: true);
    }
}