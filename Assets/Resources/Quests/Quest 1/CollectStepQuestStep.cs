using System.Collections;
using UnityEngine;

namespace HomeByMarch
{

    public class CollectStepsQuestStep : QuestStep
    {
        private int stepsCollected = 0;
        private int stepsToComplete = 1000; // Example: 1000 steps needed to complete the quest
        private void Awake()
        {
            Debug.Log("Awake called.");
        }

        private void Start()
        {
            Debug.Log("Start called.");
            Debug.Log("CollectStepsQuestStep started. Current steps collected: " + stepsCollected);
            UpdateState();
        }

        private void OnEnable()
        {
            // Subscribe to the steps gained event from the GameEventsManager
            Debug.Log("CollectStepsQuestStep enabled. Subscribing to step events.");
            GameEventsManager.instance.stepCountEvents.onStepsGained += StepsGained;
        }

        private void OnDisable()
        {
            // Unsubscribe from the steps gained event
            Debug.Log("CollectStepsQuestStep disabled. Unsubscribing from step events.");
            GameEventsManager.instance.stepCountEvents.onStepsGained -= StepsGained;
        }

        private void StepsGained(int steps)
        {
            Debug.Log("StepsGained event triggered. Gained steps: " + steps);

            if (stepsCollected < stepsToComplete)
            {
                // Add the gained steps to the total steps collected
                stepsCollected += steps;
                Debug.Log("Steps collected updated: " + stepsCollected + " / " + stepsToComplete);
                UpdateState();
            }

            // Check if the step requirement is met or exceeded
            if (stepsCollected >= stepsToComplete)
            {
                Debug.Log("Steps collected completed! Finishing the quest step.");
                CompleteQuest(); // Mark the quest step as completed
            }
        }

        private void UpdateState()
        {
            string state = stepsCollected.ToString();
            string status = "Collected " + stepsCollected + " / " + stepsToComplete + " steps.";

            // Log the status for debugging
            Debug.Log("Quest step updated. Status: " + status);
            ChangeState(state, status); // Update the quest step state with collected steps
        }

        protected override void SetQuestStepState(string state)
        {
            // Parse the saved state to restore the collected steps count
            this.stepsCollected = System.Int32.Parse(state);

            // Log restored state for debugging
            Debug.Log("Restoring quest step state. Steps collected: " + stepsCollected);
            UpdateState();
        }
    }

}