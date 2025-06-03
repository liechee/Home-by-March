using System;
using TMPro;
using UnityEngine;

public class SeasonalCountDown : MonoBehaviour
{
    public TextMeshProUGUI countdownText; // Reference to the TextMeshProUGUI component
    private DateTime targetDateTime; // The target date and time for the countdown

    void Start()
    {
        // Set the target time to 11:59 PM today
        targetDateTime = DateTime.Today.AddDays(1).AddSeconds(-1);
        
        // Start the countdown immediately
        StartCoroutine(UpdateCountdown());
    }

    System.Collections.IEnumerator UpdateCountdown()
    {
        while (true)
        {
            TimeSpan timeRemaining = targetDateTime - DateTime.Now;

            if (timeRemaining.TotalSeconds <= 0)
            {
                countdownText.text = "Time's up!";
                yield break; // Stop the countdown when the target time is reached
            }
            
            // Format the remaining time as Hours:Minutes:Seconds
            countdownText.text = string.Format("{0:D2}HR LEFT",
                timeRemaining.Hours);
            
            yield return new WaitForSeconds(1); // Wait for 1 second before updating the time again
        }
    }
}
