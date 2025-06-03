using UnityEngine;

namespace Repforge.StepCounterPro
{
public class StepCounterConfig : ScriptableObject
{
    [Header("StepCounter Pro Configuration")]

    [Tooltip("A message that tells the user why the app is requesting access to the device’s motion data. Will by default only write this if not already present in the Info.plist file.")]
    [TextArea]
    public string motionUsageDescription = "The device's motion data is required by the application in order to count steps";

    [Tooltip("Toggle this to override the existing Motion Usage Description. If this setting is off, the NSMotionUsageDescription field will only be overwritten if the field does not exist in the Info.plist file.")]
    public bool overrideMotionUsageDescription = false;
}

}