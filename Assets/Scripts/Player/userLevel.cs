using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HomeByMarch;
using System;

/// <summary>
/// Displays step counts, level, and experience bar.
///
/// Single source of truth: ALL step data comes from OverallStepCounter events.
/// UserLevel never reads the step file or applies offsets directly — that logic
/// lives exclusively in OverallStepCounter to avoid the two systems drifting apart.
///
/// Startup sequence:
///   1. Subscribe to onStepsUpdated + onLoaded.
///   2. If OverallStepCounter already has data (e.g. loaded before this scene),
///      pull its current values immediately so the UI is never blank.
///   3. All subsequent updates arrive via onStepsUpdated.
/// </summary>
public class UserLevel : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────
    //  UI References
    // ─────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────
    //  Step State  (written only from OnStepsUpdated)
    // ─────────────────────────────────────────────────────────

    public int currentStepCount;      // kept for LogOutManager reset compat
    public int dailyStepCount;
    public int overallStepCount;
    public int totalStepsForNextLevel;
    public int remainingStepsForNextLevel;

    // ─────────────────────────────────────────────────────────
    //  Private
    // ─────────────────────────────────────────────────────────

    private OverallStepCounter stepCounter;

    // ─────────────────────────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────────────────────────

    void Awake()
    {
        playerData = FindObjectOfType<PlayerData>();
        stepCounter = FindObjectOfType<OverallStepCounter>();

        // Subscribe first — never miss an event
        OverallStepCounter.onStepsUpdated += OnStepsUpdated;
        OverallStepCounter.onLoaded       += OnStepDataLoaded;

        // Show zeros immediately so the UI is never blank or stale
        ZeroDisplayState();
        RefreshUI();

        // If OverallStepCounter already has valid data (e.g. this scene loaded after
        // the counter ran its first query), pull the values right now instead of
        // waiting for the next event tick.
        if (stepCounter != null && stepCounter.stepData != null &&
            (stepCounter.overallSteps > 0 || stepCounter.cloudLoaded))
        {
            ApplySteps(stepCounter.overallSteps, stepCounter.stepData.dailySteps);
        }
    }

    void OnDestroy()
    {
        OverallStepCounter.onStepsUpdated -= OnStepsUpdated;
        OverallStepCounter.onLoaded       -= OnStepDataLoaded;
    }

    // ─────────────────────────────────────────────────────────
    //  Event Handlers
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Fired by OverallStepCounter every time the step count changes meaningfully.
    /// This is the single entry point for all step data — no file reads here.
    /// </summary>
    private void OnStepsUpdated(int newOverall, int newDaily)
    {
        if (newOverall < 0 || newDaily < 0)
        {
            Debug.LogWarning($"[UserLevel] Ignoring negative step values: overall={newOverall}, daily={newDaily}");
            return;
        }

        // Skip if nothing changed (OverallStepCounter already debounces by threshold,
        // but guard here too in case onLoaded triggers a duplicate)
        if (newOverall == overallStepCount && newDaily == dailyStepCount) return;

        ApplySteps(newOverall, newDaily);
    }

    /// <summary>
    /// Fired by OverallStepCounter when initial data is ready (local file or cloud).
    /// Re-pulls values in case the step count was set before onStepsUpdated fired.
    /// </summary>
    private void OnStepDataLoaded()
    {
        if (stepCounter == null) return;
        ApplySteps(stepCounter.overallSteps,
                   stepCounter.stepData != null ? stepCounter.stepData.dailySteps : 0);
    }

    // ─────────────────────────────────────────────────────────
    //  Core Apply Logic
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Single method that accepts authoritative step values from OverallStepCounter,
    /// recalculates level and XP, then updates the UI.
    /// All previous paths (file reads, offset math, cloud branches) are removed —
    /// OverallStepCounter owns that logic exclusively.
    /// </summary>
    private void ApplySteps(int newOverall, int newDaily)
    {
        overallStepCount = newOverall;
        dailyStepCount   = newDaily;
        currentStepCount = newDaily; // currentStepCount mirrors daily for display

        RecalculateLevelAndXP();
        RefreshUI();

        Debug.Log($"[UserLevel] Applied — Overall: {overallStepCount}, Daily: {dailyStepCount}, Level: {playerData.level}");
    }

    /// <summary>
    /// Checks for level-ups and recalculates XP thresholds.
    /// Level-up logic is only here, not duplicated across Init/Update/Cloud paths.
    /// </summary>
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
            Debug.Log($"[UserLevel] Level up → {playerData.level}");
        }

        totalStepsForNextLevel     = CalculateTotalStepsForLevel(playerData.level + 1);
        remainingStepsForNextLevel = Mathf.Max(0, totalStepsForNextLevel - overallStepCount);
    }

    // ─────────────────────────────────────────────────────────
    //  UI Refresh
    // ─────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────

    public int CalculateTotalStepsForLevel(int level)
    {
        return 100 * Mathf.FloorToInt(Mathf.Pow(level - 1, 2.35f));
    }

    public string ReformatIntToText(int number)
    {
        return number >= 10000 ? Mathf.Floor(number / 1000f) + "K" : number.ToString();
    }

    // ─────────────────────────────────────────────────────────
    //  Reset  (called by LogOutManager)
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Zeroes all step display state and refreshes UI to 0.
    /// Called by LogOutManager as part of the nuclear wipe.
    /// The actual step data reset lives in OverallStepCounter.ResetStepDataCompletely().
    /// </summary>
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