// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using Unity.Services.Core;
// using Unity.Services.Authentication;
// using System.Threading.Tasks;
// using Unity.Services.Authentication.PlayerAccounts;


// public class UnityAuthenticator : MonoBehaviour{

//     string external_Ids;
//     async void Awake(){
//         await UnityServices.InitializeAsync();
//         PlayerAccountService.Instance.SignedIn +=  SignInWithUnity;
//     }

//     public void SignUp(){
//         SignUpWithUsernamePasswordAsync();
//     }
//     async void SignInWithUnity()
//     {
//         try
//         {
//             await AuthenticationService.Instance.SignInWithUnityAsync(PlayerAccountService.Instance.AccessToken);
//             externalIds = GetExternalIds(AuthenticationService.Instance.PlayerInfo);

//         }
//         catch (RequestFailedException ex)
//         {
//                 Debug.LogException(ex);
//                 SetException(ex);
//         }
//     }
// }