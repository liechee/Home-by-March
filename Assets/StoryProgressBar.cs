using UnityEngine;
using UnityEngine.UI;

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


    void OnEnable()
    {
        PlayerPrefsCloudSync2.onPlayerPrefsLoaded += UpdateProgressBar;
    }

    void OnDisable()
    {
        PlayerPrefsCloudSync2.onPlayerPrefsLoaded -= UpdateProgressBar;
    }

    void Start()
    {
        UpdateProgressBar();
    }

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