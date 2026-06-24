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
    private bool hasUserInteracted = false;
    private bool stepCountingStarted = false;
    private bool hasReceivedSteps = false;
    private bool isGuestLoginActive = false;

    void Awake()
    {
        playerData = FindObjectOfType<PlayerData>();
        stepCounter = FindObjectOfType<OverallStepCounter>();

        // Initialize display thresholds
        if (playerData != null)
            totalStepsForNextLevel = CalculateTotalStepsForLevel(playerData.level + 1);

        // Check if GuestLogin is active
        CheckGuestLoginStatus();
    }

    // void Start()
    // {
    //     // If GuestLogin is active, start counting immediately without waiting for interaction
    //     if (isGuestLoginActive)
    //     {
    //         Debug.Log("[UserLevel] GuestLogin active - starting step counting immediately");
    //         StartStepCounting();
    //     }
    //     else
    //     {
    //         // Don't start step counting automatically - wait for user interaction
    //         // Just display existing data without resetting to zero
    //         if (stepCounter != null && stepCounter.stepData != null)
    //         {
    //             // Display existing step values without resetting
    //             overallStepCount = stepCounter.overallSteps;
    //             dailyStepCount = stepCounter.stepData.dailySteps;
    //             currentStepCount = dailyStepCount;
    //             RecalculateLevelAndXP();
    //             RefreshUI();
    //             stepCounter.StartDelayedGuestStepCounting();
    //         }
    //         else
    //         {
    //             // Show current values without resetting
    //             RefreshUI();
    //         }

    //         Debug.Log("[UserLevel] Waiting for user interaction to start step counting");
    //     }
    // }
    void Start()
    {
        if (PlayerPrefs.GetInt("HasLoggedOut", 0) == 1) return;
        if (stepCounter.initializingFreshData) return; // this check is on stepCounter, move it:

        if (stepCounter != null && stepCounter.stepData != null)
        {
            overallStepCount = stepCounter.overallSteps;
            // Use the live accumulated daily total, not just the session's fixed baseline —
            // savedDailyBase never grows during the session, so using it alone causes the
            // displayed daily count to drop back to its starting value whenever this object
            // re-initializes (e.g. returning to the main scene from elsewhere).
            dailyStepCount = Mathf.Max(stepCounter.stepData.dailySteps, stepCounter.savedDailyBase);
            currentStepCount = dailyStepCount;
            RecalculateLevelAndXP();
            RefreshUI();
        }
        else
        {
            RefreshUI();
        }

        stepCounter?.StartDelayedGuestStepCounting(); // safe — no-ops if !isGuestLoginPending
    }

    // In UserLevel.cs, make sure OnEnable has proper subscription:

    void OnEnable()
    {
        OverallStepCounter.onStepsUpdated -= OnStepsUpdated;
        OverallStepCounter.onLoaded -= OnStepDataLoaded;
        PlayerData.onPlayerDataChanged -= OnPlayerDataChanged;

        OverallStepCounter.onStepsUpdated += OnStepsUpdated;
        OverallStepCounter.onLoaded += OnStepDataLoaded;
        PlayerData.onPlayerDataChanged += OnPlayerDataChanged;

        stepCounter = FindObjectOfType<OverallStepCounter>();
        playerData = FindObjectOfType<PlayerData>();
        // Force immediate refresh if step counter already has data
        if (stepCounter != null && stepCounter.stepData != null)
        {
            int overall = stepCounter.overallSteps;
            int daily = Mathf.Max(stepCounter.stepData.dailySteps, stepCounter.savedDailyBase);

            Debug.Log($"[UserLevel] OnEnable immediate refresh — overall={overall}, daily={daily}");
            ApplySteps(overall, daily);
        }
        RefreshUI();
    }

    private void OnStepsUpdated(int newOverall, int newDaily)
    {
        Debug.Log($"[UserLevel] OnStepsUpdated received - Overall: {newOverall}, Daily: {newDaily}");
        ApplySteps(newOverall, newDaily);
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
        OverallStepCounter.onLoaded -= OnStepDataLoaded;
        PlayerData.onPlayerDataChanged -= OnPlayerDataChanged;
    }

    void Update()
    {
        // Detect user interaction (touch, mouse click, key press) - only if not GuestLogin
        if (!isGuestLoginActive && !hasUserInteracted && !stepCountingStarted)
        {
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                StartStepCounting();
            }
        }
    }

    /// <summary>
    /// Check if GuestLogin is active
    /// </summary>
    private void CheckGuestLoginStatus()
    {
        try
        {
            // Check if GuestLoginManager has started step counting
            if (PlayerPrefs.GetInt("StepCountingActive", 0) == 1 ||
                PlayerPrefs.GetInt("GuestLoginStepCountingStarted", 0) == 1 ||
                PlayerPrefs.GetInt("IsGuestSession", 0) == 1)
            {
                isGuestLoginActive = true;
                Debug.Log("[UserLevel] GuestLogin is active");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[UserLevel] Error checking GuestLogin status: {e.Message}");
        }
    }

    /// <summary>
    /// Start step counting on user interaction or GuestLogin
    /// </summary>
    private void StartStepCounting()
    {
        if (stepCountingStarted) return;

        Debug.Log("[UserLevel] Starting step counting");
        hasUserInteracted = true;
        stepCountingStarted = true;

        if (stepCounter == null)
        {
            stepCounter = FindObjectOfType<OverallStepCounter>();
        }

        if (stepCounter != null)
        {
            // Don't reset to zero - just start counting from current values
            stepCounter.GetOverallSteps();
            Debug.Log($"[UserLevel] Step counting started - Current overall: {overallStepCount}, Daily: {dailyStepCount}");
        }
        else
        {
            Debug.LogWarning("[UserLevel] StepCounter not found");
        }
    }



    private void OnStepDataLoaded()
    {
        if (stepCounter == null) return;

        if (!stepCountingStarted)
        {
            Debug.Log("[UserLevel] Step data loaded but counting not started - storing values");
            hasReceivedSteps = true;
        }
        int daily = stepCounter.stepData != null
        ? Mathf.Max(stepCounter.stepData.dailySteps, stepCounter.savedDailyBase)
        : stepCounter.savedDailyBase;

        ApplySteps(stepCounter.overallSteps, daily);
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
        // Don't reset - just update with new values
        overallStepCount = newOverall;
        dailyStepCount = newDaily;
        currentStepCount = newDaily;

        RecalculateLevelAndXP();
        RefreshUI();

        if (debugStepLogs)
            Debug.Log($"[UserLevel] Applied — Overall: {overallStepCount}, Daily: {dailyStepCount}, Level: {playerData?.level ?? 1}");
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

        totalStepsForNextLevel = CalculateTotalStepsForLevel(playerData.level + 1);
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

        const string colorOpen = "<color=#FFEE00>";
        const string colorClose = "</color>";

        if (levelText != null) levelText.text = playerData.level.ToString();
        if (levelTextOutside != null) levelTextOutside.text = playerData.level.ToString();

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
            int stepsThisLevel = overallStepCount - CalculateTotalStepsForLevel(playerData.level);
            int stepsNeeded = totalStepsForNextLevel - CalculateTotalStepsForLevel(playerData.level);
            float percent = stepsNeeded > 0 ? Mathf.Clamp01((float)stepsThisLevel / stepsNeeded) : 0f;
            percentageText.text = Mathf.RoundToInt(percent * 100) + "%";
        }

        if (userNameText != null && playerData != null)
            userNameText.text = playerData.playerName;
    }

    private void UpdateExperienceBar()
    {
        if (experienceBarImage == null || playerData == null) return;

        int stepsThisLevel = overallStepCount - CalculateTotalStepsForLevel(playerData.level);
        int stepsNeeded = totalStepsForNextLevel - CalculateTotalStepsForLevel(playerData.level);
        float fill = stepsNeeded > 0 ? Mathf.Clamp01((float)stepsThisLevel / stepsNeeded) : 0f;

        experienceBarImage.fillAmount = fill;

        if (debugStepLogs)
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
        // Only reset display, not the actual step counter values
        RefreshUI();
        Debug.Log("[UserLevel] UI refreshed without resetting step values");
    }

    /// <summary>
    /// Called by GuestLoginManager to notify that step counting has started
    /// </summary>
    public void OnGuestLoginStepCountingStarted()
    {
        Debug.Log("[UserLevel] Notified by GuestLoginManager that step counting has started");
        isGuestLoginActive = true;

        if (!stepCountingStarted)
        {
            StartStepCounting();
        }

        // Refresh display with current step data
        if (stepCounter != null && stepCounter.stepData != null)
        {
            ApplySteps(stepCounter.overallSteps, stepCounter.stepData.dailySteps);
        }
    }

    /// <summary>
    /// Manually start step counting (can be called from UI buttons)
    /// </summary>
    public void OnUserInteractionStartCounting()
    {
        if (!stepCountingStarted)
        {
            StartStepCounting();
        }
    }

    /// <summary>
    /// Force refresh step count from the step counter
    /// </summary>
    public void RefreshStepCount()
    {
        if (stepCounter != null && stepCountingStarted)
        {
            stepCounter.GetOverallSteps();
            Debug.Log("[UserLevel] Step count manually refreshed");
        }
    }

    /// <summary>
    /// Check if step counting is active
    /// </summary>
    public bool IsStepCountingActive()
    {
        return stepCountingStarted;
    }

    /// <summary>
    /// Check if GuestLogin is active
    /// </summary>
    public bool IsGuestLoginActive()
    {
        return isGuestLoginActive;
    }
}