// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.UI;

// public class StoryPanel : MonoBehaviour
// {
//     [Header("UI Components")]
//     public Image image; // Manually assign Image in the Inspector
//     public GameObject nextButton; // GameObject for the next button
//     public GameObject previousButton; // GameObject for the previous button
//     public GameObject panel; // The parent panel that will be closed

//     [Header("Story Data")]
//     public List<Sprite> storySprites; // List of story sprites

//     private int currentIndex = 0; // Index of the current story

//     void Start()
//     {
//         // Debugging to check if the Image component is assigned
//         if (image == null)
//         {
//             Debug.LogError("Image component is not assigned in the Inspector.");
//             return;
//         }

//         // Add listeners to buttons
//         nextButton.GetComponent<Button>().onClick.AddListener(NextStory);
//         previousButton.GetComponent<Button>().onClick.AddListener(PreviousStory);

//         // Initialize the story display
//         UpdateStory();
//     }

//     void Update()
//     {
//         // Continuously check if the image needs updating (optional for dynamic story)
//         if (currentIndex >= 0 && currentIndex < storySprites.Count)
//         {
//             UpdateStory();
//         }
//     }

//     private void UpdateStory()
//     {
//         // Ensure the currentIndex is within valid bounds
//         if (currentIndex >= 0 && currentIndex < storySprites.Count)
//         {
//             // Debugging: Log the current index and sprite being assigned
//             Debug.Log("Updating story. Current Index: " + currentIndex);
//             Debug.Log("Assigning sprite: " + (storySprites[currentIndex] != null ? storySprites[currentIndex].name : "null"));

//             // Update the sprite
//             if (storySprites[currentIndex] != null)
//             {
//                 image.sprite = storySprites[currentIndex];
//             }
//             else
//             {
//                 Debug.LogError("The sprite at index " + currentIndex + " is null.");
//             }
//         }
//         else
//         {
//             // If index is out of bounds or lists aren't synchronized
//             Debug.LogWarning("Current index is out of bounds or the story sprites list is empty.");
//         }

//         // Enable/Disable buttons based on index
//         previousButton.GetComponent<Button>().interactable = currentIndex > 0;
//         nextButton.GetComponent<Button>().interactable = currentIndex < storySprites.Count - 1;
//     }

//     public void NextStory()
//     {
//         if (currentIndex < storySprites.Count - 1)
//         {
//             currentIndex++;
//             UpdateStory();
//         }
//         else
//         {
//             // Close the panel if it is the last image
//             Debug.Log("Last photo reached, closing the panel.");
//             panel.SetActive(false);  // Disable the entire panel
//         }
//     }

//     public void PreviousStory()
//     {
//         if (currentIndex > 0)
//         {
//             currentIndex--;
//             UpdateStory();
//         }
//         else
//         {
//             Debug.LogWarning("No previous stories available.");
//         }
//     }
// }
