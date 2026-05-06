using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using TMPro;
using System.Text;

public class PlayerProfileStatus : MonoBehaviour
{
    [SerializeField] TMP_Text m_ProfileStatusText;

    private async void Awake()
    {
        await UnityServices.InitializeAsync();

        AuthenticationService.Instance.SignedIn += OnAuthChanged;
        AuthenticationService.Instance.SignedOut += OnAuthChanged;
        AuthenticationService.Instance.Expired += OnAuthChanged;

        UpdateProfileStatus();
    }

    private void OnEnable()
    {
        UpdateProfileStatus();
    }

    private void OnAuthChanged()
    {
        UpdateProfileStatus();
    }

    private void UpdateProfileStatus()
    {
        if (m_ProfileStatusText == null) return;

        bool signedIn = AuthenticationService.Instance.IsSignedIn;

        const string colorOpen = "<color=#FFEE00>";
        const string colorClose = "</color>";

        var sb2 = new StringBuilder();
        sb2.AppendLine(signedIn
            ? $"{colorOpen}Your journey is safe.{colorClose}"
            : $"{colorOpen}Log in to save your journey.{colorClose}");

        m_ProfileStatusText.text = sb2.ToString();
    }

    private void OnDestroy()
    {
        if (AuthenticationService.Instance != null)
        {
            AuthenticationService.Instance.SignedIn -= OnAuthChanged;
            AuthenticationService.Instance.SignedOut -= OnAuthChanged;
            AuthenticationService.Instance.Expired -= OnAuthChanged;
        }
    }
}
