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

        [SerializeField]
        TMP_Text m_StatusText;
        [SerializeField]
        GameObject m_SignOut;
        [SerializeField]
        GameObject m_SignIn;


        string m_ExternalIds;
        private bool _isSigningIn = false;

        private async void Awake()
        {
            await UnityServices.InitializeAsync();
            PlayerAccountService.Instance.SignedIn += OnPlayerAccountSignedIn;
            SetupEvents();
            //UpdateUI();
            // Check if already signed in on game reopen
            if (AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("Already signed in. Updating UI.");
                m_ExternalIds = GetExternalIds(AuthenticationService.Instance.PlayerInfo);
                UpdateUI();
            }
            else
            {
                Debug.Log("Not signed in yet. Waiting for PlayerAccountService.");
                PlayerAccountService.Instance.SignedIn += OnPlayerAccountSignedIn;
                UpdateUI();
            }
        }

        private void OnPlayerAccountSignedIn()
        {
            if (!_isSigningIn && !AuthenticationService.Instance.IsSignedIn)
            {
                SignInWithUnity();
            }
        }

        public async void StartSignInAsync()
        {
            if (_isSigningIn) return;
            _isSigningIn = true;

            try
            {
                if (!PlayerAccountService.Instance.IsSignedIn)
                {
                    await PlayerAccountService.Instance.StartSignInAsync();
                    Debug.Log("PlayerAccountService signed in.");
                    // OnPlayerAccountSignedIn will be called by the event
                }
                if (PlayerAccountService.Instance.IsSignedIn && !AuthenticationService.Instance.IsSignedIn)
                {
                    Debug.Log("Signing in with Unity using access token...");
                    SignInWithUnity(); // Use access token to authenticate
                }
            }
            catch (RequestFailedException ex)
            {
                Debug.LogException(ex);
                SetException(ex);
            }
            finally
            {
                _isSigningIn = false;
                UpdateUI();
            }
        }

        public void SignOut()
        {
            AuthenticationService.Instance.SignOut();

            PlayerAccountService.Instance.SignOut();
            Debug.Log("Signed out of Player Accounts and Authentication Service");

            UpdateUI();
        }

        public void OpenAccountPortal()
        {
            Application.OpenURL(PlayerAccountService.Instance.AccountPortalUrl);
        }

        async void SignInWithUnity()
        {
            if (_isSigningIn || AuthenticationService.Instance.IsSignedIn)
                return;

            _isSigningIn = true;

            try
            {
                var accessToken = PlayerAccountService.Instance.AccessToken;
                if (!string.IsNullOrEmpty(accessToken))
                {
                    await AuthenticationService.Instance.SignInWithUnityAsync(accessToken);
                    Debug.Log("Successfully signed in with Unity.");
                    m_ExternalIds = GetExternalIds(AuthenticationService.Instance.PlayerInfo);
                    UpdateUI();
                }
                else
                {
                    Debug.LogWarning("No access token available for Unity sign-in.");
                }
            }
            catch (RequestFailedException ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                _isSigningIn = false;
            }
        }

        void UpdateUI()
        {
            // var statusBuilder = new StringBuilder();

            // if (AuthenticationService.Instance.IsSignedIn)
            // {
            //     m_SignOut.SetActive(true);
            //     m_SignIn.SetActive(false);
            //     statusBuilder.AppendLine("Signed in");
            //     statusBuilder.AppendLine(GetPlayerInfoText());
            // }
            // else
            // {
            //     m_SignOut.SetActive(false);
            //     m_SignIn.SetActive(true);
            //     statusBuilder.AppendLine("Not signed in");
            // }

            // m_StatusText.text = statusBuilder.ToString();
            // Debug.Log("UI Updated: " + statusBuilder.ToString());
            if (m_StatusText == null || m_SignOut == null || m_SignIn == null)
            {
                Debug.LogWarning("UI references are missing!");
                return;
            }
            StringBuilder statusBuilder = new();

            bool playerSignedIn = AuthenticationService.Instance.IsSignedIn;
            Debug.Log("UpdateUI() called. Auth IsSignedIn: " + playerSignedIn);

            if (playerSignedIn)
            {
                m_SignOut.SetActive(true);
                m_SignIn.SetActive(false);
                statusBuilder.AppendLine("Signed in");
                statusBuilder.AppendLine(GetPlayerInfoText());
            }
            else
            {
                m_SignOut.SetActive(false);
                m_SignIn.SetActive(true);
                statusBuilder.AppendLine("Not signed in");
            }

            m_StatusText.text = statusBuilder.ToString();
        }

        string GetExternalIds(PlayerInfo playerInfo)
        {
            if (playerInfo.Identities == null)
            {
                return "None";
            }

            var sb = new StringBuilder();
            foreach (var id in playerInfo.Identities)
            {
                sb.Append(" " + id.TypeId);
            }

            return sb.ToString();
        }

        string GetPlayerInfoText()
        {
            return $"ExternalIds: <b>{m_ExternalIds}</b>";
        }

        void SetException(Exception ex)
        {
            // m_ExceptionText.text = ex != null ? $"{ex.GetType().Name}: {ex.Message}" : "";
        }
        void SetupEvents()
        {
            AuthenticationService.Instance.SignedIn += () =>
            {
                // Shows how to get a playerID
                Debug.Log($"PlayerID: {AuthenticationService.Instance.PlayerId}");

                // Shows how to get an access token
                Debug.Log($"Access Token: {AuthenticationService.Instance.AccessToken}");

            };

            AuthenticationService.Instance.SignInFailed += (err) =>
            {
                Debug.LogError(err);
            };

            AuthenticationService.Instance.SignedOut += () =>
            {
                Debug.Log("Player signed out.");
            };

            AuthenticationService.Instance.Expired += () =>
              {
                  Debug.Log("Player session could not be refreshed and expired.");
              };
        }

        // void OnApplicationQuit()
        // {
        //     AuthenticationService.Instance.SignOut();
        //     Debug.Log("Signed out on application quit.");
        // }
    }

}