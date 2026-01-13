using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Android;

namespace Repforge.StepCounterPro
{
    public class AndroidStepCounterBridge : StepCounterBridge
    {
        const string pluginName = "com.nopainnogame.stepcounterpro.StepChecker";

        private static string dateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fff";
        private static bool enabled = false;

        static AndroidJavaClass _pluginClass;
        static AndroidJavaObject _pluginInstance;
        static System.Threading.SynchronizationContext syncContext;

        public static AndroidJavaClass PluginClass
        {
            get
            {
                if (_pluginClass == null)
                {
                    _pluginClass = new AndroidJavaClass(pluginName);
                    AndroidJavaClass playerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    AndroidJavaObject activity = playerClass.GetStatic<AndroidJavaObject>("currentActivity");
                    _pluginClass.SetStatic<AndroidJavaObject>("activity", activity);
                }
                return _pluginClass;
            }
        }

        public static AndroidJavaObject PluginInstance
        {
            get
            {
                if (_pluginInstance == null)
                {
                    _pluginInstance = PluginClass.CallStatic<AndroidJavaObject>("getInstance");
                    syncContext = System.Threading.SynchronizationContext.Current;
                }
                return _pluginInstance;
            }
        }

        ///<summary>
        /// The StepCallback object acts internally as a proxy between C# and Java / Kotlin. 
        /// Upon successfully retrieving the steps from the sensor or persistent storage the onSuccess method is called with the step count
        /// This value is used to invoke a step response Action passed from the calling function.
        /// </summary>
        class StepCallback : AndroidJavaProxy
        {
            ///<summary>The action (function) to call upon successfully retrieving steps from native side</summary>
            private System.Action<int> stepResponse;

            public StepCallback(System.Action<int> stepResponseIn) : base(pluginName + "$StepCallback")
            {
                stepResponse = stepResponseIn;
            }

            /// <summary>
            ///  Called from native Java / Kotlin code when steps have successfully been retrieved
            /// </summary>
            /// <param name="stepCount">The step count from the requested time period</param>
            public void onSuccess(int stepCount)
            {
                syncContext.Post(_ => { stepResponse(stepCount); }, null);

            }
        }



        /// <summary>
        /// Subscribes to the background collection of steps on Android devices. On Android, only the steps registered after this point will be recorded and queryable. 
        /// After subscribing, step data is stored for 10 days.
        /// NOTE: On iOS, step data is only stored for 7 days. Querying data beyond 7 days can therefore lead to inconsistent behavior between the platforms.
        /// </summary>
        public void Enable()
        {
            if(!enabled)
            {
                enabled = true;
                PluginInstance.Call("subscribe");
            }
        }

        /// <summary>
        /// Queries the persistent storage for the number of steps recorded between the specified start and end times.
        /// </summary>
        /// <param name="startDateTime">The DateTime representing the start of the query period.</param>
        /// <param name="endDateTime">The DateTime representing the end of the query period.</param>
        /// <param name="onQueryCompleted">The callback action to be invoked when the query completes successfully. Takes the step count as an int parameter.</param>
        public void QuerySteps(DateTime startDateTime, DateTime endDateTime, Action<int> onQueryCompleted, Action onPermissionDenied)
        {
            string startDateTimeString = startDateTime.ToString(dateTimeFormat, CultureInfo.InvariantCulture);
            string endDateTimeString = endDateTime.ToString(dateTimeFormat, CultureInfo.InvariantCulture);

            RequestPermission(() =>
            {
                Enable();
                PluginInstance.Call("getStepsFrom", startDateTimeString, endDateTimeString, new StepCallback(onQueryCompleted));
            },
            () =>
            {
                onPermissionDenied?.Invoke();
            });
        }

        /// <summary>
        /// Queries the persistent storage for the number of steps recorded since the specified start time.
        /// </summary>
        /// <param name="startDateTime">The DateTime representing the start of the query period.</param>
        /// <param name="onQueryCompleted">The callback action to be invoked when the query completes successfully. Takes the step count as an int parameter.</param>
        public void QueryStepsSince(DateTime startDateTime, Action<int> onQueryCompleted)
        {
            string startDateTimeString = startDateTime.ToString(dateTimeFormat, CultureInfo.InvariantCulture);
            PluginInstance.Call("getStepsSince", startDateTimeString, new StepCallback(onQueryCompleted));
        }

        public void RequestPermission(Action onPermissionGranted, Action onPermissionDenied)
        {
            if (!Permission.HasUserAuthorizedPermission("android.permission.ACTIVITY_RECOGNITION"))
            {
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += (_) => onPermissionGranted?.Invoke();
                callbacks.PermissionDenied += (_) => onPermissionDenied?.Invoke();
                Permission.RequestUserPermission("android.permission.ACTIVITY_RECOGNITION", callbacks);
            }
            else
            {
                onPermissionGranted?.Invoke();
            }
        }

        public void OpenAppSettings()
        {
            PluginInstance.Call("openAppSettings");
        }

        public bool HasPermission()
        {
            return Permission.HasUserAuthorizedPermission("android.permission.ACTIVITY_RECOGNITION");
        }

        public bool IsEnabled()
        {
            return PluginInstance.Call<bool>("isSubscribed");
        }

        // TODO: Perhaps check sensor also?
        public bool IsStepCountingAvailable()
        {
            return PluginInstance.Call<bool>("checkGooglePlayServices");
        }
    }
}