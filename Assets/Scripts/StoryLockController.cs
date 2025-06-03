using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HomeByMarch;
using TMPro;
using ExitGames.Client.Photon.StructWrapping;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class StoryLockController : MonoBehaviour
{
    public Image images;
    public TMP_Text titleText;
    public TMP_Text bodyText;

    int overallSteps;

    [System.Serializable]
    public class StoryLock
    {
        public GameObject lockObject;
        public int requiredSteps;
        public bool isUnlocked = false;
        private string stepCountData;
    }

    public StoryLock[] storyLocks;
    private Sprite newImage;

    private string stepJsonFilePath;
    private string stepCountData;

    void Awake()
    {
        stepJsonFilePath = Application.persistentDataPath + "/stepData.json";

        // Check if the step data file exists before trying to read it
        if (File.Exists(stepJsonFilePath))
        {
            stepCountData = File.ReadAllText(stepJsonFilePath);
            StepData data = JsonUtility.FromJson<StepData>(stepCountData);
            overallSteps = data.overallSteps;
        }
        else
        {
            Debug.LogWarning("Step data file not found. Creating a new file with default data.");
            File.WriteAllText(stepJsonFilePath, JsonUtility.ToJson(new StepData()));
            stepCountData = File.ReadAllText(stepJsonFilePath);
        }

        // Load previous story completion statuses from PlayerPrefs
        LoadStoryCompletionStatuses();
    }

    private void LoadStoryCompletionStatuses()
    {
        for (int i = 0; i < storyLocks.Length; i++)
        {
            // Load the completion status of each story from PlayerPrefs
            bool isCompleted = PlayerPrefs.GetInt("StoryCompleted_" + i, 0) == 1;
            storyLocks[i].isUnlocked = isCompleted;

            // Log the completion status for debugging
            Debug.Log($"Story {i} completion status loaded from PlayerPrefs: {isCompleted}");
        }
    }
    private void Start()
    {
        Debug.Log(overallSteps);
        newImage = LoadSprite("stories/unknown");

        foreach (StoryLock storyLock in storyLocks)
        {
            if (storyLock.lockObject != null)
            {
                storyLock.lockObject.SetActive(true);
            }
        }

        Debug.Log("StoryLockController started with " + overallSteps + " steps.");
    }

    void Update()
    {
        // Check if the "Q" key is pressed
        // if (Input.GetKeyDown(KeyCode.Q))
        // {
        //     // Set the completion status for Story 0 to true
        //     SetStoryCompletionStatus(0, true);

        //     // Optionally, unlock the story lock if it's not already unlocked
        //     storyLocks[0].isUnlocked = true;

        //     // Log the change
        //     Debug.Log("Story 0 completion status set to true.");
        // }

        // The rest of your Update logic
        for (int i = 0; i < storyLocks.Length; i++)
        {
            // Check if the story can be unlocked
            if (!storyLocks[i].isUnlocked && overallSteps >= storyLocks[i].requiredSteps && IsPreviousStoryCompleted(storyLocks[i]))
            {
                UnlockStoryLock(storyLocks[i]); // Unlock the story lock
                UpdatePanelWithStoryInfo(storyLocks[i]); // Update the UI with the story info
            }
            else if (storyLocks[i].lockObject != null && !storyLocks[i].isUnlocked)
            {
                // Keep the lock object active if the story is still locked
                storyLocks[i].lockObject.SetActive(true);
            }
            else if (storyLocks[i].lockObject != null && storyLocks[i].isUnlocked)
            {
                // Hide the lock object if the story is unlocked
                storyLocks[i].lockObject.SetActive(false);
            }
        }

        PlayerPrefs.Save();
    }

    private bool IsPreviousStoryCompleted(StoryLock storyLock)
    {
        int index = System.Array.IndexOf(storyLocks, storyLock);
        if (index > 0) // Ensure there is a previous story
        {
            return PlayerPrefs.GetInt("StoryCompleted_" + (index - 1), 0) == 1;
        }
        return true; // No previous story for the first story
    }

    // Unlock a specific story lock
    private void UnlockStoryLock(StoryLock storyLock)
    {
        storyLock.isUnlocked = true;
        if (storyLock.lockObject != null)
        {
            storyLock.lockObject.SetActive(false);
        }
    }

    public void OnPreviousStoryComplete(int storyLockIndex)
    {
        if (storyLockIndex >= 0 && storyLockIndex < storyLocks.Length)
        {
            SetStoryCompletionStatus(storyLockIndex, true);
            storyLocks[storyLockIndex].isUnlocked = true;
        }
    }

    public void SetStoryCompletionStatus(int index, bool isComplete)
    {
        PlayerPrefs.SetInt("StoryCompleted_" + index, isComplete ? 1 : 0);
        PlayerPrefs.Save();
    }

    private Sprite LoadSprite(string path)
    {
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite == null)
        {
            Debug.LogError($"Failed to load sprite at path: {path}");
        }
        return sprite;
    }

    // Array to hold the titles for the stories
    private string[] newTMPTitles = new string[]
    {
        // The Beginning - "Compass"
        "INERTIA",
        "POT OF KNOWLEDGE",
        "THE FIRST STEP",

        // A THOUSAND MILES
        "AT EACH OTHER’S THROATS",
        "UNCROSSABLE WALL",
        "DREAM OF THE CELESTIAL CHAMBER",

        // MARCH
        "A FUTURE UNCERTAIN",
        "A PAST ONE CANNOT RETURN TO",
        "WHEN ALL HOPE IS LOST"
    };

    public void OnButtonClick(int index)
    {
        images.sprite = newImage;
        if (index >= 0 && index < storyLocks.Length)
        {
            StoryLock storyLock = storyLocks[index];

            if (!storyLock.isUnlocked) // Check if the story is still locked
            {
                string conditions = "To unlock this story:\n\n";
                conditions += $"- Required Steps: {storyLock.requiredSteps}\n";

                // Add the title of the previous story that must be completed
                if (index > 0) // Ensure there is a previous story
                {
                    conditions += $"- Complete \"{newTMPTitles[index - 1]}\" first\n";
                }

                // Set the title and body text to display the conditions
                titleText.text = newTMPTitles[index];
                bodyText.text = conditions;

                // Log the conditions for debugging
                Debug.Log($"Conditions for unlocking {newTMPTitles[index]}:\n{conditions}");
            }
            else
            {
                Debug.Log($"Story {newTMPTitles[index]} is already unlocked.");
            }
        }
        else
        {
            Debug.LogWarning($"Invalid index {index}. No story lock found.");
        }

        // Removed the call to CanUnlock here as it's unnecessary. 
        // Unlock and show info based on the current story lock logic.
    }

    // Method to update the panel with the story info when unlocked
    private void UpdatePanelWithStoryInfo(StoryLock storyLock)
    {
        // Find the index of the unlocked story lock
        int index = System.Array.IndexOf(storyLocks, storyLock);

        if (index >= 0 && index < newTMPTitles.Length)
        {
            // Set the title and body text to indicate the story is unlocked
            titleText.text = newTMPTitles[index];
            bodyText.text = "This story is now unlocked!";
        }
        else
        {
            Debug.LogWarning($"Invalid index {index}. No title available for unlocked story.");
        }
    }

    // Editor-only logic to auto-unlock story locks in the editor
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        // Simulate unlocking the story locks in the editor based on the steps
        foreach (StoryLock storyLock in storyLocks)
        {
            if (!storyLock.isUnlocked && overallSteps >= storyLock.requiredSteps && IsPreviousStoryCompleted(storyLock))
            {
                UnlockStoryLock(storyLock);
                UpdatePanelWithStoryInfo(storyLock); // Update panel info when unlocked
            }
        }
    }
#endif
}
