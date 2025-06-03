using System.Collections;
using UnityEngine;
using HomeByMarch;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;          // Enemy prefab
    public Transform[] spawnPoints;         // Array of spawn points
    public float triggerRange = 15f;        // Range to trigger a spawn
    public SFXManager sfxManager;           // SFX reference
    private BossDungeonController bossDungeonController; // Reference to the boss dungeon controller

    private Transform player;               // Player transform
    private bool[] hasSpawned;              // Track if each spawn point already triggered
    [SerializeField] private bool isBoss = false;                  // Flag to check if the enemy is a boss

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        // Initialize spawn tracking
        hasSpawned = new bool[spawnPoints.Length];
        bossDungeonController = FindObjectOfType<BossDungeonController>();
    }

    void Update()
    {
        if (player == null) return;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (!hasSpawned[i] && Vector3.Distance(player.position, spawnPoints[i].position) <= triggerRange)
            {
                StartCoroutine(SpawnEnemyAt(spawnPoints[i].position));
                hasSpawned[i] = true;
            }
        }
    }

    IEnumerator SpawnEnemyAt(Vector3 position)
    {
        Debug.Log("Spawning enemy at: " + position);
        GameObject enemy = Instantiate(enemyPrefab, position, Quaternion.identity);

        Enemy enemyScript = enemy.GetComponent<Enemy>();
        if (enemyScript != null && sfxManager != null)
        {
            enemyScript.SFXManager = sfxManager;

        }
        if (bossDungeonController != null)
        {
            if (isBoss == true)
            { bossDungeonController.RegisterSpawnedBoss(enemy); }
        }

        yield return null;
    }
}
