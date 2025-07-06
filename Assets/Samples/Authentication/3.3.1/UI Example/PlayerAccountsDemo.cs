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
            SetupEvents();

            // if (AuthenticationService.Instance.IsSignedIn)
            // {
            //     Debug.Log("User already signed in.");
            //     m_ExternalIds = GetExternalIds(AuthenticationService.Instance.PlayerInfo);
            //     UpdateUI();
            // }
            // else
            // {
            //     // Still waiting for PlayerAccount event?
            //     PlayerAccountService.Instance.SignedIn += SignInWithUnity;
            // }
            // if (AuthenticationService.Instance.IsAuthorized)
            // {
            //     try
            //     {
            //         await AuthenticationService.Instance.SignInWithUnityAsync(PlayerAccountService.Instance.AccessToken);
            //         Debug.Log("Signed in with cached Player Account token.");
            //         m_ExternalIds = GetExternalIds(AuthenticationService.Instance.PlayerInfo);
            //         UpdateUI();
            //     }
            //     catch (RequestFailedException ex)
            //     {
            //         Debug.Log("Could not restore Player Account session.");
            //         Debug.LogException(ex);
            //     }
            // }
            // else
            // {
            //     PlayerAccountService.Instance.SignedIn += SignInWithUnity;
            //     Debug.Log("Waiting for player to sign in manually.");
            // }
            Debug.Log("Initializing Unity Services...");

            if (PlayerAccountService.Instance.IsSignedIn)
            {
                Debug.Log("Player Account is signed in.");

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    try
                    {
                        await AuthenticationService.Instance.SignInWithUnityAsync(PlayerAccountService.Instance.AccessToken);
                        Debug.Log("Signed in with Unity Authentication.");
                    }
                    catch (RequestFailedException ex)
                    {
                        Debug.LogError("Failed to sign in with Unity Auth:");
                        Debug.LogException(ex);
                        return;
                    }
                }

                m_ExternalIds = GetExternalIds(AuthenticationService.Instance.PlayerInfo);
                UpdateUI();
            }
            else
            {
                Debug.Log("Player Account NOT signed in. Waiting for manual sign-in...");
                PlayerAccountService.Instance.SignedIn += async () =>
                {
                    Debug.Log("Player Account SignedIn event triggered.");
                    try
                    {
                        await AuthenticationService.Instance.SignInWithUnityAsync(PlayerAccountService.Instance.AccessToken);
                        m_ExternalIds = GetExternalIds(AuthenticationService.Instance.PlayerInfo);
                        UpdateUI();
                    }
                    catch (RequestFailedException ex)
                    {
                        Debug.LogException(ex);
                    }
                };
            }
        }

        public async void StartSignInAsync()
        {
            if (PlayerAccountService.Instance.IsSignedIn)
            {
                Debug.Log("starting sign in with unity");
                SignInWithUnity();
                Debug.Log("signed in with unity");
                return;
            }

            try
            {
                await PlayerAccountService.Instance.StartSignInAsync();
                Debug.Log("skibidi start signin async" + AuthenticationService.Instance.PlayerId);
                UpdateUI();

            }
            catch (RequestFailedException ex)
            {
                Debug.LogException(ex);
                SetException(ex);
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
            // try
            // {
            //     await AuthenticationService.Instance.SignInWithUnityAsync(PlayerAccountService.Instance.AccessToken);
            //     m_ExternalIds = GetExternalIds(AuthenticationService.Instance.PlayerInfo);
            //     UpdateUI();
            // }
            // catch (RequestFailedException ex)
            // {
            //     Debug.LogException(ex);
            //     SetException(ex);
            // }
            if (_isSigningIn || AuthenticationService.Instance.IsSignedIn)
                return;

            _isSigningIn = true;

            try
            {
                await AuthenticationService.Instance.SignInWithUnityAsync(PlayerAccountService.Instance.AccessToken);
                Debug.Log("Successfully signed in with Unity.");
                m_ExternalIds = GetExternalIds(AuthenticationService.Instance.PlayerInfo);
                UpdateUI();
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