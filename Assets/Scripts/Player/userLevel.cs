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

    //private StepData currentStepData;

    void Awake()
    {
        stepJsonFilePath = Application.persistentDataPath + "/stepData.json";

        // Load from file as fallback
        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
        {
            Debug.Log("Fresh logout detected. Skipping step data load.");
            stepCountData = string.Empty;
            ResetStepData();
            // Don't delete the flag yet - let other components handle it
        }
        else if (File.Exists(stepJsonFilePath))
        {
            stepCountData = File.ReadAllText(stepJsonFilePath);
        }
        else
        {
            Debug.LogWarning("Step data file not found. Creating a new one.");
            StepData fresh = new StepData(); // defaults
            File.WriteAllText(stepJsonFilePath, JsonUtility.ToJson(fresh));
            stepCountData = JsonUtility.ToJson(fresh);
        }

        playerData = FindObjectOfType<PlayerData>();

        stepCounter = FindObjectOfType<OverallStepCounter>();
        if (stepCounter != null)
        {
            OverallStepCounter.onLoaded += OnStepDataReadyFromCloud;
        }

        //InitializeUserLevelAndSteps();
        // Perform first-time init using local file (but only if cloud won't override it)
        if (stepCounter == null || PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
        {
            InitializeUserLevelAndSteps();
            UpdateText();
            UpdateExperienceBar();
        }

        // Only delete the logout flag after all components have handled reset
        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1)
        {
            PlayerPrefs.DeleteKey("HasLoggedOut");
        }
    }

    void OnDestroy()
    {
        if (stepCounter != null)
        {
            OverallStepCounter.onLoaded -= OnStepDataReadyFromCloud;
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
        if (!cloudStepDataLoaded)
        {
            UpdateInformation();
            UpdateText();
            UpdateExperienceBar();
        }
    }

    void InitializeUserLevelAndSteps()
    {
        Debug.Log("Initializing user level and steps...");

        try
        {
            //dailyStepCount = currentStepData.dailySteps;
            //overallStepCount = currentStepData.overallSteps;

            StepData data = JsonUtility.FromJson<StepData>(stepCountData);

            dailyStepCount = data.dailySteps;
            overallStepCount = data.overallSteps;

            Debug.Log($"Loaded Step Data - Daily: {dailyStepCount}, Overall: {overallStepCount}");

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
        }
        catch (IOException e)
        {
            Debug.LogError($"Error initializing user level and steps: {e.Message}");
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
        percentageText.text = Mathf.FloorToInt(percent * 100) + "%";

        if (userNameText != null && playerData != null)
        {
            userNameText.text = playerData.playerName;
        }
    }

    void UpdateInformation()
    {
        try
        {
            // Check if the file exists before reading
            if (!File.Exists(stepJsonFilePath))
            {
                Debug.LogWarning("Step data file not found. Creating a new one.");
                File.WriteAllText(stepJsonFilePath, JsonUtility.ToJson(new StepData()));
            }
            stepCountData = File.ReadAllText(stepJsonFilePath);
            StepData data = JsonUtility.FromJson<StepData>(stepCountData);

            // dailyStepCount = currentStepData.dailySteps;
            // overallStepCount = currentStepData.overallSteps;

            dailyStepCount = data.dailySteps;
            overallStepCount = data.overallSteps;

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
        int differenceInSteps = totalStepsForNextLevel - totalStepsForPreviousLevel;

        float fillAmount = (float)(differenceInSteps - remainingStepsForNextLevel) / differenceInSteps;

        if (experienceBarImage != null)
        {
            experienceBarImage.fillAmount = Mathf.Clamp01(fillAmount);
        }
    }
    public void ResetStepData()
    {
        // Reset in-memory values
        dailyStepCount = 0;
        overallStepCount = 0;
        remainingStepsForNextLevel = 0;
        currentStepCount = 0;
        totalStepsForNextLevel = CalculateTotalStepsForLevel(2); // Assuming level 1 is the start

        // Reset the file on disk - ensure it's completely fresh
        StepData fresh = new StepData(); // defaults
        string freshJson = JsonUtility.ToJson(fresh);
        File.WriteAllText(stepJsonFilePath, freshJson);
        stepCountData = freshJson;

        // Force clear any cached data
        cloudStepDataLoaded = false;
        lastRecordedSteps = 0;
        addedSteps = 0;
        lastUIUpdatedStepCount = -1;

        // Optionally update UI
        UpdateText();
        UpdateExperienceBar();

        Debug.Log("Step data has been completely reset and cleared from memory.");
    }
}