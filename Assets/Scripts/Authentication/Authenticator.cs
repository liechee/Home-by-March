using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;
using System;

public class Authenticator : MonoBehaviour
{
    
    async void Start(){
        await UnityServices.InitializeAsync();
    }

    // Prevent multiple concurrent sign-in attempts
    private bool isSigningIn = false;

    public async void SignIn()
    {
        if (isSigningIn)
        {
            Debug.LogWarning("SignIn requested but a sign-in is already in progress.");
            return;
        }

        isSigningIn = true;
        try
        {
            await SignInAnonymously();
        }
        finally
        {
            isSigningIn = false;
        }
    }

    async Task SignInAnonymously()
    {
        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("signed in:  " + AuthenticationService.Instance.PlayerId);
            
            Debug.Log($"[SIGN-IN] SuppressCloudRestore before clear: {PlayerPrefs.GetInt("SuppressCloudRestore", 0)}");
            Debug.Log($"[SIGN-IN] HasLoggedOut before clear: {PlayerPrefs.GetInt("HasLoggedOut", 0)}");

            // Clear logout suppression so cloud/local restores can occur now that user is signed in
            if (PlayerPrefs.HasKey("SuppressCloudRestore"))
            {
                Debug.Log("[SIGN-IN] Clearing SuppressCloudRestore flag");
                PlayerPrefs.DeleteKey("SuppressCloudRestore");
            }
            if (PlayerPrefs.HasKey("SuppressStepQuery"))
            {
                Debug.Log("[SIGN-IN] Clearing SuppressStepQuery flag");
                PlayerPrefs.DeleteKey("SuppressStepQuery");
            }
            // IMPORTANT: Clear HasLoggedOut so local/cloud restores are allowed for the new session
            if (PlayerPrefs.HasKey("HasLoggedOut"))
            {
                Debug.Log("[SIGN-IN] Clearing HasLoggedOut flag for new session");
                PlayerPrefs.DeleteKey("HasLoggedOut");
            }
            PlayerPrefs.Save();
            
            Debug.Log($"[SIGN-IN] SuppressCloudRestore after clear: {PlayerPrefs.GetInt("SuppressCloudRestore", 0)}");

            // Trigger cloud load of step data if the OverallStepCounter exists
            try
            {
                OverallStepCounter stepCounter = FindObjectOfType<OverallStepCounter>();
                if (stepCounter != null)
                {
                    Debug.Log("[SIGN-IN] Found OverallStepCounter - calling LoadStepDataFromCloud()");
                    // Load cloud data (will early-exit if suppression remains)
                    await stepCounter.LoadStepDataFromCloud();
                }
                else
                {
                    Debug.LogWarning("[SIGN-IN] OverallStepCounter NOT FOUND");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Error while requesting cloud load from Authenticator: " + e.Message);
            }
        }
        catch (AuthenticationException e)
        {
            Debug.LogWarning("Sign-in failed or already in progress: " + e.Message);
        }
    }


}