using Repforge.StepCounterPro;
using UnityEngine;

public class SCPermissionCanvas : MonoBehaviour
{
    private StepCounterRequest stepCounter;

    private void Start()
    {
        stepCounter = new StepCounterRequest();
    }

    public void OpenSettings()
    {
        stepCounter.OpenAppSettings();
    }
}
