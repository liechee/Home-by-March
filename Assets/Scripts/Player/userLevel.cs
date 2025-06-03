using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HomeByMarch;

public class UserLevel : MonoBehaviour
{
    [SerializeField] public TMP_Text levelText;
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

    void Awake()
    {
        stepJsonFilePath = Application.persistentDataPath + "/stepData.json";

        // Check if the step data file exists before trying to read it
        if (File.Exists(stepJsonFilePath))
        {
            stepCountData = File.ReadAllText(stepJsonFilePath);
        }
        else
        {
            Debug.LogWarning("Step data file not found. Creating a new file with default data.");
            File.WriteAllText(stepJsonFilePath, JsonUtility.ToJson(new StepData()));
            stepCountData = File.ReadAllText(stepJsonFilePath);
        }

        // Load PlayerData from the scene
        playerData = FindObjectOfType<PlayerData>();

        // Initialize user level and steps
        InitializeUserLevelAndSteps();
    }

    void Update()
    {
        UpdateInformation();
        UpdateText();
        UpdateExperienceBar();
    }

    void InitializeUserLevelAndSteps()
    {
        Debug.Log("Initializing user level and steps...");

        try
        {
            StepData data = JsonUtility.FromJson<StepData>(stepCountData);

            dailyStepCount = data.dailySteps;
            overallStepCount = data.overallSteps;

            Debug.Log($"Loaded Step Data - Daily: {dailyStepCount}, Overall: {overallStepCount}");

            int totalStepsForCurrentLevel = CalculateTotalStepsForLevel(playerData.level);
            totalStepsForNextLevel = CalculateTotalStepsForLevel(playerData.level + 1);
            remainingStepsForNextLevel = totalStepsForNextLevel - overallStepCount;

            Debug.Log($"Level: {playerData.level}, Steps for Current Level: {totalStepsForCurrentLevel}, Next Level: {totalStepsForNextLevel}");

            if (playerData.level != playerData.lastSavedLevel)
            {
                Debug.LogWarning("Level mismatch detected. Correcting level...");
                for (int i = playerData.lastSavedLevel; i < playerData.level; i++)
                {
                    Debug.Log($"Applying Level-Up for level {i + 1}");
                    playerData.LevelUp();
                    playerData.lastSavedLevel++;
                }
                playerData.SavePlayerData();
            }

            while (overallStepCount >= CalculateTotalStepsForLevel(playerData.level + 1))
            {
                playerData.level++;
                Debug.Log($"Level Up! New Level: {playerData.level}");
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
        string colorTagOpen = "<color=#FFEE00>"; // red
    string colorTagClose = "</color>";
    
        levelText.text = playerData.level.ToString();

        currentStepCountText.text = "Daily steps: " + colorTagOpen + dailyStepCount + colorTagClose;

        currentStepCountTextOutside.text = currentStepCount.ToString();

        totalStepsForNextLevelText.text = "Walk a total of " + colorTagOpen + ReformatIntToText(totalStepsForNextLevel) + colorTagClose + " steps to advance to Level " + colorTagOpen + (playerData.level + 1) + colorTagClose;

        remainingStepsForNextLevelText.text = "Remaining steps for next level: " + colorTagOpen + ReformatIntToText(remainingStepsForNextLevel) + colorTagClose;
        
        overallStepCountText.text = "Overall steps: " + colorTagOpen + overallStepCount + colorTagClose;
       
        percentageText.text = Mathf.FloorToInt((float)(overallStepCount - CalculateTotalStepsForLevel(playerData.level)) / (totalStepsForNextLevel - CalculateTotalStepsForLevel(playerData.level)) * 100) + "%"; // percentage

        if (userNameText != null && playerData != null)
        {
            userNameText.text = playerData.playerName;
        }
    }

    void UpdateInformation()
    {
        try
        {
            stepCountData = File.ReadAllText(stepJsonFilePath);
            StepData data = JsonUtility.FromJson<StepData>(stepCountData);

            dailyStepCount = data.dailySteps;
            overallStepCount = data.overallSteps;

            totalStepsForNextLevel = CalculateTotalStepsForLevel(playerData.level + 1);
            remainingStepsForNextLevel = totalStepsForNextLevel - overallStepCount;

            while (overallStepCount >= CalculateTotalStepsForLevel(playerData.level + 1))
            {
                playerData.level++;
                Debug.Log($"Level Up! New Level: {playerData.level}");
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
        if (number >= 10000)
        {
            return "" + Mathf.Floor(number / 1000) + "K";
        }
        else
        {
            return number.ToString();
        }
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
