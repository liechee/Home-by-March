#if UNITY_IOS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;
namespace Repforge.StepCounterPro
{
public class iOSStepCounterBridge : StepCounterBridge
{
    private const string dateFormat = "yyyy-MM-ddTHH:mm:ss.fff";

    // Callback handling. Should in theory support concurrent queries with different callbacks
    private static Dictionary<int, Action<int>> callbackDictionary = new Dictionary<int, Action<int>>();
    private static int currentId = 0;

    public static int RegisterCallback(Action<int> callback)
    {
        currentId++;
        callbackDictionary.Add(currentId, callback);
        return currentId;
    }


    [DllImport("__Internal")]
    private static extern void _QuerySteps(string fromTime, string toTime, ref StepCounterCallbacks callbacks, int callbackID);

    //TODO: Rename to less common name to avoid conflicts with other plugins
    [DllImport("__Internal")]
    private static extern void _EnableStepCounter();

    [DllImport("__Internal")]
    private static extern void _RequestStepCounterPermission(ref StepCounterPermissionCallbacks callbacks);

    [DllImport("__Internal")]
    private static extern bool _IsStepCounterAuthorized();

    [DllImport("__Internal")]
    private static extern bool _IsStepCounterSubscribed();

    [DllImport("__Internal")]
    private static extern bool _IsStepCountingAvailable();

    [DllImport("__Internal")]
    private static extern void _RedirectToStepCounterSettings();

    [StructLayout(LayoutKind.Sequential)]
    private struct StepCounterCallbacks
    {
        internal OnDataReceivedDelegate onData;
        internal OnPermissionResultDelegate onPermissionResult;
    }

    internal delegate void OnDataReceivedDelegate(int callbackID, int numberOfSteps);

    [MonoPInvokeCallback(typeof(OnDataReceivedDelegate))]
    private static void OnDataReceived(int callbackID, int numberOfSteps)
    {
            Debug.Log("Number of steps: " + numberOfSteps);
        try
        {
            if (callbackDictionary.TryGetValue(callbackID, out Action<int> callback))
            {
                callback.Invoke(numberOfSteps);
                callbackDictionary.Remove(callbackID);
            }
        } catch
        {
            Debug.Log("Something went wrong");
        }
        
    }

    private static List<(Action, Action)> permissionCallbacks = new List<(Action, Action)>();

    [StructLayout(LayoutKind.Sequential)]
    private struct StepCounterPermissionCallbacks
    {
        internal OnPermissionResultDelegate onPermissionResult;
    }

    internal delegate void OnPermissionResultDelegate(bool granted);

    [MonoPInvokeCallback(typeof(OnPermissionResultDelegate))]
    private static void OnPermissionResult(bool granted)
    {
        Debug.Log("Permission granted = " + granted);
        if (granted) {
            foreach((Action, Action) actions in permissionCallbacks) {
                actions.Item1.Invoke();
            }
        } else
        {
            foreach ((Action, Action) actions in permissionCallbacks)
            {
                actions.Item2.Invoke();
            }
        }
        permissionCallbacks.Clear();
    }

    public void QuerySteps(DateTime startDateTime, DateTime endDateTime, Action<int> onQueryCompleted, Action onPermissionDenied)
    {
        string startDateTimeString = startDateTime.ToString(dateFormat, CultureInfo.InvariantCulture);
        string endDateTimeString = endDateTime.ToString(dateFormat, CultureInfo.InvariantCulture);

        var callbacks = new StepCounterCallbacks();
        callbacks.onData = OnDataReceived;
        callbacks.onPermissionResult = OnPermissionResult;

        int id = RegisterCallback(onQueryCompleted);

        _QuerySteps(startDateTimeString, endDateTimeString, ref callbacks, id);
    }

    public void QueryStepsSince(DateTime startDateTime, Action<int> onQueryCompleted)
    {
        string startDateTimeString = startDateTime.ToString(dateFormat, CultureInfo.InvariantCulture);
        string endDateTimeString = DateTime.Now.ToString(dateFormat, CultureInfo.InvariantCulture);
        var callbacks = new StepCounterCallbacks();
        callbacks.onData = OnDataReceived;
            callbacks.onPermissionResult = OnPermissionResult;

            int id = RegisterCallback(onQueryCompleted);

        _QuerySteps(startDateTimeString, endDateTimeString, ref callbacks, id);
    }

    public void Enable()
    {
        _EnableStepCounter();
    }

    public void RequestPermission(Action onPermissionGranted, Action onPermissionDenied)
    {
        var callbacks = new StepCounterPermissionCallbacks();
        callbacks.onPermissionResult = OnPermissionResult;
        permissionCallbacks.Add((onPermissionGranted, onPermissionDenied));
        _RequestStepCounterPermission(ref callbacks);
    }

    public void OpenAppSettings()
    {
        _RedirectToStepCounterSettings();
    }

    public bool HasPermission()
    {
        return _IsStepCounterAuthorized();
    }

    public bool IsEnabled()
    {
        return _IsStepCounterSubscribed();
    }

    public bool IsStepCountingAvailable()
    {
        return _IsStepCountingAvailable();
    }
}
}
#endif