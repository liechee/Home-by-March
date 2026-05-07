using System.Collections;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts.Samples;
using TMPro;
using System.Text;
using UnityEngine.SceneManagement;

public class PlayerProfileStatus : MonoBehaviour
{
    [SerializeField] TMP_Text m_ProfileStatusText;
    Coroutine _waitForAuthCoroutine;

    private async void Awake()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            try
            {
                await UnityServices.InitializeAsync();
            }
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
        RegisterWithPlayerAccountsDemo();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeAuthEvents();
        UnsubscribeAuthManager();
        if (_waitForAuthCoroutine != null)
        {
            StopCoroutine(_waitForAuthCoroutine);
            _waitForAuthCoroutine = null;
        }
    }

    private void RegisterWithPlayerAccountsDemo()
    {
        ResolveProfileText();
        if (m_ProfileStatusText == null) return;

        var demo = FindObjectOfType<PlayerAccountsDemo>();
        if (demo != null)
            demo.SetExternalUiTargets(null, m_ProfileStatusText, null, null);
    }

    private void OnAuthChanged()
    {
        RequestRefresh();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RequestRefresh();
    }

    private void RequestRefresh()
    {
        if (!isActiveAndEnabled) return;

        ResolveProfileText();

        if (m_ProfileStatusText == null)
        {
            Debug.LogWarning("[PlayerProfileStatus] No TMP_Text found to update.");
            return;
        }

        if (_waitForAuthCoroutine != null)
            StopCoroutine(_waitForAuthCoroutine);

        _waitForAuthCoroutine = StartCoroutine(RefreshUntilAuthReady());
    }

    private IEnumerator RefreshUntilAuthReady()
    {
        const float timeout = 5f;
        float elapsed = 0f;

        yield return null;

        while (elapsed < timeout)
        {
            ResolveProfileText();

            if (m_ProfileStatusText == null)
                yield break;

            if (AuthManager.Instance != null && AuthManager.Instance.IsReady)
            {
                UpdateProfileStatus();
                break;
            }

            UpdateProfileStatus();
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        UpdateProfileStatus();
        _waitForAuthCoroutine = null;
    }

    private void TrySubscribeAuthEvents()
    {
        if (AuthenticationService.Instance == null) return;

        AuthenticationService.Instance.SignedIn -= OnAuthChanged;
        AuthenticationService.Instance.SignedIn += OnAuthChanged;
        AuthenticationService.Instance.SignedOut -= OnAuthChanged;
        AuthenticationService.Instance.SignedOut += OnAuthChanged;
        AuthenticationService.Instance.Expired -= OnAuthChanged;
        AuthenticationService.Instance.Expired += OnAuthChanged;
    }

    private void UnsubscribeAuthEvents()
    {
        if (AuthenticationService.Instance == null) return;

        AuthenticationService.Instance.SignedIn -= OnAuthChanged;
        AuthenticationService.Instance.SignedOut -= OnAuthChanged;
        AuthenticationService.Instance.Expired -= OnAuthChanged;
    }

    private void TrySubscribeAuthManager()
    {
        if (AuthManager.Instance == null) return;

        AuthManager.Instance.OnStateChanged -= OnAuthChanged;
        AuthManager.Instance.OnStateChanged += OnAuthChanged;
    }

    private void UnsubscribeAuthManager()
    {
        if (AuthManager.Instance == null) return;
        AuthManager.Instance.OnStateChanged -= OnAuthChanged;
    }

    private void UpdateProfileStatus()
    {
        ResolveProfileText();

        if (m_ProfileStatusText == null) return;

        bool signedIn = IsSignedInStable();

        const string colorOpen = "<color=#FFEE00>";
        const string colorClose = "</color>";

        var sb2 = new StringBuilder();
        sb2.AppendLine(signedIn
            ? $"{colorOpen}Your journey is safe.{colorClose}"
            : $"{colorOpen}Log in to save your journey.{colorClose}");

        m_ProfileStatusText.text = sb2.ToString();
    }

    private bool IsSignedInStable()
    {
        bool liveSignedIn = false;

        if (AuthManager.Instance != null && AuthManager.Instance.IsReady)
            liveSignedIn = AuthManager.Instance.IsSignedIn;
        else if (AuthenticationService.Instance != null)
            liveSignedIn = AuthenticationService.Instance.IsSignedIn;

        if (liveSignedIn)
            return true;

        if (PlayerPrefs.GetInt("PlayerSignedIn", 0) == 1)
            return true;

        return PlayerPrefs.GetString(AuthManager.PrefLoginMode, "") == "Unity";
    }

    private void ResolveProfileText()
    {
        if (m_ProfileStatusText != null)
            return;

        m_ProfileStatusText = GetComponent<TMP_Text>();
        if (m_ProfileStatusText != null)
            return;

        m_ProfileStatusText = GetComponentInChildren<TMP_Text>(true);
        if (m_ProfileStatusText != null)
            return;

        m_ProfileStatusText = GetComponentInParent<TMP_Text>(true);
    }

    private void OnDestroy()
    {
        UnsubscribeAuthEvents();
        UnsubscribeAuthManager();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
