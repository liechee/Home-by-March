using System;

namespace Repforge.StepCounterPro
{
    /// <summary>
    /// Represents a request to query step count data and to perform other step counter related actions from the platform specific step counter bridge.
    /// </summary>
    public class StepCounterRequest
{
    private StepCounterBridge bridge;

    private DateTime _startDate;
    private DateTime _endDate;
    private Action<int> _onQuerySuccess;
    private Action<string> _onError;
    private Action _onPermissionDenied;
    private Action _onPermissionGranted;

        /// <summary>
        /// Initializes a new instance of the <see cref="StepCounterRequest"/> class.
        /// </summary>
        public StepCounterRequest()
    {
#if UNITY_EDITOR
        bridge = new MockEditorBridge();
#elif PLATFORM_ANDROID
        bridge = new AndroidStepCounterBridge();
#elif UNITY_IOS
        bridge = new iOSStepCounterBridge();
#endif
    }


        /// <summary>
        /// Sets the start date for the step count query.
        /// </summary>
        /// <remarks>
        /// End date must also be set using <see cref="To(DateTime)"/>
        /// </remarks>
        /// <param name="fromDate">The start date of the query period.</param>
        /// <returns>The current <see cref="StepCounterRequest"/> instance for chaining purposes.</returns>
        public StepCounterRequest From(DateTime fromDate)
    {
        _startDate = fromDate;
        return this;
    }

        /// <summary>
        /// Sets the end date for the step count query.
        /// </summary>
        /// <remarks>
        /// Start date must also be set using <see cref="From(DateTime)"/>
        /// </remarks>
        /// <param name="toDate">The end date of the query period.</param>
        /// <returns>The current <see cref="StepCounterRequest"/> instance for method chaining</returns>
        public StepCounterRequest To(DateTime toDate)
    {
        _endDate = toDate;
        return this;
    }

        /// <summary>
        /// Sets the start and end dates for the step count query.
        /// </summary>
        /// <param name="fromDate">The start date of the query period.</param>
        /// <param name="toDate">The end date of the query period.</param>
        /// <returns>The current <see cref="StepCounterRequest"/> instance for method chaining</returns>
        public StepCounterRequest Between(DateTime fromDate, DateTime toDate)
    {
        _startDate = fromDate;
        _endDate = toDate;
        return this;
    }

        /// <summary>
        /// Sets the start date and uses the current date as the end date for the step count query.
        /// </summary>
        /// <param name="fromDate">The start date of the query period.</param>
        /// <returns>The current <see cref="StepCounterRequest"/> instance for method chaining.</returns>
        public StepCounterRequest Since(DateTime fromDate)
    {
        _startDate = fromDate;
        _endDate = DateTime.Now;
        return this;
    }

        /// <summary>
        /// Sets the callback to be invoked when the step count query is successful.
        /// </summary>
        /// <param name="onQuerySuccess">The callback action that receives the step count as an integer.</param>
        /// <returns>The current <see cref="StepCounterRequest"/> instance.</returns>
        public StepCounterRequest OnQuerySuccess(Action<int> onQuerySuccess)
    {
        _onQuerySuccess = onQuerySuccess;
        return this;
    }
        /// <summary>
        /// Sets the callback to be invoked when an error occurs during the step count query.
        /// </summary>
        /// <param name="onError">The callback action that receives the error message as a string.</param>
        /// <returns>The current <see cref="StepCounterRequest"/> instance.</returns>
        public StepCounterRequest OnError(Action<string> onError)
    {
        _onError = onError;
        return this;
    }
        /// <summary>
        /// Sets the callback to be invoked when the user denies, or has permanently denied, the required permission for step counting.
        /// </summary>
        /// <remarks>This callback will be called if permission is denied after invoking <see cref="Execute"/> or <see cref="RequestPermission"/></remarks>
        /// <param name="onPermissionDenied">The callback action to be invoked when permission is denied.</param>
        /// <returns>The current <see cref="StepCounterRequest"/> instance.</returns>
        public StepCounterRequest OnPermissionDenied(Action onPermissionDenied)
    {
        _onPermissionDenied = onPermissionDenied;
        return this;
    }
        /// <summary>
        /// Sets the callback to be invoked when the user grants the required permission for step counting.
        /// </summary>
        /// <remarks>
        /// Contrary to <see cref="OnPermissionDenied(Action)"/>, the callback will only be invoked if permission is granted after invoking <see cref="RequestPermission"/>
        /// </remarks>
        /// <param name="onPermissionGranted">The callback action to be invoked when permission is granted.</param>
        /// <returns>The current <see cref="StepCounterRequest"/> instance.</returns>
        public StepCounterRequest OnPermissionGranted(Action onPermissionGranted)
    {
        _onPermissionGranted = onPermissionGranted;
        return this;
    }

