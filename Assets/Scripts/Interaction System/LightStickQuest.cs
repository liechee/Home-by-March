using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightStickQuest : NPC, ITalkable
{
    [SerializeField] private DialogueText dialogue;
    [SerializeField] private DialogueController dialogueController;
    public override void Interact()
    {
        Talk(dialogue);
    }

    public void Talk(DialogueText dialogue)
    { 
        dialogueController.DisplayDialogue(dialogue);
    }
}
