using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HomeByMarch;

public class BossDungeonController : MonoBehaviour
{
    [SerializeField] private GameObject spawnedBoss; // Only the boss, not any enemy
    private bool gameActive = false;

    [Header("UI Elements")]
    public GameObject gameOverPanel;
    public GameObject winPanel;
    public GameObject goldRewardPanel;

    public GameObject shardCollectionPanel;
    public GameObject[] shardOnImages;
    public GameObject congratulationsPanel;
    public Button continueButton;
    public GameObject dungeonfinisher;

    [Header("Dungeon Settings")]

    public StoryLockController storyLockController;
    public int dungeonIndex;
    public int shardIndex;
    // public string itemClaimedKey = "RewardClaimed";
    public SFXManager sfxManager;
    private string itemClaimedKey => $"RewardClaimed_{dungeonIndex}";

    void Start()
    {
        Debug.Log("[BossDungeonController] Start called. Hiding goldRewardPanel.");
        goldRewardPanel.SetActive(false);
        gameOverPanel.SetActive(false);
        winPanel.SetActive(false);
        congratulationsPanel.SetActive(false);
        shardCollectionPanel.SetActive(false);

        // if (PlayerPrefs.GetInt("ShardCollected", 0) == 1 && shardOn != null)
        // {
        //     shardOn.SetActive(true);
        // }
        // else
        // {
        //     shardOn.SetActive(false);
        // }
        // Show collected shards
        for (int i = 0; i < shardOnImages.Length; i++)
        {
            if (PlayerPrefs.GetInt($"ShardCollected_{i}", 0) == 1)
            {
                shardOnImages[i].SetActive(true);
            }
            else
            {
                shardOnImages[i].SetActive(false);
            }
        }

    }

    void Update()
    {
        // Only react if the game is active and the specific boss is gone
        if (gameActive && spawnedBoss == null)
        {
            Debug.Log("[BossDungeonController] Boss defeated, showing congratulations panel after delay.");
            StartCoroutine(ShowCongratulationsPanelAfterDelay());
            gameActive = false; // Prevent multiple triggers

        }
    }

    // Call this when you spawn the boss
    public void RegisterSpawnedBoss(GameObject boss)
    {
        Debug.Log("[BossDungeonController] Boss registered.");
        spawnedBoss = boss;
        gameActive = true;
    }

    IEnumerator ShowCongratulationsPanelAfterDelay()
    {
        yield return new WaitForSeconds(1f);

        HideAllPanels();
        if (congratulationsPanel != null) congratulationsPanel.SetActive(true);
        if (continueButton != null) continueButton.gameObject.SetActive(true);

        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(() =>
        {
            continueButton.gameObject.SetActive(false);
            StartCoroutine(ShowRemainingRewardPanels());
        });
    }

    IEnumerator ShowRemainingRewardPanels()
    {
        int claimedStatus = PlayerPrefs.GetInt(itemClaimedKey, 0);

        // 1. Show Shard Collection Panel (Only on first clear)
        if (claimedStatus == 0 && shardCollectionPanel != null)
        {
            HideAllPanels();
            shardCollectionPanel.SetActive(true);
            yield return WaitForPanelOrDelay(shardCollectionPanel);

            // // Activate Shard
            // if (shardOn != null)
            // {
            //     PlayerPrefs.SetInt("ShardCollected", 1);
            //     PlayerPrefs.SetInt(itemClaimedKey, 1);
            //     PlayerPrefs.Save();
            //     shardOn.SetActive(true);
            // }
            // Collect the current shard
            string shardKey = $"ShardCollected_{shardIndex}";
            PlayerPrefs.SetInt(shardKey, 1);
            PlayerPrefs.SetInt(itemClaimedKey, 1);
            PlayerPrefs.Save();

            if (shardIndex >= 0 && shardIndex < shardOnImages.Length)
            {
                shardOnImages[shardIndex].SetActive(true);
            }
        }

        // 2. Show Congratulations Panel
        HideAllPanels();
        if (congratulationsPanel != null) congratulationsPanel.SetActive(true);
        yield return WaitForPanelOrDelay(congratulationsPanel);

        // 3. Show Win Panel (Always)
        HideAllPanels();
        if (winPanel != null) winPanel.SetActive(true);
        yield return WaitForPanelOrDelay(winPanel);

        // 4. Show Gold Panel (only on repeat runs)
        if (claimedStatus == 1 && goldRewardPanel != null)
        {
            HideAllPanels();
            goldRewardPanel.SetActive(true);
            yield return WaitForPanelOrDelay(goldRewardPanel);
        }

        EndGame(true);
    }



    IEnumerator WaitForPanelOrDelay(GameObject panel)
    {
        if (panel == null) yield break;

        Animator anim = panel.GetComponent<Animator>();
        if (anim != null)
        {
            while (anim.IsInTransition(0))
                yield return null;

            AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
            float timer = 0f;
            while (timer < state.length)
            {
                timer += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(1.5f);
        }
    }

    void HideAllPanels()
    {
        if (congratulationsPanel != null) congratulationsPanel.SetActive(false);
        if (shardCollectionPanel != null) shardCollectionPanel.SetActive(false);
        // if (shardOn != null) shardOn.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (goldRewardPanel != null) goldRewardPanel.SetActive(false);
    }

    void EndGame(bool won)
    {
        if (won)
        {
            storyLockController.SetStoryCompletionStatus(dungeonIndex, true);
        }
        else
        {
            gameOverPanel.SetActive(true);
            StartCoroutine(FreezeAllEnemies());
        }
    }

    IEnumerator FreezeAllEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemy in enemies)
        {
            var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
            var enemyScript = enemy.GetComponent<Enemy>();
            var animator = enemy.GetComponentInChildren<Animator>();

            if (agent != null) agent.isStopped = true;
            if (enemyScript != null) enemyScript.enabled = false;
            if (animator != null) animator.enabled = false;
        }

        yield return null;
    }
}