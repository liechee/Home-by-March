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
    private StepData currentStepData;

    void Awake()
    {
        stepJsonFilePath = Application.persistentDataPath + "/stepData.json";

        // Load from file as fallback
        if (File.Exists(stepJsonFilePath))
        {
            string json = File.ReadAllText(stepJsonFilePath);
            currentStepData = JsonUtility.FromJson<StepData>(json);
        }
        else
        {
            Debug.LogWarning("Step data file not found. Creating a new one.");
            currentStepData = new StepData();
            File.WriteAllText(stepJsonFilePath, JsonUtility.ToJson(currentStepData));
        }

        playerData = FindObjectOfType<PlayerData>();
        InitializeUserLevelAndSteps();

        stepCounter = FindObjectOfType<OverallStepCounter>();
        if (stepCounter != null)
        {
            OverallStepCounter.onLoaded += OnStepDataReadyFromCloud;
        }

        if (stepCounter?.stepData != null && stepCounter.overallSteps > 0)
        {
            OnStepDataReadyFromCloud();
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

        currentStepData = stepCounter.stepData;
        cloudStepDataLoaded = true;

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
            dailyStepCount = currentStepData.dailySteps;
            overallStepCount = currentStepData.overallSteps;

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
            dailyStepCount = currentStepData.dailySteps;
            overallStepCount = currentStepData.overallSteps;

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
}