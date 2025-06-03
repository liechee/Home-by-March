using Repforge.StepCounterPro;
using System;
using TMPro;
using UnityEngine;
using System.IO;
using UnityEngine.Android;

namespace HomeByMarch
{
    [Serializable]
    public class StepData
    {
        public int numberOfSteps; // Steps for today
        public int dailySteps;    // Daily steps count
        public int overallSteps;  // Overall steps count
        public string lastSavedDate; // Store the last saved date to check if a new day has started

        public int lastSavedStepCount; // Store the last saved step count to check if a new day has started
        public int lastSaveOverallSteps;
    }

    public class StepCountDemo : MonoBehaviour
    {
        public TMP_Text text, debugText;
        public Canvas permissionCanvas;
        public int dailyStepCount;
        private int overallStepCount;
        private string stepJsonFilePath;
        private int previousDailyStepCount;  // Track previous day's steps

        void Start()
        {
            InitializeStepCounter();
            LoadStepData();  // Load saved step data
            RequestPermission();  // Request permissions for step counting

            StepCounterRequest permissionRequest = new StepCounterRequest();
            permissionRequest
                .OnPermissionGranted(OnPermissionGranted)
                .OnPermissionDenied(OnPermissionDenied)
                .RequestPermission();
        }

        private void InitializeStepCounter()
        {
            stepJsonFilePath = Application.persistentDataPath + "/stepData.json";
            debugText.text = stepJsonFilePath;
        }

        private void OnPermissionGranted()
        {
            // Fetch today's steps after permission is granted
            StepsToday();
        }

        private void OnPermissionDenied()
        {
            permissionCanvas.gameObject.SetActive(true);
        }

        private void LoadStepData()
        {
            if (File.Exists(stepJsonFilePath))
            {
                string jsonString = File.ReadAllText(stepJsonFilePath);
                StepData data = JsonUtility.FromJson<StepData>(jsonString);

                if (data.lastSavedDate != DateTime.Today.ToString("yyyy-MM-dd"))
                {
                    dailyStepCount = 0;  // Reset daily steps if it's a new day
                    previousDailyStepCount = data.dailySteps;  // Store the last recorded daily steps
                }
                else
                {
                    dailyStepCount = data.dailySteps;  // Load today's steps
                    previousDailyStepCount = data.lastSavedStepCount;  // Store last recorded daily steps for comparison
                }

                overallStepCount = data.overallSteps;  // Load the overall step count
            }
            else
            {
                overallStepCount = 0;
                dailyStepCount = 0;
                previousDailyStepCount = 0;
            }
        }

        private void SaveStepData()
        {
            StepData data = new StepData
            {
                numberOfSteps = dailyStepCount,
                dailySteps = dailyStepCount,
                overallSteps = overallStepCount,
                lastSavedDate = DateTime.Today.ToString("yyyy-MM-dd"),
                lastSavedStepCount = dailyStepCount  // Save the last recorded daily step count
            };

            string stepCountString = JsonUtility.ToJson(data);
            File.WriteAllText(stepJsonFilePath, stepCountString);
            Debug.Log("Step data saved: " + stepCountString);
        }

        public void StepsToday()
        {
            StepCounterRequest request = new StepCounterRequest();
            request
                .Since(DateTime.Today)
                .OnQuerySuccess((value) =>
                {
                    dailyStepCount = value;  // Update daily steps
                    UpdateOverallSteps();  // Update overall steps by adding the difference
                    text.text = value.ToString();  // Update UI

                    SaveStepData();  // Save data after updating steps
                })
                .OnPermissionDenied(() => permissionCanvas.gameObject.SetActive(true))
                .Execute();
        }

        private void UpdateOverallSteps()
        {
            Debug.Log("Previous Daily Steps: " + previousDailyStepCount);

            // Calculate the difference between today's steps and the last recorded steps
            int stepsDifference = dailyStepCount - previousDailyStepCount;
            Debug.Log("stepsDifference: " + stepsDifference);

            if (stepsDifference > 0)  // Only update if there's an increase in steps
            {

                overallStepCount += stepsDifference;  // Add the difference to the overall step count
                previousDailyStepCount = dailyStepCount;  // Update previous day's step count
                Debug.Log("Updated Overall Steps: " + overallStepCount); // Log the updated overall step count
            }
        }

        async void RequestPermission()
        {
#if UNITY_ANDROID
        AndroidRuntimePermissions.Permission fileManagementResult = await AndroidRuntimePermissions.RequestPermissionAsync("android.permission.MANAGE_EXTERNAL_STORAGE");
#endif
        }
    }


}
