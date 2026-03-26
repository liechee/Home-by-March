using System;

namespace Repforge.StepCounterPro
{


    public class MockEditorBridge : StepCounterBridge
    {
        public void Enable()
        {

        }

        public bool HasPermission()
        {
            return true;
        }

        public bool IsEnabled()
        {
            return true;
        }

        public bool IsStepCountingAvailable()
        {
            return true;
        }

        public void OpenAppSettings()
        {

        }

        public void QuerySteps(DateTime startDateTime, DateTime endDateTime, Action<int> onQueryCompleted, Action onPermissionDenied)
        {
            onQueryCompleted(UnityEngine.Random.Range(0, 12000));
        }

        public void QueryStepsSince(DateTime startDateTime, Action<int> onQueryCompleted)
        {
            onQueryCompleted(UnityEngine.Random.Range(0, 12000));
        }

        public void RequestPermission(Action onPermissionGranted, Action onPermissionDenied)
        {
            onPermissionGranted.Invoke();
        }
    }
}