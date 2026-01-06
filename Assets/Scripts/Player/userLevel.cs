using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HomeByMarch;
using System;
public class UserLevel : MonoBehaviour
{
    [SerializeField] public TMP_Text levelText;
    [SerializeField] public TMP_Text levelTextOutside;
    public TMP_Text currentStepCountText;
    public TMP_Text currentStepCountTextOutside;
    public TMP_Text totalStepsForNextLevelText;
    public TMP_Text remainingStepsForNextLevelText;
    public TMP_Text overallStepCountText;
    public TMP_Text percentageText;

    [Header("UI stuff")]
    public Image experienceBarImage;

    public StepCountDemo stepCountDemo;

    public int totalStepsForNextLevel;
    public int currentStepCount;
    public int dailyStepCount;
    public int overallStepCount;
    public int remainingStepsForNextLevel;

    [Header("User Info")]
    public TMP_Text userNameText;
    public PlayerData playerData;

    private string stepJsonFilePath;
    private string stepCountData;
    private OverallStepCounter stepCounter;
    private bool cloudStepDataLoaded = false;
    private int lastRecordedSteps = 0; // steps at cloud load
    private int addedSteps = 0; // steps added since then
    private int lastUIUpdatedStepCount = -1;
    private const string DailyStepOffsetKey = "DailyStepOffset";
    private const string OverallStepOffsetKey = "OverallStepOffset";

    //private StepData currentStepData;

    void Awake()
    {
        Debug.Log("[USERLEVEL] Awake() started");
        stepJsonFilePath = Application.persistentDataPath + "/stepData.json";
        playerData = FindObjectOfType<PlayerData>();

        // Check for logout first
        bool hasLoggedOut = PlayerPrefs.GetInt("HasLoggedOut", 0) == 1;
        Debug.Log($"[USERLEVEL] HasLoggedOut flag: {hasLoggedOut}");

        if (hasLoggedOut)
        {
            Debug.Log("[USERLEVEL] Fresh logout detected");
            stepCountData = string.Empty;
            
            // Set values to 0 immediately
            dailyStepCount = 0;
            overallStepCount = 0;
            remainingStepsForNextLevel = 0;
            currentStepCount = 0;
            totalStepsForNextLevel = CalculateTotalStepsForLevel(2);
            
            ResetStepData();

            // Find and subscribe to stepCounter BEFORE it processes
            stepCounter = FindObjectOfType<OverallStepCounter>();
            if (stepCounter != null)
            {
                OverallStepCounter.onStepsUpdated += OnStepsUpdatedFromCounter;
                Debug.Log("[USERLEVEL] Subscribed to step events AFTER logout");
            }
            
            // Update UI immediately to show 0
            UpdateText();
            UpdateExperienceBar();
        }
        else
        {
            // Normal startup - load from file first
            if (File.Exists(stepJsonFilePath))
            {
                stepCountData = File.ReadAllText(stepJsonFilePath);
            }
            else
            {
                StepData fresh = new StepData();
                File.WriteAllText(stepJsonFilePath, JsonUtility.ToJson(fresh));
                stepCountData = JsonUtility.ToJson(fresh);
            }

            stepCounter = FindObjectOfType<OverallStepCounter>();
            if (stepCounter != null)
            {
                OverallStepCounter.onLoaded += OnStepDataReadyFromCloud;
                OverallStepCounter.onStepsUpdated += OnStepsUpdatedFromCounter;
            }

            // Initialize with file data first
            InitializeUserLevelAndSteps();
            UpdateText();
            UpdateExperienceBar();
        }

        // Clean up logout flag AFTER everything is set up
        if (hasLoggedOut)
        {
            PlayerPrefs.DeleteKey("HasLoggedOut");
            Debug.Log("[USERLEVEL] Cleared HasLoggedOut flag");
        }
        
        Debug.Log($"[USERLEVEL] Awake() completed - Overall: {overallStepCount}, Daily: {dailyStepCount}");
    }

