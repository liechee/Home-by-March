using TMPro;
using UnityEngine;
using System.IO;  // For saving the file
using UnityEngine.SceneManagement;  // For loading the next scene
using Unity.Services.Authentication.PlayerAccounts.Samples;

public class InputFieldGrabber : MonoBehaviour
{
    [Header("The TMP Input Field component")]
    [SerializeField] private TMP_InputField inputField;  // Reference to the TMP Input Field

    private string filePath;  // Path to store guest-only name JSON

    private void Start()
    {
        // Keep this data separate from authoritative playerData.json used by PlayerData.
        filePath = Application.persistentDataPath + "/guestNameDraft.json";
    }

    public void GrabFromInputField()
    {
        if (inputField == null)
            return;

        string inputText = inputField.text.Trim();  // Grabbing the text from the TMP Input Field
        if (string.IsNullOrEmpty(inputText))
            return;

        // Never overwrite cloud-backed player profile data from this local input helper.
        if (AuthManager1.Instance != null && AuthManager1.Instance.IsSignedIn)
        {
            Debug.LogWarning("[InputFieldGrabber] Ignored name write while signed in.");
            return;
        }

        PlayerPrefs.SetString(AuthManager1.PrefGuestName, inputText);
        PlayerPrefs.Save();

        // Create a guest-only payload to avoid colliding with PlayerData schema.
        GuestNameDraft draft = new GuestNameDraft { guestName = inputText };

        // Serialize guest name to a dedicated local file.
        string json = JsonUtility.ToJson(draft);
        File.WriteAllText(filePath, json);

        Debug.Log("[InputFieldGrabber] Guest name saved locally: " + inputText);

        // Load the next scene 
        SceneManager.LoadScene("Exploration 4"); 
    }
    
    [System.Serializable]
    public class GuestNameDraft
    {
        public string guestName;
    }
}
