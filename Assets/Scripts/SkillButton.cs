// using UnityEngine;
// using UnityEngine.UI; // Required for Button and UI components
// using System.Collections.Generic; // Required for using List

// public class ButtonScript : MonoBehaviour
// {
//     [Header("Mana Settings")]
//     public PlayerMana playerMana; // Reference to the PlayerMana script

//     [Header("Button Settings")]
//     public List<Button> manaButtons = new List<Button>(); // List of buttons
//     public List<float> manaReductionAmounts = new List<float>(); // Corresponding mana reduction amounts for each button

//     void Start()
//     {
//         // Ensure that each button has a corresponding mana reduction value
//         for (int i = 0; i < manaButtons.Count; i++)
//         {
//             int index = i; // Cache the index to avoid closure issues in lambda expressions
//             if (manaButtons[i] != null)
//             {
//                 manaButtons[i].onClick.AddListener(() => OnButtonClick(index));
//             }
//         }
//     }

//     // Method triggered when a button is clicked
//     // void OnButtonClick(int buttonIndex)
//     // {
//     //     if (playerMana != null && buttonIndex < manaReductionAmounts.Count)
//     //     {
//     //         playerMana.TakeDamage(manaReductionAmounts[buttonIndex]); // Reduce mana based on the corresponding button
//     //     }
//     // }
// }