    void OnDestroy()
    {
        if (stepCounter != null)
        {
            OverallStepCounter.onLoaded -= OnStepDataReadyFromCloud;
            OverallStepCounter.onStepsUpdated -= OnStepsUpdatedFromCounter; // ADD THIS
        }
    }
    void OnStepsUpdatedFromCounter(int newOverallSteps, int newDailySteps)
    {
        // Add safeguards to prevent glitchy updates
        if (newOverallSteps < 0 || newDailySteps < 0)
        {
            Debug.LogWarning($"UserLevel: Received negative step values - Overall: {newOverallSteps}, Daily: {newDailySteps}");
            return; // Don't update with invalid values
        }

        // Only update if values actually changed to prevent unnecessary UI updates
        bool valuesChanged = (overallStepCount != newOverallSteps || dailyStepCount != newDailySteps);
        
        if (valuesChanged)
        {
            overallStepCount = newOverallSteps;
            dailyStepCount = newDailySteps;

            // Recalculate level-related values
            totalStepsForNextLevel = CalculateTotalStepsForLevel(playerData.level + 1);
            int totalStepsForCurrentLevel = CalculateTotalStepsForLevel(playerData.level);
            remainingStepsForNextLevel = totalStepsForNextLevel - overallStepCount;

            Debug.Log($"[STEPS UPDATE] Overall: {overallStepCount}, Daily: {dailyStepCount}, Level: {playerData.level}, Remaining: {remainingStepsForNextLevel}");

            // Check for level ups
            while (overallStepCount >= CalculateTotalStepsForLevel(playerData.level + 1))
            {
                playerData.level++;
                playerData.LevelUp();
                playerData.lastSavedLevel = playerData.level;
                playerData.SavePlayerData();
                totalStepsForNextLevel = CalculateTotalStepsForLevel(playerData.level + 1);
                remainingStepsForNextLevel = totalStepsForNextLevel - overallStepCount;
                Debug.Log($"[LEVEL UP] New level: {playerData.level}, Total steps for next: {totalStepsForNextLevel}");
            }

            // Update UI only once after all calculations
            UpdateText();
            UpdateExperienceBar();

            Debug.Log($"UserLevel: Steps updated via event - Overall: {overallStepCount}, Daily: {dailyStepCount}");
        }
    }

    void OnStepDataReadyFromCloud()
    {
        Debug.Log("Cloud step data is ready. Refreshing UI...");

        //currentStepData = stepCounter.stepData;
        cloudStepDataLoaded = true;
        // Use the in-memory step data from OverallStepCounter
        // if (stepCounter != null && stepCounter.stepData != null)
        // {
        //     dailyStepCount = stepCounter.stepData.dailySteps;
        //     overallStepCount = stepCounter.overallSteps;

        //     //overallStepCount += dailyStepCount;
        //     lastRecordedSteps = overallStepCount;
        //     stepCounter.GetOverallSteps();

        // }

        InitializeUserLevelAndSteps();
        UpdateInformation();
        UpdateText();
        UpdateExperienceBar();
    }

    void Update()
    {
        if (!cloudStepDataLoaded && stepCounter == null)
        {
            UpdateInformation();
            UpdateText();
            UpdateExperienceBar();
        }
    }

    // void InitializeUserLevelAndSteps()
    // {
    //     Debug.Log("Initializing user level and steps...");

    //     try
    //     {
    //         //dailyStepCount = currentStepData.dailySteps;
    //         //overallStepCount = currentStepData.overallSteps;

    //         StepData data = JsonUtility.FromJson<StepData>(stepCountData);

    //         // dailyStepCount = data.dailySteps;
    //         // overallStepCount = data.overallSteps;
    //         int dailyOffset = PlayerPrefs.GetInt(DailyStepOffsetKey, 0);
    //         int overallOffset = PlayerPrefs.GetInt(OverallStepOffsetKey, 0);

    //         dailyStepCount = Mathf.Max(0, data.dailySteps - dailyOffset);
    //         overallStepCount = Mathf.Max(0, data.overallSteps - overallOffset);


    //         Debug.Log($"Loaded Step Data - Daily: {dailyStepCount}, Overall: {overallStepCount}");

