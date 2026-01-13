using System;

namespace HomeByMarch
{
    public class StepCountEvents
    {
        // Event for when steps are gained
        public event Action<int> onStepsGained;
        public void StepsGained(int steps)
        {
            onStepsGained?.Invoke(steps);
        }

        // Event for when step count changes
        public event Action<int> onStepCountChange;
        public void StepCountChange(int stepCount)
        {
            onStepCountChange?.Invoke(stepCount);
        }
    }
}
