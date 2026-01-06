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

            // Clear logout suppression so cloud/local restores can occur now that user is signed in
            if (PlayerPrefs.HasKey("SuppressCloudRestore"))
            {
                PlayerPrefs.DeleteKey("SuppressCloudRestore");
            }
            if (PlayerPrefs.HasKey("SuppressStepQuery"))
            {
                PlayerPrefs.DeleteKey("SuppressStepQuery");
            }
            PlayerPrefs.Save();

            // Trigger cloud load of step data if the OverallStepCounter exists
            try
            {
                OverallStepCounter stepCounter = FindObjectOfType<OverallStepCounter>();
                if (stepCounter != null)
                {
                    // Load cloud data (will early-exit if suppression remains)
                    await stepCounter.LoadStepDataFromCloud();
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