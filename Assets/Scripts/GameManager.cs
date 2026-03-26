using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum GameState { MenuScreen, Loading, Exploration, Dungeon, Battle, Dialog }
    public GameState currentState;

    public delegate void OnGameStateChanged(GameState newState);
    public event OnGameStateChanged onGameStateChanged;

    private void Awake()
    {
        Debug.Log("GameManager Awake is called.");

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        InitializeGame();
    }

    private void InitializeGame()
    {
        currentState = GameState.MenuScreen;
        Debug.Log("Game initialization complete. Current state is MenuScreen.");
    }

    public void ChangeGameState(GameState newState)
    {
        Debug.Log($"Game state changing from {currentState} to {newState}");

        // Start loading process
        StartCoroutine(LoadState(newState));
    }

    private System.Collections.IEnumerator LoadState(GameState newState)
    {
        // Change to Loading state
        currentState = GameState.Loading;
        onGameStateChanged?.Invoke(GameState.Loading);
        Debug.Log("Current game state is now: Loading");

        // Load the loading scene
        SceneManager.LoadScene("LoadingScene");  // Replace with your loading scene name

        // Wait for one frame to ensure the loading scene is loaded
        yield return null;

        // Simulate or perform actual loading (e.g., loading resources, initializing systems)
        yield return new WaitForSeconds(2.0f); // Replace with actual loading logic if needed

        // Once loading is complete, load the actual scene based on the new state
        switch (newState)
        {
            case GameState.Exploration:
                SceneManager.LoadScene("Exploration"); // Replace with your Exploration scene name
                break;
            // Handle other states and scenes if needed
            default:
                throw new System.ArgumentOutOfRangeException(nameof(newState), newState, null);
        }

        // Finally, change to the desired state after the scene is loaded
        currentState = newState;
        onGameStateChanged?.Invoke(newState);
        Debug.Log($"Current game state is now: {currentState}");
    }
}
