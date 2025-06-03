using System.IO;
using UnityEngine;
using TMPro; // Import TextMeshPro namespace

public class MissionProgress : MonoBehaviour
{
    public int Missionoverallsteps; // Total steps required for the mission
    public TMP_Text progress; // TextMeshPro UI Text to display progress
    public GameObject ObjectUnlock; // The object to unlock when the mission is complete

    private string stepJsonFilePath;
    private int MissionoverallStepssaved;

    void Awake()
    {
        // Define the file path for step data
        stepJsonFilePath = Application.persistentDataPath + "/stepData.json";

        // Check if the step data file exists
        if (File.Exists(stepJsonFilePath))
        {
            string stepCountData = File.ReadAllText(stepJsonFilePath);
            StepData data = JsonUtility.FromJson<StepData>(stepCountData);
            MissionoverallStepssaved = data.overallSteps;
        }

        // Assign a default value to Missionoverallsteps if not set
        if (Missionoverallsteps <= 0)
        {
            Debug.LogWarning("Missionoverallsteps not set. Defaulting to 1000 steps.");
            Missionoverallsteps = 1000; // Default value
        }

        // Check if the player is on a new mission
        if (PlayerPrefs.GetInt("missionoverall", 0) != 1)
        {
            SaveLastOverallSteps(Missionoverallsteps);

            // Set PlayerPrefs to indicate mission progress has started
            PlayerPrefs.SetInt("missionoverall", 1);
            PlayerPrefs.Save(); // Save PlayerPrefs changes
        }

        // Update progress and check for object unlock
        UpdateProgress();
    }

    private void SaveLastOverallSteps(int lastOverallSteps)
    {
        StepData data = new StepData { overallSteps = lastOverallSteps };
        string jsonData = JsonUtility.ToJson(data);
        File.WriteAllText(stepJsonFilePath, jsonData);
    }

    private void UpdateProgress()
    {
        int currentProgress = Missionoverallsteps - MissionoverallStepssaved;
        progress.text = $"{currentProgress} / {Missionoverallsteps}";

        if (currentProgress >= Missionoverallsteps)
        {
            ObjectUnlock.SetActive(true);
        }
        else
        {
            ObjectUnlock.SetActive(false);
        }
    }

    [System.Serializable]
    public class StepData
    {
        public int overallSteps;
    }
}
