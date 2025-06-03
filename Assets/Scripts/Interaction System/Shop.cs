using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shop : NPC, ITalkable
{
    [SerializeField] private DialogueText dialogue;
    [SerializeField] private DialogueController dialogueController;
    [SerializeField] private GameObject shopUI;

    private bool isTalking = false;

    public override void Interact()
    {
        // If currently talking, continue the dialogue
        if (isTalking)
        {
            dialogueController.DisplayDialogue(dialogue);
        }
        else
        {
            Talk(dialogue);
        }
    }

    public void Talk(DialogueText dialogue)
    {
        isTalking = true;

        // Only subscribe once per Talk
        dialogueController.OnDialogueEnd += HandleDialogueEnd;
        dialogueController.DisplayDialogue(dialogue);
    }

    private void HandleDialogueEnd()
    {
        isTalking = false;

        // Unsubscribe to avoid multiple triggers
        dialogueController.OnDialogueEnd -= HandleDialogueEnd;

        // Only open the shop UI if this is the correct shop NPC
        OpenShop();
    }

    private void OpenShop()
    {
        if (shopUI != null)
        {
            shopUI.SetActive(true);
            Debug.Log("Opening shop for: " + gameObject.name);
        }
    }
}
