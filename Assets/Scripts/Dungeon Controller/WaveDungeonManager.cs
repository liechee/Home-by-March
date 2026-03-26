using System.Collections;
using TMPro;
using UnityEngine;

public class WaveDungeonManager : MonoBehaviour
{
    [Header("Wave Settings")]
    public GameObject enemyPrefab;      // Enemy prefab to spawn
    public Transform[] spawnPoints;     // Array of spawn points
    public int totalWaves = 5;          // Total waves to complete
    public int initialEnemies = 3;      // Number of enemies in the first wave
    public float timeBetweenWaves = 5f; // Time delay before the next wave

    [Header("Player Settings")]
    public int playerHealth = 100;      // Player's starting health
    public TMP_Text playerHealthText;   // TMP Text for displaying player health

    [Header("UI Elements")]
    public TMP_Text waveText;           // TMP Text to display the current wave
    public TMP_Text enemiesRemainingText; // TMP Text for remaining enemies

    private int currentWave = 0;        // Tracks the current wave number
    private int enemiesRemaining = 0;   // Number of enemies remaining in the current wave
    private bool waveActive = false;    // Tracks if the wave is active

    private Camera mainCamera;          // Reference to the Main Camera

    void Start()
    {
        mainCamera = Camera.main; // Cache the MainCamera
        UpdateUI();
        StartCoroutine(StartNextWave());
    }

    void UpdateUI()
    {
        // Update UI texts
        waveText.text = "Wave: " + currentWave + "/" + totalWaves;
        enemiesRemainingText.text = "Enemies Remaining: " + enemiesRemaining;
        playerHealthText.text = "Health: " + playerHealth;
    }

    public void OnEnemyDefeated()
    {
        enemiesRemaining--;

        // Update UI
        UpdateUI();

        if (enemiesRemaining <= 0 && waveActive)
        {
            waveActive = false;
            if (currentWave >= totalWaves)
            {
                EndGame(true); // All waves completed, game won
            }
            else
            {
                StartCoroutine(StartNextWave());
            }
        }
    }

    public void TakeDamage(int damage)
    {
        playerHealth -= damage;
        if (playerHealth <= 0)
        {
            playerHealth = 0;
            EndGame(false); // Player health depleted, game over
        }

        UpdateUI();
    }

    void EndGame(bool won)
    {
        waveActive = false;
        if (won)
        {
            Debug.Log("You won! All waves completed!");
        }
        else
        {
            Debug.Log("Game Over! You died.");
        }
        // Additional game-ending logic here (e.g., show end screen, disable gameplay)
    }

    IEnumerator StartNextWave()
    {
        if (currentWave > 0) // If not the first wave, add a delay
        {
            yield return new WaitForSeconds(timeBetweenWaves);
        }

        currentWave++;
        enemiesRemaining = initialEnemies + (currentWave - 1) * 2; // Increase enemy count each wave
        waveActive = true;

        UpdateUI();

        // Spawn enemies
        for (int i = 0; i < enemiesRemaining; i++)
        {
            int spawnIndex = Random.Range(0, spawnPoints.Length);
            GameObject spawnedEnemy = Instantiate(enemyPrefab, spawnPoints[spawnIndex].position, Quaternion.identity);

            // Assign the MainCamera to the Billboard script in the enemy prefab
            Billboard billboard = spawnedEnemy.GetComponentInChildren<Billboard>();
            if (billboard != null && mainCamera != null)
            {
                billboard.targetCamera = mainCamera;
            }

            yield return new WaitForSeconds(0.5f); // Spawn delay between enemies
        }
    }
}
