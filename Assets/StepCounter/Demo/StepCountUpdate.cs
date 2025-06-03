using UnityEngine;
using HomeByMarch;

public class StepCountUpdate : MonoBehaviour
{
    public StepCountDemo stepCountDemo;  // Reference to the StepCountDemo script
    public float interval = 1f;  // Time interval in seconds

    void Start()
    {
        // Start calling StepsToday method at the given interval
        InvokeRepeating("CallStepsToday", 0f, interval);
    }

    void CallStepsToday()
    {
        stepCountDemo.StepsToday();
    }
}