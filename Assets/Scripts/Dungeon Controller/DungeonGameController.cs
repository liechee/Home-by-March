using System.Collections;
using TMPro;  // Add the TextMesh Pro namespace
using UnityEngine;
using UnityEngine.UI;  // Add the UI namespace for Button
using HomeByMarch;

public class DungeonGameController : MonoBehaviour
{
    public GameObject enemyPrefab;      // Reference to the enemy prefab
    public Transform[] spawnPoints;     // Array of spawn points for enemies
    //public float timeLimit = 60f;       // Time limit to defeat enemies
    //public int totalEnemiesToDefeat = 10; // Total number of enemies to defeat

    //private int enemiesDefeated = 0;    // Track the number of defeated enemies
    //private int enemiesSpawned = 0;     // Track the number of enemies spawned
    //private float timeRemaining;        // Time remaining in the dungeon
    private bool gameActive = false;    // Flag to track if the game is active

    // References to TextMeshProUGUI components
    //public TMP_Text timerText;          // TMP Text for displaying the timer
    //public TMP_Text enemiesDefeatedText; // TMP Text for displaying defeated enemies
    //public TMP_Text enemiesRemainingText; // TMP Text for displaying remaining enemies
    //public Button startButton;          // Reference to the start button
    public GameObject winPanel;         // Reference to the Win Panel (UI)
    public GameObject challengePanel;   // Reference to the Dungeon Challenge Panel (UI)
    public GameObject goldRewardPanel;  // Reference to the Gold Reward Panel (UI)
    public GameObject gameOverPanel; // Reference to the Game Over Panel

    //public GameObject StoryPanelEnd;

    public GameObject congratulationsPanel; // Reference to the Congratulations Panel (UI)
    public Button continueButton;        // Reference to the Continue Button (UI)
    public GameObject dungeonfinisher;

    // New boolean to choose between time limit or defeating enemies
    public bool useTimeLimit = true;    // Toggle for time limit or not (set in Editor)

    public StoryLockController storyLockController;

    public int dungeonIndex; // Dungeon identifier for story completion 

    public string itemClaimedKey = "RewardClaimed"; // Key for PlayerPrefs

    public float spawnInterval = 2f;    // Interval between enemy spawns

    public SFXManager sfxManager;  // Reference to the SFXManager
    void Start()
    {
        // Add listener to the start button to start the game
        //startButton.onClick.AddListener(StartGame);
        //winPanel.SetActive(false);        // Make sure the win panel is hidden at the start
        //challengePanel.SetActive(true); // Make sure the challenge panel is hidden at the start
        goldRewardPanel.SetActive(false); // Make sure the gold reward panel is hidden at the start
    }

    // This method is called when the Start button is clicked
    void StartGame()
    {
        //challengePanel.SetActive(false);
        gameActive = true;        // Set the game to active
        //enemiesDefeated = 0;      // Reset the defeated enemies count
        //enemiesSpawned = 0;       // Reset the spawned enemies count
        //timeRemaining = timeLimit; // Reset the timer only if time limit is enabled
        UpdateUI();               // Update the UI to reflect the initial state
        StartCoroutine(SpawnEnemies()); // Start spawning enemies
    }

    void Update()
    {
        if (gameActive)
        {
            if (useTimeLimit)
            {
                // Update the timer if time limit is enabled
                //timeRemaining -= Time.deltaTime;
                // if (timeRemaining <= 0)
                // {
                //     timeRemaining = 0;
                //     EndGame(false);  // Time is up, game over
                // }
            }

            UpdateUI();
        }
    }

    // Update UI texts
    void UpdateUI()
    {
        if (useTimeLimit)
        {
            // Display the timer if time limit is enabled
            //timerText.text = Mathf.Ceil(timeRemaining).ToString();
        }
        else
        {
            //timerText.text = "";  // Hide the timer if no time limit
        }

        // Display the number of defeated enemies
        //enemiesDefeatedText.text = enemiesDefeated.ToString() + "/" + totalEnemiesToDefeat;

        // Display the number of remaining enemies
        //int remainingEnemies = totalEnemiesToDefeat - enemiesDefeated;
        //enemiesRemainingText.text = "Remaining: " + remainingEnemies.ToString();
    }

