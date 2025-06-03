// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class DamagePlayer : MonoBehaviour
// {
//     public PlayerHealth pHealth;
//     public float damage;

//     // Start is called before the first frame update
//     void Start()
//     {
//         // Ensure pHealth is assigned, otherwise log an error
//         if (pHealth == null)
//         {
//             Debug.LogError("PlayerHealth reference is not set in DamagePlayer script.");
//         }
//     }

//     // Detect collision with the player
//     private void OnCollisionEnter(Collision other)
//     {
//         // Check if the collided object has the tag "Player"
//         if (other.gameObject.CompareTag("Player"))
//         {
//             // Apply damage to player's health
//             pHealth.currentHealth -= damage;
//             pHealth.UpdateHealthBar();
//             pHealth.UpdateHealthBar();
//             Debug.Log("Player took damages: " + damage);
//         }
//     }
// }