    //         int totalStepsForCurrentLevel = CalculateTotalStepsForLevel(playerData.level);
    //         totalStepsForNextLevel = CalculateTotalStepsForLevel(playerData.level + 1);
    //         remainingStepsForNextLevel = totalStepsForNextLevel - overallStepCount;

    //         if (playerData.level != playerData.lastSavedLevel)
    //         {
    //             Debug.LogWarning("Level mismatch detected. Correcting...");
    //             for (int i = playerData.lastSavedLevel; i < playerData.level; i++)
    //             {
    //                 playerData.LevelUp();
    //                 playerData.lastSavedLevel++;
    //             }
    //             playerData.SavePlayerData();
    //         }

    //         while (overallStepCount >= CalculateTotalStepsForLevel(playerData.level + 1))
    //         {
    //             playerData.level++;
    //             playerData.LevelUp();
    //             playerData.lastSavedLevel = playerData.level;
    //         }

    //         totalStepsForNextLevel = CalculateTotalStepsForLevel(playerData.level + 1);
    //         remainingStepsForNextLevel = totalStepsForNextLevel - overallStepCount;
    //     }
    //     catch (IOException e)
    //     {
    //         Debug.LogError($"Error initializing user level and steps: {e.Message}");
    //     }
    // }
    void InitializeUserLevelAndSteps()
    {
        Debug.Log("=== INITIALIZING USER LEVEL AND STEPS ===");

        try
        {
            // Check if we should get data from OverallStepCounter directly (cloud loaded scenario)
            if (stepCounter != null && stepCounter.cloudLoaded)
            {
                Debug.Log("[INIT] Getting data from OverallStepCounter (cloud loaded)");
                overallStepCount = stepCounter.overallSteps;  // Get processed value directly

                if (stepCounter.stepData != null)
                {
                    dailyStepCount = stepCounter.stepData.dailySteps;
                }

                Debug.Log($"[INIT] Got from StepCounter - Overall: {overallStepCount}, Daily: {dailyStepCount}");
            }
            else
            {
                Debug.Log("[INIT] Getting data from file (normal startup)");

                if (string.IsNullOrEmpty(stepCountData))
                {
                    Debug.Log("[INIT] stepCountData is empty, creating fresh data");
                    StepData fresh = new StepData();
                    stepCountData = JsonUtility.ToJson(fresh);
                }

                StepData data = JsonUtility.FromJson<StepData>(stepCountData);

                // Check if this is processed data (from cloud sync) or raw data
                if (data.overallSteps > 0)
                {
                    // This is processed data from cloud sync - use directly
                    overallStepCount = data.overallSteps;
                    dailyStepCount = data.dailySteps;
                    Debug.Log($"[INIT] Using processed cloud data - Overall: {overallStepCount}, Daily: {dailyStepCount}");
                }
                else
                {
                    // This is raw data - apply offsets
                    int dailyOffset = PlayerPrefs.GetInt(DailyStepOffsetKey, 0);
                    int overallOffset = PlayerPrefs.GetInt(OverallStepOffsetKey, 0);

                    dailyStepCount = Mathf.Max(0, data.dailySteps - dailyOffset);
                    overallStepCount = Mathf.Max(0, data.numberOfSteps - overallOffset);

                    Debug.Log($"[INIT] Applied offsets to raw data - Overall: {overallStepCount}, Daily: {dailyStepCount}");
                }
            }

            Debug.Log($"[INIT] Final Step Data - Overall: {overallStepCount}, Daily: {dailyStepCount}");

            // Calculate levels and UI...
            int totalStepsForCurrentLevel = CalculateTotalStepsForLevel(playerData.level);
            totalStepsForNextLevel = CalculateTotalStepsForLevel(playerData.level + 1);
            remainingStepsForNextLevel = totalStepsForNextLevel - overallStepCount;

            if (playerData.level != playerData.lastSavedLevel)
            {
                Debug.LogWarning("Level mismatch detected. Correcting...");
                for (int i = playerData.lastSavedLevel; i < playerData.level; i++)
                {
                    playerData.LevelUp();
                    playerData.lastSavedLevel++;
                }
                playerData.SavePlayerData();
            }

            while (overallStepCount >= CalculateTotalStepsForLevel(playerData.level + 1))
            {
                playerData.level++;
                playerData.LevelUp();
                playerData.lastSavedLevel = playerData.level;
            }

            totalStepsForNextLevel = CalculateTotalStepsForLevel(playerData.level + 1);
            remainingStepsForNextLevel = totalStepsForNextLevel - overallStepCount;

            Debug.Log($"[INIT COMPLETE] Level: {playerData.level}, Steps for next level: {totalStepsForNextLevel}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[INIT ERROR] Error initializing: {e.Message}");
        }
    }

