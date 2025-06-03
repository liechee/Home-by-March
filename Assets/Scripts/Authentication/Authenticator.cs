using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

public class Authenticator : MonoBehaviour
{
    
    async void Start(){
        await UnityServices.InitializeAsync();
    }

    public async void SignIn(){
        await SignInAnonymously();
    }

    async Task SignInAnonymously(){
        try{
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        Debug.Log("signed in:  " + AuthenticationService.Instance.PlayerId);
        }
        catch (AuthenticationException e){
            Debug.Log("no sign in ");
                        Debug.Log(AuthenticationService.Instance.PlayerId);
            Debug.Log(e);

        }
    }


}