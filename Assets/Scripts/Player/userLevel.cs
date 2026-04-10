using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HomeByMarch;
using System;

public class UserLevel : MonoBehaviour
{

    [Header("Level UI")]
    [SerializeField] public TMP_Text levelText;
    [SerializeField] public TMP_Text levelTextOutside;

    [Header("Step UI")]
    public TMP_Text currentStepCountText;
    public TMP_Text currentStepCountTextOutside;
    public TMP_Text totalStepsForNextLevelText;
    public TMP_Text remainingStepsForNextLevelText;
    public TMP_Text overallStepCountText;
    public TMP_Text percentageText;

    [Header("Experience Bar")]
    public Image experienceBarImage;

    [Header("User Info")]
    public TMP_Text userNameText;
    public PlayerData playerData;


    public int currentStepCount;     
    public int dailyStepCount;
    public int overallStepCount;
    public int totalStepsForNextLevel;
    public int remainingStepsForNextLevel;


    private OverallStepCounter stepCounter;
    [SerializeField] private bool debugStepLogs = false;


    void Awake()
    {
        playerData  = FindObjectOfType<PlayerData>();
        stepCounter = FindObjectOfType<OverallStepCounter>();

        // Initialize display thresholds so the UI is structurally valid even before
        // real step data arrives — but do NOT zero-flash if we already have data.
        if (playerData != null)
            totalStepsForNextLevel = CalculateTotalStepsForLevel(playerData.level + 1);
    }

    void Start()
    {
        if (stepCounter != null && stepCounter.stepData != null)
        {
            ApplySteps(stepCounter.overallSteps, stepCounter.stepData.dailySteps);
        }
        else
        {
            // stepCounter not ready yet — show zeros cleanly until the event fires
            ZeroDisplayState();
            RefreshUI();
        }
    }

    void OnEnable()
    {
        if (stepCounter == null)
            stepCounter = FindObjectOfType<OverallStepCounter>();

        if (playerData == null)
            playerData = FindObjectOfType<PlayerData>();

        // Idempotent subscription to avoid duplicate listeners after scene/UI toggles.
        OverallStepCounter.onStepsUpdated -= OnStepsUpdated;
        OverallStepCounter.onLoaded -= OnStepDataLoaded;
        OverallStepCounter.onStepsUpdated += OnStepsUpdated;
        OverallStepCounter.onLoaded += OnStepDataLoaded;

        PlayerData.onPlayerDataChanged -= OnPlayerDataChanged;
        PlayerData.onPlayerDataChanged += OnPlayerDataChanged;

        // Ensure text reflects latest player profile as soon as this UI appears.
        RefreshUI();
    }

    void OnDisable()
    {
        OverallStepCounter.onStepsUpdated -= OnStepsUpdated;
        OverallStepCounter.onLoaded -= OnStepDataLoaded;
        PlayerData.onPlayerDataChanged -= OnPlayerDataChanged;
    }

    void OnDestroy()
    {
        OverallStepCounter.onStepsUpdated -= OnStepsUpdated;
        OverallStepCounter.onLoaded       -= OnStepDataLoaded;
        PlayerData.onPlayerDataChanged    -= OnPlayerDataChanged;
    }

    private void OnStepsUpdated(int newOverall, int newDaily)
    {
        if (newOverall < 0 || newDaily < 0)
        {
            Debug.LogWarning($"[UserLevel] Ignoring negative step values: overall={newOverall}, daily={newDaily}");
            return;
        }

        ApplySteps(newOverall, newDaily);
    }

    private void OnStepDataLoaded()
    {
        if (stepCounter == null) return;
        ApplySteps(stepCounter.overallSteps,
                   stepCounter.stepData != null ? stepCounter.stepData.dailySteps : 0);
    }

    private void OnPlayerDataChanged()
    {
        if (playerData == null)
            playerData = FindObjectOfType<PlayerData>();

        if (playerData == null)
            return;

        RecalculateLevelAndXP();
        RefreshUI();
    }

    private void ApplySteps(int newOverall, int newDaily)
    {
        overallStepCount = newOverall;
        dailyStepCount   = newDaily;
        currentStepCount = newDaily; // currentStepCount mirrors daily for display

        RecalculateLevelAndXP();
        RefreshUI();

        if (debugStepLogs)
            Debug.Log($"[UserLevel] Applied — Overall: {overallStepCount}, Daily: {dailyStepCount}, Level: {playerData.level}");
    }

