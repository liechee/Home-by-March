// using UnityEngine;
// using UnityEngine.UI; // Required for Button and UI components
// using System.Collections.Generic; // Required for using List

// public class HealthButton : MonoBehaviour
// {
//     [Header("Health Settings")]
//     public PlayerHealth playerHealth; // Reference to the PlayerHealth script

//     [Header("Button Settings")]
//     public List<Button> healthButtons = new List<Button>(); // List of buttons
//     public List<float> healthReductionAmounts = new List<float>(); // Corresponding health reduction amounts for each button

//     void Start()
//     {
//         // Ensure that each button has a corresponding health reduction value
//         for (int i = 0; i < healthButtons.Count; i++)
//         {
//             int index = i; // Cache the index to avoid closure issues in lambda expressions
//             if (healthButtons[i] != null)
//             {
//                 healthButtons[i].onClick.AddListener(() => OnButtonClick(index));
//             }
//         }
//     }

//     // Method triggered when a button is clicked
//     void OnButtonClick(int buttonIndex)
//     {
//         if (playerHealth != null && buttonIndex < healthReductionAmounts.Count)
//         {
//             playerHealth.OnButtonClickReduceHealth(healthReductionAmounts[buttonIndex]); // Reduce health based on the corresponding button
//         }
//     }
// }
