// using UnityEngine;

// namespace HomeByMarch{

// public class StepCounterListener : MonoBehaviour
// {
//     [SerializeField] FloatEventListener stepCountListener;

//     void OnEnable()
//     {
//         if (stepCountListener != null)
//             stepCountListener.eventChannel.Register(stepCountListener);
//     }

//     void OnDisable()
//     {
//         if (stepCountListener != null)
//             stepCountListener.eventChannel.Deregister(stepCountListener);
//     }

//     void OnStepCountChanged(float stepCount)
//     {
//         Debug.Log("Step Count: " + stepCount);
//     }
// }
// }