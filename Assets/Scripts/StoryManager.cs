using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class StoryManager : MonoBehaviour
{
    public Image images;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public string[] sceneNames;
    public GameObject loadingScreen;
    public Slider progressBar;
    private int lastClickedIndex = -1;
    private float lastClickTime = 0f;
    private float clickResetTime = 2f;
    private int currentIndex = 0;

    private Sprite[] newImages;

    private void Start()
    {
        newImages = new Sprite[]
        {
        //The Beginning - "The Call to Journey"
        LoadSprite("stories/part 1 sub 1"),
        LoadSprite("stories/homebymarch"),
        LoadSprite("stories/homebymarch"),  

         //The Trials - "The Rising Storm"
        LoadSprite("stories/1"),
        LoadSprite("stories/homebymarch"),
        LoadSprite("stories/homebymarch"),  

        //The Resolution - "Homeward Bound"
        LoadSprite("stories/3"),
        LoadSprite("stories/homebymarch"),
        LoadSprite("stories/homebymarch")
        };
    }
    private Sprite LoadSprite(string path)
    {
        // Path should be relative to the Resources folder, e.g., "stories/5"
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite == null)
        {
            Debug.LogError($"Failed to load sprite at path: {path}");
        }
        return sprite;
    }

    private string[] newTMPTexts = new string[]
    {
        //The Beginning - "Compass"
        "A common peasant picks up a compass’ needle and is whisked away to a land far from home.\n\n\"When you are called for something greater, do you answer? Or do you remain still, perpetuating your excuses?\"",
        "The peasant discovers that the weight of the world is in their hands. In spite of the pressure, they agree.\n\n\"Do I even get to go home? Am I trudging along this lengthy quest out of blind faith that I'll be back?\"",
        "The world the peasant claims to save brought them wealth, but isolates them from the people they claim to be saving.\n\n\"Why couldn’t I live a peaceful life like the others?\"",

        //A THOUSAND MILES
        "Distrust festers in our hero and their companion as they are told a great evil shall come and stop them specifically.\n\n\"What in the world are you thinking, putting me in charge of all of this?\"",
        "Our hero is told of the nature of their assailant, and the revelation nearly causes them to desert their mission.\n\n\"When you run out of what you have, do you still wish to give to those with less?\"",
        "Our hero approaches the peak of the highest mountain and the end of the longest river.\n\n\"The cunning waste their pains; The wise men vex their brains; But the simpleton, who seeks no gains, with belly full, he wanders free as drifting boat upon the sea.\"",

        //MARCH
        "Izenik now knows the full weight of their quest and the true consequences of failure. they approach the lair of the commander to finally begin the end.\n\n\"I may not be the hero from chivalry, but I shall gallop on.\"",
        "In which is discovered the full truth behind the eater of worlds, and Izenik's last stand against the overwhelming force.\n\n\"For I wish to see the world I love once more. I ask for strength one last time.\"",
        "The compass is no more. the ender of worlds towers before iznik.\n\n\"There's no place like home.\""
    };

    private string[] newTMPTitles = new string[]
    {
        //The Beginning - "Compass"
        "INERTIA",
        "POT OF KNOWLEDGE",
        "THE FIRST STEP",

        //A THOUSAND MILES
        "AT EACH OTHER’S THROATS",
        "UNCROSSABLE WALL",
        "DREAM OF THE CELESTIAL CHAMBER",

        //MARCH
        "A FUTURE UNCERTAIN",
        "A PAST ONE CANNOT RETURN TO",
        "WHEN ALL HOPE IS LOST"
    };

    public void Next()
    {
        if (newTMPTexts != null && newTMPTexts.Length > 0)
        {
            currentIndex = (currentIndex + 3) % newTMPTexts.Length;
            UpdateText();
        }
        else
        {
            Debug.LogWarning("newTMPTexts array is null or empty.");
        }
    }

    public void Previous()
    {
        if (newTMPTexts != null && newTMPTexts.Length > 0)
        {
            currentIndex = (currentIndex - 3 + newTMPTexts.Length) % newTMPTexts.Length;
            UpdateText();
        }
        else
        {
            Debug.LogWarning("newTMPTexts array is null or empty.");
        }
    }

    public void OnButtonClick(int index)
    {
        // click is consecutive
        if (index == lastClickedIndex && Time.time - lastClickTime <= clickResetTime)
        {
            // Load the scene associated with the button
            if (sceneNames != null && index < sceneNames.Length && !string.IsNullOrEmpty(sceneNames[index]))
            {
                StartCoroutine(LoadSceneAsync(sceneNames[index]));
            }
            else
            {
                Debug.LogWarning($"No scene assigned for index {index}");
            }
        }
        else
        {
            // Display text associated with the button
            if (index < newTMPTexts.Length && index < newTMPTitles.Length && index < newImages.Length)
            {
                titleText.text = newTMPTitles[index];
                bodyText.text = newTMPTexts[index];
                images.sprite = newImages[index];
            }
            else
            {
                Debug.LogWarning($"No text assigned for index {index}");
            }

            // Update last clicked index and time
            lastClickedIndex = index;
            lastClickTime = Time.time;
        }
    }
    private System.Collections.IEnumerator LoadSceneAsync(string sceneName)
    {
        if (loadingScreen != null)
        {
            loadingScreen.SetActive(true);
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        while (!operation.isDone)
        {
            if (progressBar != null)
            {
                progressBar.value = Mathf.Clamp01(operation.progress / 0.9f);
            }
            yield return null;
        }
    }

    private void UpdateText()
    {
        titleText.text = newTMPTitles[currentIndex];
        bodyText.text = newTMPTexts[currentIndex];
        images.sprite = newImages[currentIndex];
    }
}