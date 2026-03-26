using UnityEngine;

namespace HomeByMarch
{
    public class StepCountManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private int startingSteps = 0;

        public int currentStepCount { get; private set; }

        private void Awake()
        {
            currentStepCount = startingSteps;
        }

        private void OnEnable()
        {
            // Subscribe to steps gained event
            GameEventsManager.instance.stepCountEvents.onStepsGained += StepsGained;
        }

        private void OnDisable()
        {
            // Unsubscribe from steps gained event
            GameEventsManager.instance.stepCountEvents.onStepsGained -= StepsGained;
        }

        private void Start()
        {
            // Trigger step count change event at the start
            GameEventsManager.instance.stepCountEvents.StepCountChange(currentStepCount);
        }

        private void StepsGained(int steps)
        {
            // Update current step count when steps are gained
            currentStepCount += steps;

            // Trigger the step count change event with the updated value
            GameEventsManager.instance.stepCountEvents.StepCountChange(currentStepCount);
        }
    }
}