        /// <summary>
        /// Executes the step count query with the provided parameters and callbacks.
        /// </summary>
        /// <remarks>
        /// If <see cref="OnQuerySuccess(Action{int})"/> is not set or if the start and end dates are invalid, the query will not be executed and <see cref="OnError(Action{string})"/> will be invoked with an error message.
        /// </remarks>
        public void Execute()
    {
        if (!ValidateDates())
            return;

        if(_onQuerySuccess == null)
        {
            _onError.Invoke("Invalid query: No result handler set");
            return;
        }

        bridge.QuerySteps(_startDate, _endDate, _onQuerySuccess, _onPermissionDenied);
    }

    private bool ValidateDates()
    {
        if(_startDate == null)
        {
            _onError.Invoke("Invalid date: Start date is missing!");
            return false;
        }
        if (_endDate == null)
        {
            _onError.Invoke("Invalid date: End date is missing!");
            return false;
        }
        if (_endDate < _startDate)
        {
            _onError.Invoke("Invalid date: End date can not be earlier than start date");
            return false;
        }
        return true;
    }

        /// <summary>
        /// Opens the app settings to allow the user to manually grant permissions.
        /// </summary>
        /// <remarks>
        /// On many devices, app will restart if permissions are removed while running.
        /// </remarks>
        public void OpenAppSettings()
    {
        bridge.OpenAppSettings();
    }

        /// <summary>
        /// Checks whether the necessary permission for step counting has been granted.
        /// </summary>
        /// <remarks>
        /// If used before a query, consider using the <see cref="OnPermissionDenied(Action)"/> callback invoked by <see cref="Execute"/> instead.
        /// </remarks>
        /// <returns><c>true</c> if the permission is granted; otherwise, <c>false</c>.</returns>
        public bool HasPermission()
    {
        return bridge.HasPermission();
    }

        /// <summary>
        /// Enables the step counter, including the background collection of steps.
        /// </summary>
        /// <remarks>
        /// This method is called automatically when executing a query. However, step data from before first calling Enable is not queryable on Android devices, so consider calling Enable early.
        /// </remarks>
        public void Enable()
        {
            bridge.Enable();
        }

        /// <summary>
        /// Checks whether the step counter functionality is enabled on the device., including the background collection of steps.
        /// </summary>
        /// <returns><c>true</c> if the step counter is enabled; otherwise, <c>false</c>.</returns>
        public bool IsEnabled()
    {
        return bridge.IsEnabled();
    }

        /// <summary>
        /// Checks whether the step counter feature is available on the current device.
        /// </summary>
        /// <returns><c>true</c> if the step counter is available; otherwise, <c>false</c>.</returns>
        public bool isStepCounterAvailible()
    {
        return bridge.IsStepCountingAvailable();
    }

    /// <summary>
    /// Requests the nessesary permissions in order to use the step counter. Depending on the user's response, either the <paramref name="onPermissionGranted"/> or <paramref name="onPermissionDenied"/> callback will be triggered. 
    /// </summary>
    /// <remarks>
    /// If already granted, <paramref name="onPermissionGranted"/> will immediatly be invoked and the user will not be prompted. 
    /// If previously denied on iOS, or user pressing 'Do not ask again' on Android, the user will not be prompted and <paramref name="onPermissionDenied"/> will be invoked. 
    /// If the step counter is crucial for your application to function correctly, clearly communicate to the user why they need to grant the specific permission and use <see cref="OpenAppSettings"/>. to redirect the user to the app's settings.
    /// </remarks>
    /// <param name="onPermissionGranted">The callback action to be invoked when the user grants or has already granted permission to use the step counter</param>
    /// <param name="onPermissionDenied">The callback action to be invoked when the user denies or has already denied permission to use the step counter</param>
    public void RequestPermission()
    {
        bridge.RequestPermission(_onPermissionGranted, _onPermissionDenied);
    }
}
}