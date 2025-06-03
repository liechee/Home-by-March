using System;
namespace Repforge.StepCounterPro
{
    public interface StepCounterBridge
    {
        public void Enable();
        public void QuerySteps(DateTime startDateTime, DateTime endDateTime, Action<int> onQueryCompleted, Action onPermissionDenied);
        public void QueryStepsSince(DateTime startDateTime, Action<int> onQueryCompleted);
        public void RequestPermission(Action onPermissionGranted, Action onPermissionDenied);
        public void OpenAppSettings();
        public bool HasPermission();
        public bool IsEnabled();
        public bool IsStepCountingAvailable();
    }
}