    public int CalculateTotalStepsForLevel(int level)
    {
        return (100 * Mathf.FloorToInt(Mathf.Pow(level - 1, 2.35f)));
    }

    void UpdateText()
    {
        string colorOpen = "<color=#FFEE00>";
        string colorClose = "</color>";

        levelText.text = playerData.level.ToString();
        levelTextOutside.text = levelText.text;

        currentStepCountText.text = "Daily steps: " + colorOpen + dailyStepCount + colorClose;
        currentStepCountTextOutside.text = currentStepCount.ToString();

        totalStepsForNextLevelText.text = "Walk a total of " + colorOpen + ReformatIntToText(totalStepsForNextLevel) + colorClose +
            " steps to advance to Level " + colorOpen + (playerData.level + 1) + colorClose;

        remainingStepsForNextLevelText.text = "Remaining steps for next level: " + colorOpen + ReformatIntToText(remainingStepsForNextLevel) + colorClose;

        overallStepCountText.text = "Overall steps: " + colorOpen + overallStepCount + colorClose;

        int stepsThisLevel = overallStepCount - CalculateTotalStepsForLevel(playerData.level);
        int stepsNeeded = totalStepsForNextLevel - CalculateTotalStepsForLevel(playerData.level);
        float percent = stepsNeeded > 0 ? Mathf.Clamp01((float)stepsThisLevel / stepsNeeded) : 0f;
        percentageText.text = Mathf.RoundToInt(percent * 100) + "%";

        if (userNameText != null && playerData != null)
        {
            userNameText.text = playerData.playerName;
        }
    }

    // void UpdateInformation()
    // {
    //     try
    //     {
    //         // Check if the file exists before reading
    //         if (!File.Exists(stepJsonFilePath))
    //         {
    //             Debug.LogWarning("Step data file not found. Creating a new one.");
    //             File.WriteAllText(stepJsonFilePath, JsonUtility.ToJson(new StepData()));
    //         }
    //         stepCountData = File.ReadAllText(stepJsonFilePath);
    //         StepData data = JsonUtility.FromJson<StepData>(stepCountData);

    //         // dailyStepCount = currentStepData.dailySteps;
    //         // overallStepCount = currentStepData.overallSteps;

    //         // dailyStepCount = data.dailySteps;
    //         // overallStepCount = data.overallSteps;
    //         int dailyOffset = PlayerPrefs.GetInt(DailyStepOffsetKey, 0);
    //         int overallOffset = PlayerPrefs.GetInt(OverallStepOffsetKey, 0);

    //         dailyStepCount = Mathf.Max(0, data.dailySteps - dailyOffset);
    //         overallStepCount = Mathf.Max(0, data.overallSteps - overallOffset);


    //         totalStepsForNextLevel = CalculateTotalStepsForLevel(playerData.level + 1);
    //         remainingStepsForNextLevel = totalStepsForNextLevel - overallStepCount;

