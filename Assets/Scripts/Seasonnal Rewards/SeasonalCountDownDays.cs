using System;
using TMPro;
using UnityEngine;

public class SeasonalDaysCountDown : MonoBehaviour
{
    public TextMeshProUGUI countdownText; // Reference to the TextMeshProUGUI component
    public DateTime targetDate; // The target date for the countdown

    public GameObject seasonpanel;

    void Start()
    {
        // Set the target date to a specific date (e.g., December 31, 2023)
        targetDate = new DateTime(2025, 02, 06);

        // Start the countdown immediately
        StartCoroutine(UpdateCountdown());
    }

    System.Collections.IEnumerator UpdateCountdown()
    {
        while (true)
        {
            TimeSpan timeRemaining = targetDate - DateTime.Now;

            if (timeRemaining.TotalDays <= 0)
            {
                countdownText.text = "0 Days Left";
                seasonpanel.SetActive(false);
                yield break; // Stop the countdown when the target date is reached
            }

            // Format the remaining time as Days
            countdownText.text = string.Format("{0} Days Left", Mathf.CeilToInt((float)timeRemaining.TotalDays));

            // Wait for 1 second and then loop to avoid time drift
            for (int i = 0; i < 86400; i++)
            {
                yield return new WaitForSeconds(1);
            }
        }
    }
}