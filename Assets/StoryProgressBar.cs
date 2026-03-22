using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays story progress based on ShardCollected_N PlayerPrefs keys.
///
/// WHY THE EVENT SUBSCRIPTIONS EXIST:
///   UpdateProgressBar() reads from PlayerPrefs. On first launch, PlayerPrefs
///   are loaded from cloud asynchronously AFTER all Start() calls have run.
///   Calling UpdateProgressBar() only in Start() always shows stale/local values
///   on the first scene — the correct values appear only after a scene reload
///   because Start() runs again with the now-loaded cloud data.
///
///   Fix: subscribe to PlayerPrefsCloudSync.onPlayerPrefsLoaded so the bar
///   refreshes the moment cloud PlayerPrefs arrive, regardless of which scene
///   is active or how many times the scene has been loaded.
/// </summary>
public class StoryProgressBar : MonoBehaviour
{
    [Header("UI Elements")]
    public Image fillImage;

    [Header("Star Holders")]
    public GameObject star1;
    public GameObject star2;
    public GameObject star3;

    [Header("Story Settings")]
    public int totalSubplots = 9;

    // ─────────────────────────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────────────────────────

    void OnEnable()
    {
        // Subscribe whenever this object becomes active so the bar always
        // reflects the latest data even if the object was disabled and re-enabled.
        PlayerPrefsCloudSync.onPlayerPrefsLoaded += UpdateProgressBar;
    }

    void OnDisable()
    {
        PlayerPrefsCloudSync.onPlayerPrefsLoaded -= UpdateProgressBar;
    }

    void Start()
    {
        // Show whatever is in PlayerPrefs right now (may be local/stale on first open).
        // onPlayerPrefsLoaded will call UpdateProgressBar again once cloud data arrives.
        UpdateProgressBar();
    }

    // ─────────────────────────────────────────────────────────
    //  Progress Bar Update
    // ─────────────────────────────────────────────────────────

    public void UpdateProgressBar()
    {
        int collectedShards = 0;

        for (int i = 0; i < totalSubplots; i++)
        {
            if (PlayerPrefs.GetInt($"ShardCollected_{i}", 0) == 1)
                collectedShards++;
        }

        float fillAmount = Mathf.Clamp01((float)collectedShards / totalSubplots);

        if (fillImage != null)
            fillImage.fillAmount = fillAmount;

        int starsUnlocked = collectedShards / 3;
        if (star1 != null) star1.SetActive(starsUnlocked >= 1);
        if (star2 != null) star2.SetActive(starsUnlocked >= 2);
        if (star3 != null) star3.SetActive(starsUnlocked >= 3);

        Debug.Log($"[StoryProgressBar] Shards: {collectedShards}/{totalSubplots}, " +
                  $"Stars: {starsUnlocked}, Fill: {fillAmount * 100f:F0}%");
    }
}