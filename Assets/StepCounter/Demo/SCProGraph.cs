using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Repforge.StepCounterPro
{

    public class SCProGraph : MonoBehaviour
    {
        public GraphInterval interval;

        [Header("Columns")]
        public GameObject columnPrefab;
        public Transform columnParent;
        public int numberOfColumns;
        public float maxColumnHeight = 400;

        [Header("Horizontal label")]
        public GameObject dayTextPrefab;
        public Transform daysParent;

        [Header("Vertical labels")]
        public int numberOfLabels = 2;
        public GameObject labelPrefab;
        public Transform labelParent;

        private RectTransform[] columns;
        private int[] stepCounts;

        [Header("Other")]
        public GameObject permissionCanvas;
        public Button reloadGraphButton;

        private void Start()
        {
            RequestPermissionAndOpen();
            reloadGraphButton.onClick.AddListener(RequestPermissionAndOpen);
        }

        private void OnDestroy()
        {
            reloadGraphButton.onClick.RemoveListener(RequestPermissionAndOpen);
        }

        public void RequestPermissionAndOpen()
        {
            StepCounterRequest permissionRequest = new StepCounterRequest();
            permissionRequest
                .OnPermissionGranted(InitializeGraph)
                .OnPermissionDenied(() => { 
                    permissionCanvas.SetActive(true);
                    reloadGraphButton.gameObject.SetActive(true);
                })
                .RequestPermission();
        }

        private void InitializeGraph()
        {
            // Initialize arrays
            columns = new RectTransform[numberOfColumns];
            stepCounts = new int[numberOfColumns];
            int largestValue = 0;

            GameObject current;
            int callbackCount = 0;
            for (int i = 0; i < numberOfColumns; i++)
            {
                current = Instantiate(columnPrefab, columnParent);
                columns[i] = current.GetComponent<RectTransform>(); ;

                // Set up date intervals
                DateTime fromTime;
                DateTime toTime;
                if (interval == GraphInterval.Daily)
                {
                    if (i == numberOfColumns - 1)
                    {
                        fromTime = DateTime.Today;
                        toTime = DateTime.Now;
                    }
                    else
                    {
                        fromTime = DateTime.Today.AddDays(-((numberOfColumns - 1) - i));
                        toTime = DateTime.Today.AddDays(-((numberOfColumns - 1) - (i + 1)));
                    }
                }
                else
                {
                    fromTime = DateTime.Today.AddHours(-((numberOfColumns - 1) - i));
                    toTime = DateTime.Today.AddHours(-((numberOfColumns - 1) - (i + 1)));
                }

                GameObject go = Instantiate(dayTextPrefab, daysParent);
                go.GetComponent<TMP_Text>().text = fromTime.ToString("ddd");

                // Query Steps
                // NOTE: Making local variable for "i" since it will likely change before callback.
                int currentIndex = i;

                StepCounterRequest query = new StepCounterRequest();
                query
                .Between(fromTime, toTime)
                .OnQuerySuccess((value) => SetValues(value, currentIndex, ref largestValue, ref callbackCount))
                .OnError((message) => Debug.LogError(message))
                .OnPermissionDenied(() => Debug.LogWarning("Handle permissions here"))
                .Execute();
            }
        }

        private void SetValues(int value, int currentIndex, ref int largestValue, ref int callbackCount)
        {
            if (value > largestValue)
                largestValue = value;
            callbackCount++;
            stepCounts[currentIndex] = value;
            if (callbackCount == numberOfColumns)
                SetScaleAndNormalize(largestValue);
        }

        private void SetScaleAndNormalize(int largestValue)
        {
            // Round up to nearest 1000
            int newLargestValue = ((largestValue + 1000) - largestValue % 1000);
            for (int i = 0; i < numberOfColumns; i++)
            {
                float scaleFactor = (float)stepCounts[i] / (float)newLargestValue;
                columns[i].SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, maxColumnHeight * scaleFactor);
            }
            GameObject current;
            int intervals = newLargestValue / numberOfLabels;
            for(int j = numberOfLabels; j >= 0; j--)
            {
                current = Instantiate(labelPrefab, labelParent);
                current.GetComponent<TMP_Text>().text = (intervals * j).ToString();
            }
        }
    }



    public enum GraphInterval
    {
        Daily,
        Hourly,
    }
}