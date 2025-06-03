using UnityEngine;

public class moveToGame2 : MonoBehaviour
{
    private void Awake()
    {
        // Subscribe to GameManager's onGameStateChanged event
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onGameStateChanged += GameManagerGameStateChanged;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from GameManager's onGameStateChanged event
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onGameStateChanged -= GameManagerGameStateChanged;
        }
    }

    private void GameManagerGameStateChanged(GameManager.GameState state)
    {
        // Handle state change if needed
    }

    // Start is called before the first frame update
    private void Start()
    {
        Debug.Log("moveToGame script is active and Start method is called.");
    }

    // Update is called once per frame
    private void Update()
    {
        // Update logic if needed
    }

    // Public method to change game state
    public void OnButtonClick()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeGameState(GameManager.GameState.Exploration); // Set desired state
        }
    }
}
