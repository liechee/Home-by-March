using System.Collections;
using System.Collections.Generic;
using Unity.Notifications.Android;
using UnityEngine;

public class NotificationController : MonoBehaviour
{
    [SerializeField] AndroidNotifications androidNotifications;

    private void Start()
    {
        // Request permission and register the notification channel for Android
        androidNotifications.RequestAuthorization();
        androidNotifications.RegisterNotificationChannel();
    }

    private void OnApplicationFocus(bool focus)
    {
        if (focus == false)
        {
            // Cancel all notifications and send a new one
            AndroidNotificationCenter.CancelAllNotifications();
            androidNotifications.SendNotification(
                "Daily Notification", 
                "Don't forget to log your steps today and keep exploring the world of Home by March!", 
                2 // Notification will fire after 5 minutes
            );
        }
    }
}