    //         while (overallStepCount >= CalculateTotalStepsForLevel(playerData.level + 1))
    //         {
    //             playerData.level++;
    //             playerData.LevelUp();
    //             playerData.lastSavedLevel = playerData.level;
    //             playerData.SavePlayerData();
    //         }
    //     }
    //     catch (IOException e)
    //     {
    //         Debug.LogError($"Error reading step data: {e.Message}");
    //     }
    // }
    void UpdateInformation()
    {
        try
        {
            // Don't update if cloud data is loaded - use the direct values instead
            if (stepCounter != null && stepCounter.cloudLoaded)
            {
                overallStepCount = stepCounter.overallSteps;
                if (stepCounter.stepData != null)
                {
                    dailyStepCount = stepCounter.stepData.dailySteps;
                }

                totalStepsForNextLevel = CalculateTotalStepsForLevel(playerData.level + 1);
                remainingStepsForNextLevel = totalStepsForNextLevel - overallStepCount;
                return; // Skip file reading
            }

            // Only read from file if cloud data isn't loaded
            if (!File.Exists(stepJsonFilePath))
            {
                Debug.LogWarning("Step data file not found. Creating a new one.");
                File.WriteAllText(stepJsonFilePath, JsonUtility.ToJson(new StepData()));
            }

            stepCountData = File.ReadAllText(stepJsonFilePath);
            StepData data = JsonUtility.FromJson<StepData>(stepCountData);

            // Check if this is processed data or raw data
            if (data.overallSteps > 0)
            {
                // Processed data from cloud
                overallStepCount = data.overallSteps;
                dailyStepCount = data.dailySteps;
            }
            else
            {
                // Raw data - apply offsets
                int dailyOffset = PlayerPrefs.GetInt(DailyStepOffsetKey, 0);
                int overallOffset = PlayerPrefs.GetInt(OverallStepOffsetKey, 0);

                dailyStepCount = Mathf.Max(0, data.dailySteps - dailyOffset);
                overallStepCount = Mathf.Max(0, data.numberOfSteps - overallOffset);
            }

            totalStepsForNextLevel = CalculateTotalStepsForLevel(playerData.level + 1);
            remainingStepsForNextLevel = totalStepsForNextLevel - overallStepCount;

            while (overallStepCount >= CalculateTotalStepsForLevel(playerData.level + 1))
            {
                playerData.level++;
                playerData.LevelUp();
                playerData.lastSavedLevel = playerData.level;
                playerData.SavePlayerData();
            }
        }
        catch (IOException e)
        {
            Debug.LogError($"Error reading step data: {e.Message}");
        }
    }

    public string ReformatIntToText(int number)
    {
        return number >= 10000 ? Mathf.Floor(number / 1000f) + "K" : number.ToString();
    }

    void UpdateExperienceBar()
    {
        int totalStepsForPreviousLevel = CalculateTotalStepsForLevel(playerData.level);
        int stepsThisLevel = overallStepCount - totalStepsForPreviousLevel;
        int stepsNeeded = totalStepsForNextLevel - totalStepsForPreviousLevel;

        float fillAmount = stepsNeeded > 0 ? Mathf.Clamp01((float)stepsThisLevel / stepsNeeded) : 0f;

        if (experienceBarImage != null)
        {
            experienceBarImage.fillAmount = fillAmount;
            Debug.Log($"[EXP BAR] Updated - Level: {playerData.level}, StepsThisLevel: {stepsThisLevel}, StepsNeeded: {stepsNeeded}, Fill: {fillAmount:F2}");
        }
    }
    public void ResetStepData()
    {
        Debug.Log("=== RESETTING STEP DATA IN USERLEVEL ===");

        PlayerPrefs.DeleteKey(DailyStepOffsetKey);
        PlayerPrefs.DeleteKey(OverallStepOffsetKey);
        PlayerPrefs.Save();

        // Reset in-memory values to 0
        dailyStepCount = 0;
        overallStepCount = 0;
        remainingStepsForNextLevel = 0;
        currentStepCount = 0;
        totalStepsForNextLevel = CalculateTotalStepsForLevel(2);

        // Create fresh step data file
        StepData fresh = new StepData();
        string freshJson = JsonUtility.ToJson(fresh);
        File.WriteAllText(stepJsonFilePath, freshJson);
        stepCountData = freshJson;

        // Clear cached data
        cloudStepDataLoaded = false;
        lastRecordedSteps = 0;
        addedSteps = 0;
        lastUIUpdatedStepCount = -1;

        // Update UI to show 0
        UpdateText();
        UpdateExperienceBar();

        Debug.Log($"[RESET COMPLETE] UserLevel reset - Overall: {overallStepCount}, Daily: {dailyStepCount}");
    }
}