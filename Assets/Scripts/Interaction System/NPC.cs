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
    [SerializeField] private Button interactButton;

    void Awake()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }
        // Ensure interactSprite is hidden at start
        if (interactSprite != null)
            interactSprite.SetActive(false);
        if (interactButton != null)
            interactButton.gameObject.SetActive(false);
    }

    private void Update()
    {
        bool canInteract = InteractionDistanceCheck();

        if (Keyboard.current.eKey.wasPressedThisFrame && canInteract)
        {
            Interact();
            Debug.Log("Interacting with " + gameObject.name);
        }

        // Only show interactSprite if player is near
        if (interactSprite != null)
        {
            interactSprite.SetActive(canInteract);
        }
        if (interactButton != null)
        {
            interactButton.gameObject.SetActive(canInteract); // This toggles visibility
        }
    }

    public abstract void Interact();

    public bool InteractionDistanceCheck()
    {
        if (player == null)
        {
            Debug.LogError("Player reference is missing in NPC script!");
            return false;
        }

        float distance = Vector3.Distance(player.position, transform.position);
        return distance <= interactRange;
    }

    // Method to handle button click
    public void OnInteractButtonClicked()
    {
        if (InteractionDistanceCheck())
        {
            Interact();
            Debug.Log("Interacting with " + gameObject.name);
        }
    }
}
