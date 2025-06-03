using TMPro;
using UnityEngine;
using System.IO;  // For saving the file
using UnityEngine.SceneManagement;  // For loading the next scene

public class InputFieldGrabber : MonoBehaviour
{
    [Header("The TMP Input Field component")]
    [SerializeField] private TMP_InputField inputField;  // Reference to the TMP Input Field

    private string filePath;  // Path to store the JSON file

    private void Start()
    {
        // Set the file path to store the player name as JSON
        filePath = Application.persistentDataPath + "/playerData.json";
    }

    public void GrabFromInputField()
    {
        string inputText = inputField.text;  // Grabbing the text from the TMP Input Field

        // Create a PlayerData object to store the player's name
        PlayerData playerData = new PlayerData();
        playerData.playerName = inputText;

        // Serialize the player data to JSON and save it to a file
        string json = JsonUtility.ToJson(playerData);
        File.WriteAllText(filePath, json);

        Debug.Log("Player name saved as JSON: " + json);  // Log the saved data

        // Load the next scene 
        SceneManager.LoadScene("Exploration 4"); 
    }
    
    [System.Serializable]
    public class PlayerData
    {
        public string playerName;
    }
}