    private void RecalculateLevelAndXP()
    {
        if (playerData == null) return;

        // Process any pending level-ups
        while (overallStepCount >= CalculateTotalStepsForLevel(playerData.level + 1))
        {
            playerData.level++;
            playerData.LevelUp();
            playerData.lastSavedLevel = playerData.level;
            playerData.SavePlayerData();
            if (debugStepLogs)
                Debug.Log($"[UserLevel] Level up → {playerData.level}");
        }

        totalStepsForNextLevel     = CalculateTotalStepsForLevel(playerData.level + 1);
        remainingStepsForNextLevel = Mathf.Max(0, totalStepsForNextLevel - overallStepCount);
    }

    private void RefreshUI()
    {
        UpdateText();
        UpdateExperienceBar();
    }

    private void UpdateText()
    {
        if (playerData == null) return;

        const string colorOpen  = "<color=#FFEE00>";
        const string colorClose = "</color>";

        if (levelText != null)        levelText.text        = playerData.level.ToString();
        if (levelTextOutside != null)  levelTextOutside.text = playerData.level.ToString();

        if (currentStepCountText != null)
            currentStepCountText.text = "Daily steps: " + colorOpen + dailyStepCount + colorClose;

        if (currentStepCountTextOutside != null)
            currentStepCountTextOutside.text = dailyStepCount.ToString();

        if (totalStepsForNextLevelText != null)
            totalStepsForNextLevelText.text =
                "Walk a total of " + colorOpen + ReformatIntToText(totalStepsForNextLevel) + colorClose +
                " steps to advance to Level " + colorOpen + (playerData.level + 1) + colorClose;

        if (remainingStepsForNextLevelText != null)
            remainingStepsForNextLevelText.text =
                "Remaining steps for next level: " + colorOpen + ReformatIntToText(remainingStepsForNextLevel) + colorClose;

        if (overallStepCountText != null)
            overallStepCountText.text = "Overall steps: " + colorOpen + overallStepCount + colorClose;

        if (percentageText != null)
        {
            int   stepsThisLevel = overallStepCount - CalculateTotalStepsForLevel(playerData.level);
            int   stepsNeeded    = totalStepsForNextLevel - CalculateTotalStepsForLevel(playerData.level);
            float percent        = stepsNeeded > 0 ? Mathf.Clamp01((float)stepsThisLevel / stepsNeeded) : 0f;
            percentageText.text  = Mathf.RoundToInt(percent * 100) + "%";
        }

        if (userNameText != null && playerData != null)
            userNameText.text = playerData.playerName;
    }

    private void UpdateExperienceBar()
    {
        if (experienceBarImage == null || playerData == null) return;

        int   stepsThisLevel = overallStepCount - CalculateTotalStepsForLevel(playerData.level);
        int   stepsNeeded    = totalStepsForNextLevel - CalculateTotalStepsForLevel(playerData.level);
        float fill           = stepsNeeded > 0 ? Mathf.Clamp01((float)stepsThisLevel / stepsNeeded) : 0f;

        experienceBarImage.fillAmount = fill;
        Debug.Log($"[UserLevel] XP bar — level={playerData.level}, stepsThisLevel={stepsThisLevel}, fill={fill:F2}");
    }


    public int CalculateTotalStepsForLevel(int level)
    {
        return 100 * Mathf.FloorToInt(Mathf.Pow(level - 1, 2.35f));
    }

    public string ReformatIntToText(int number)
    {
        return number >= 10000 ? Mathf.Floor(number / 1000f) + "K" : number.ToString();
    }

    public void ResetStepData()
    {
        ZeroDisplayState();
        RefreshUI();
        Debug.Log("[UserLevel] Display state reset to zero.");
    }

    private void ZeroDisplayState()
    {
        dailyStepCount             = 0;
        overallStepCount           = 0;
        currentStepCount           = 0;
        remainingStepsForNextLevel = 0;
        totalStepsForNextLevel     = CalculateTotalStepsForLevel(2);
    }
}