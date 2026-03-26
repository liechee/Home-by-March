using UnityEngine;

public class CallPermission : MonoBehaviour {


    void Start() {
        RequestPermission();
    }
     async void RequestPermission()
    {
        #if UNITY_EDITOR
            
        #endif
        #if UNITY_ANDROID
            AndroidRuntimePermissions.Permission stepTrackerResult = await AndroidRuntimePermissions.RequestPermissionAsync("android.permission.ACTIVITY_RECOGNITION");
            AndroidRuntimePermissions.Permission fileManagementResult = await AndroidRuntimePermissions.RequestPermissionAsync("android.permission.MANAGE_EXTERNAL_STORAGE");

        #endif
    }
}