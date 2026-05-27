using UnityEngine;
using System.Collections.Generic;

public class GuestUsernameGenerator : MonoBehaviour
{
    [Header("Username Settings")]
    [SerializeField] private string prefix = "Guest";
    [SerializeField] private bool addRandomNumber = true;
    [SerializeField] private bool addRandomWord = true;
    [SerializeField] private int numberMin = 100;
    [SerializeField] private int numberMax = 9999;

    [Header("Player Data")]
    [SerializeField] private PlayerData playerData;
    
    [Header("Word Lists")]
    [SerializeField] private List<string> adjectives = new List<string>
    {
        "Brave", "Swift", "Clever", "Mighty", "Noble", "Wild", "Calm", "Bold",
        "Bright", "Dark", "Fierce", "Gentle", "Happy", "Lucky", "Quick", "Silent"
    };
    
    [SerializeField] private List<string> nouns = new List<string>
    {
        "Wolf", "Eagle", "Tiger", "Dragon", "Phoenix", "Knight", "Wizard", "Ranger",
        "Explorer", "Voyager", "Traveler", "Warrior", "Hunter", "Scout", "Mage", "Rogue"
    };
    
    [SerializeField] private List<string> animals = new List<string>
    {
        "Lion", "Tiger", "Bear", "Fox", "Hawk", "Owl", "Raven", "Falcon",
        "Panther", "Leopard", "Cheetah", "Wolf", "Eagle", "Horse", "Deer", "Hound"
    };
    
    private static GuestUsernameGenerator instance;
    private HashSet<string> usedUsernames = new HashSet<string>();
    
    private void Awake()
    {
        if (playerData == null)
        {
            playerData = FindObjectOfType<PlayerData>();
        }

        // Singleton pattern for global access
        if (instance == null)
        {
            instance = this;
           DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Generate a unique guest username
    /// </summary>
    public string GenerateGuestUsername()
    {
        string username;
        int attempts = 0;
        int maxAttempts = 100;
        
        do
        {
            username = BuildUsername();
            attempts++;
            
            if (attempts >= maxAttempts)
            {
                Debug.LogWarning("Max attempts reached, using timestamp-based username");
                username = $"{prefix}_{System.DateTime.Now.Ticks}";
                break;
            }
            
        } while (usedUsernames.Contains(username));
        
        usedUsernames.Add(username);
        // SyncPlayerDataName(username); // Uncomment if you have a PlayerData instance available
        return username;
    }
    
    
    /// <summary>
    /// Build the username based on settings
    /// </summary>
    private string BuildUsername()
    {
        List<string> parts = new List<string>();
        
        // Add prefix
        parts.Add(prefix);
        
        // Add random word combinations
        if (addRandomWord)
        {
            int wordType = Random.Range(0, 3);
            
            switch (wordType)
            {
                case 0: // Adjective + Noun
                    parts.Add(GetRandomWord(adjectives));
                    parts.Add(GetRandomWord(nouns));
                    break;
                case 1: // Animal only
                    parts.Add(GetRandomWord(animals));
                    break;
                case 2: // Adjective + Animal
                    parts.Add(GetRandomWord(adjectives));
                    parts.Add(GetRandomWord(animals));
                    break;
            }
        }
        
        // Add random number
        if (addRandomNumber)
        {
            int randomNum = Random.Range(numberMin, numberMax + 1);
            parts.Add(randomNum.ToString());
        }
        
        // Join parts with underscores
        return string.Join("_", parts);
    }
    
    /// <summary>
    /// Get a random word from a list
    /// </summary>
    private string GetRandomWord(List<string> wordList)
    {
        if (wordList == null || wordList.Count == 0)
            return "";
        
        return wordList[Random.Range(0, wordList.Count)];
    }
    
    /// <summary>
    /// Generate username with specific style
    /// </summary>
    public string GenerateUsernameWithStyle(UsernameStyle style)
    {
        switch (style)
        {
            case UsernameStyle.Simple:
                return $"{prefix}_{Random.Range(1000, 10000)}";
                
            case UsernameStyle.AdjectiveAnimal:
                return $"{GetRandomWord(adjectives)}_{GetRandomWord(animals)}_{Random.Range(100, 1000)}";
                
            case UsernameStyle.NounNumber:
                return $"{GetRandomWord(nouns)}_{Random.Range(100, 9999)}";
                
            default:
                return GenerateGuestUsername();
        }
    }
    
    /// <summary>
    /// Clear the used usernames cache (useful for new game sessions)
    /// </summary>
    public void ClearUsedUsernames()
    {
        usedUsernames.Clear();
    }
    
    /// <summary>
    /// Check if username is already taken
    /// </summary>
    public bool IsUsernameTaken(string username)
    {
        return usedUsernames.Contains(username);
    }
    
    /// <summary>
    /// Manually register a username (for external usernames)
    /// </summary>
    public void RegisterUsername(string username)
    {
        if (!string.IsNullOrEmpty(username))
        {
            usedUsernames.Add(username);
           // SyncPlayerDataName(username);
        }
    }
}

/// <summary>
/// Different styles of username generation
/// </summary>
public enum UsernameStyle
{
    Simple,
    AdjectiveAnimal,
    NounNumber
}