    // This method is called when an enemy is defeated
    public void OnEnemyDefeated()
    {
        //enemiesDefeated++;
        UpdateUI();  

        //if (enemiesDefeated >= totalEnemiesToDefeat)
        {
            StartCoroutine(ShowCongratulationsPanelAfterDelay());
        }
    }

    private IEnumerator ShowCongratulationsPanelAfterDelay()
    {
        yield return new WaitForSeconds(2f); 
        congratulationsPanel.SetActive(true);
        continueButton.onClick.AddListener(() =>
        {
            congratulationsPanel.SetActive(false);
            EndGame(true);  
        });
    }

    // Ends the game, either win or lose
    void EndGame(bool won)
    {
        gameActive = false;


        if (won)
        {

            // Mark the dungeon story as completed
            storyLockController.SetStoryCompletionStatus(dungeonIndex, true); // Replace 'dungeonIndex' with the appropriate dungeon identifier.

            // Check PlayerPrefs for the itemClaimedKey
            int claimedStatus = PlayerPrefs.GetInt(itemClaimedKey, 0);
            if (claimedStatus == 1)
            {
                Debug.Log("Opening Gold Reward Panel");
                //StoryPanelEnd.SetActive(true);
                goldRewardPanel.SetActive(true); // Show the Gold Reward Panel
            }
            else
            {
                Debug.Log("Opening Win Panel");
                //StoryPanelEnd.SetActive(true);
                //winPanel.SetActive(true); // Show the Win Panel
            }
        }
        else
        {
            Debug.Log("Game over! Time's up or enemies not defeated.");
            gameOverPanel.SetActive(true); // shows game over screen
            StartCoroutine(FreezeAllEnemies());
        }
    }

    // Coroutine to spawn enemies at random spawn points
    IEnumerator SpawnEnemies()
    {
        while (gameActive )//&& enemiesSpawned < totalEnemiesToDefeat)
        {
            int spawnIndex = Random.Range(0, spawnPoints.Length);  // Random spawn point
            GameObject enemy = Instantiate(enemyPrefab, spawnPoints[spawnIndex].position, Quaternion.identity);  // Spawn enemy

            // Assign the SFXManager to the enemy's Enemy script
            Enemy enemyScript = enemy.GetComponent<Enemy>();
            if (enemyScript != null && sfxManager != null)
            {
                enemyScript.SFXManager = sfxManager;  // Assign the SFXManager to the enemy script
            }

            //enemiesSpawned++;  // Increment the spawn count
            yield return new WaitForSeconds(spawnInterval);  // Wait before spawning the next enemy (adjust as needed)
        }
    }

    IEnumerator FreezeAllEnemies(){
        {
            
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy"); // searches for all enemies
            
            if (enemies.Length > 0){
                foreach (GameObject enemy in enemies)
                {
                    Debug.Log("you accidentally froze");
                    UnityEngine.AI.NavMeshAgent enemyNavMesh = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>(); // getting the navmesh. if it doesnt, this returns null and the freeze thing wont bug out (hopefully)
                    Enemy enemyScript = enemy.GetComponent<Enemy>(); // gets the enemy script to be disabled on game over
                    Animator animator = enemy.GetComponentInChildren<Animator>(); // the part that makes the enemy move in place. remove that too.
                    enemyNavMesh.isStopped = true; // this should freeze all the enemies in place
                    enemyScript.enabled = false; // stop registering attacks and playing all those sound effects
                    animator.enabled = false; // stop moving. THE WORLD
                }
            } else{
                Debug.Log("no enemies lol");
            }
        }

        yield return 0;
    }


    // This method is called when the player collides with the Dungeon Trigger
    private void OnTriggerEnter(Collider other)
    {
        // Check if the player (tagged as "Player") collided with the trigger
        if (other.CompareTag("Player"))
        {
            //challengePanel.SetActive(true); // Show the Dungeon Challenge Panel
        }
    }
}
