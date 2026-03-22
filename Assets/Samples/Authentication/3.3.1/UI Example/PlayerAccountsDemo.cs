using System;
using System.Text;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Unity.Services.Authentication.PlayerAccounts.Samples
{
    class PlayerAccountsDemo : MonoBehaviour
    {
        [SerializeField] TMP_Text m_StatusText;
        [SerializeField] GameObject m_SignOut;
        [SerializeField] GameObject m_SignIn;

        private bool _isSigningIn = false;
        private string m_ExternalIds = "";
        private async void Awake()
        {
            await UnityServices.InitializeAsync();

            SetupAuthEvents();

            PlayerAccountService.Instance.SignedIn += OnPlayerAccountServiceSignedIn;

            // If the player was already signed in from a previous session
            // (Unity persists sessions across launches), trigger the post-signin
            // flow immediately so OverallStepCounter loads cloud data.
            if (AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("[Auth] Already signed in on launch — triggering cloud load.");
                m_ExternalIds = GetExternalIds(AuthenticationService.Instance.PlayerInfo);
                OnFullySignedIn();
            }

            UpdateUI();
        }
        public async void StartSignInAsync()
        {
            if (_isSigningIn) return;
            _isSigningIn = true;

            try
            {
                if (!PlayerAccountService.Instance.IsSignedIn)
                    await PlayerAccountService.Instance.StartSignInAsync();

                if (PlayerAccountService.Instance.IsSignedIn &&
                    !AuthenticationService.Instance.IsSignedIn)
                {
                    await SignInWithUnity();
                }
            }
            catch (RequestFailedException ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                _isSigningIn = false;
                UpdateUI();
            }
        }


        private void OnPlayerAccountServiceSignedIn()
        {
            if (!_isSigningIn && !AuthenticationService.Instance.IsSignedIn)
                _ = SignInWithUnityAsync();
        }

        private async System.Threading.Tasks.Task SignInWithUnityAsync()
        {
            _isSigningIn = true;
            try { await SignInWithUnity(); }
            catch (RequestFailedException ex) { Debug.LogException(ex); }
            finally { _isSigningIn = false; UpdateUI(); }
        }

        private async System.Threading.Tasks.Task SignInWithUnity()
        {
            if (AuthenticationService.Instance.IsSignedIn) return;

            string accessToken = PlayerAccountService.Instance.AccessToken;
            if (string.IsNullOrEmpty(accessToken))
            {
                Debug.LogWarning("[Auth] No access token available.");
                return;
            }

            await AuthenticationService.Instance.SignInWithUnityAsync(accessToken);
            Debug.Log("[Auth] Signed in with Unity.");
            m_ExternalIds = GetExternalIds(AuthenticationService.Instance.PlayerInfo);
        }


        public void SignOut()
        {
            LogOutManager logoutManager = FindObjectOfType<LogOutManager>();
            if (logoutManager != null)
            {
                logoutManager.LogoutAndRestart();
            }
            else
            {
                // Fallback if LogOutManager is not in the scene
                Debug.LogWarning("[Auth] LogOutManager not found — doing bare sign-out. " +
                    "Add LogOutManager to the scene for a proper wipe.");
                AuthenticationService.Instance.SignOut();
                PlayerAccountService.Instance.SignOut();
                UpdateUI();
            }
        }

        public void OpenAccountPortal()
        {
            Application.OpenURL(PlayerAccountService.Instance.AccountPortalUrl);
        }

   
        private void OnFullySignedIn()
        {
            Debug.Log("[Auth] Player fully signed in — notifying systems.");

            
            PlayerPrefs.SetInt("PlayerSignedIn", 1);
            PlayerPrefs.Save();

            OverallStepCounter stepCounter = FindObjectOfType<OverallStepCounter>();
            if (stepCounter != null && !stepCounter.cloudLoaded)
            {
                Debug.Log("[Auth] Triggering step data cloud load.");
                _ = stepCounter.LoadStepDataFromCloud();
            }

            PlayerPrefsCloudSyncButton syncButton = FindObjectOfType<PlayerPrefsCloudSyncButton>();
            if (syncButton != null)
            {
                Debug.Log("[Auth] Triggering non-step cloud load.");
                syncButton.LoadFromCloud();
            }
            PlayerData playerData = FindObjectOfType<PlayerData>();
            if (playerData != null)
            {
                Debug.Log("[Auth] Triggering player data cloud load.");
                playerData.LoadPlayerDataFromCloud();
            }

            UpdateUI();
        }


        private void SetupAuthEvents()
        {
            AuthenticationService.Instance.SignedIn += () =>
            {
                Debug.Log($"[Auth] SignedIn — PlayerID: {AuthenticationService.Instance.PlayerId}");
                // Trigger the full post-signin flow when AuthenticationService confirms sign-in.
                // This covers both the explicit sign-in path and session restoration on relaunch.
                OnFullySignedIn();
            };

            AuthenticationService.Instance.SignInFailed += (err) =>
            {
                Debug.LogError($"[Auth] Sign-in failed: {err}");
                UpdateUI();
            };

            AuthenticationService.Instance.SignedOut += () =>
            {
                Debug.Log("[Auth] Signed out.");
                PlayerPrefs.DeleteKey("PlayerSignedIn");
                PlayerPrefs.Save();
                UpdateUI();
            };

            AuthenticationService.Instance.Expired += () =>
            {
                Debug.LogWarning("[Auth] Session expired.");
                PlayerPrefs.DeleteKey("PlayerSignedIn");
                PlayerPrefs.Save();
                UpdateUI();
            };
        }

        // ─────────────────────────────────────────────────────────
        //  UI
        // ─────────────────────────────────────────────────────────

        private void UpdateUI()
        {
            if (m_StatusText == null || m_SignOut == null || m_SignIn == null)
            {
                Debug.LogWarning("[Auth] UI references missing.");
                return;
            }

            bool signedIn = AuthenticationService.Instance.IsSignedIn;
            Debug.Log($"[Auth] UpdateUI — IsSignedIn: {signedIn}");

            m_SignOut.SetActive(signedIn);
            m_SignIn.SetActive(!signedIn);

            var sb = new StringBuilder();
            sb.AppendLine(signedIn ? "Signed in" : "Not signed in");
            if (signedIn) sb.AppendLine(GetPlayerInfoText());
            m_StatusText.text = sb.ToString();
        }



        private string GetExternalIds(PlayerInfo playerInfo)
        {
            if (playerInfo?.Identities == null) return "None";
            var sb = new StringBuilder();
            foreach (var id in playerInfo.Identities)
                sb.Append(" " + id.TypeId);
            return sb.ToString();
        }

        private string GetPlayerInfoText() =>
            $"ExternalIds: <b>{m_ExternalIds}</b>";
    }
}