using System.Collections;
using System.Collections.Generic;
using HomeByMarch;
using Photon.Pun.Demo.SlotRacer;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public abstract class NPC : MonoBehaviour, IInteractable
{
    [SerializeField] private GameObject interactSprite;
    [SerializeField] private Transform player;
    [SerializeField] private float interactRange = 1f;
    [SerializeField] private LayerMask interactLayer;
    [SerializeField] private Button[] interactButton;


    //[SerializeField] private int colliderCount;
    void Awake()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }
    }



    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    private void Update()
    {

        if (Keyboard.current.eKey.wasPressedThisFrame && InteractionDistanceCheck())
        {
            Interact();
            Debug.Log("Interacting with " + gameObject.name);
        }


        if (interactSprite.gameObject.activeSelf && !InteractionDistanceCheck())
        {
            interactSprite.gameObject.SetActive(false);
        }
        else if (!interactSprite.gameObject.activeSelf && InteractionDistanceCheck())
        {
            interactSprite.gameObject.SetActive(true);
        }

    }

    public abstract void Interact();

    public bool InteractionDistanceCheck()
    {
        // if (Vector3.Distance(player.position, transform.position) <= interactRange)
        // {
        //     return true;
        // }
        // else
        // {
        //     return false;
        // }
         if (player == null)
        {
            Debug.LogError("Player reference is missing in NPC script!");
            return false;
        }

        float distance = Vector3.Distance(player.position, transform.position);
        return distance <= interactRange;
        // Your distance logic here
    }
    // Method to handle button click
    public void OnInteractButtonClicked()
    {
        if (InteractionDistanceCheck())
        {
            // Cancel the player's attack
            Interact();
            Debug.Log("Interacting with " + gameObject.name);
        }
    }

}
