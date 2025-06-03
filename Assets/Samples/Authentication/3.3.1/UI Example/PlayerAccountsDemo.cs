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

        async void Awake()
        {
            await UnityServices.InitializeAsync();
            PlayerAccountService.Instance.SignedIn +=  SignInWithUnity;
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


            UpdateUI();
        }

        public void OpenAccountPortal()
        {
            Application.OpenURL(PlayerAccountService.Instance.AccountPortalUrl);
        }

        async void SignInWithUnity()
        {
            try
            {
                await AuthenticationService.Instance.SignInWithUnityAsync(PlayerAccountService.Instance.AccessToken);
                m_ExternalIds = GetExternalIds(AuthenticationService.Instance.PlayerInfo);
                UpdateUI();
            }
            catch (RequestFailedException ex)
            {
                Debug.LogException(ex);
                SetException(ex);
            }
        }

        void UpdateUI()
        {
            var statusBuilder = new StringBuilder();

            // statusBuilder.AppendLine($"Player Accounts State: <b>{(PlayerAccountService.Instance.IsSignedIn ? "Signed in" : "Signed out")}</b>");
            // statusBuilder.AppendLine($"Player Accounts Access token: <b>{(string.IsNullOrEmpty(PlayerAccountService.Instance.AccessToken) ? "Missing" : "Exists")}</b>\n");
            // statusBuilder.AppendLine($"Authentication Service State: <b>{(AuthenticationService.Instance.IsSignedIn ? "Signed in" : "Signed out")}</b>");

            if (AuthenticationService.Instance.IsSignedIn)
            {
                m_SignOut.SetActive(true);
                m_SignIn.SetActive(false);
                statusBuilder.AppendLine(GetPlayerInfoText());
                statusBuilder.AppendLine($"Player ID: <b>{AuthenticationService.Instance.PlayerId}</b>");
            }
            else {
                m_SignOut.SetActive(false);
                m_SignIn.SetActive(true);
            }

            m_StatusText.text = statusBuilder.ToString();


            SetException(null);
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
    }
}