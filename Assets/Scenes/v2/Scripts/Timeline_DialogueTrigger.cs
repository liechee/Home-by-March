using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;



public class Timeline_DialogueTrigger : MonoBehaviour
{
    public PlayableDirector director;
    public DialogueController dialogueController;
    public DialogueText dialogueData; // Drag your dialogue asset or data here


    public void TriggerDialogue()
    {
        if (director == null || dialogueController == null || dialogueData == null)
        {
            Debug.LogError("Missing reference in Timeline_DialogueTrigger!");
            return;
        }

        // Subscribe to dialogue end
        dialogueController.OnDialogueEnd += ResumeTimeline;

        director.Pause();
        dialogueController.DisplayDialogue(dialogueData);
    }

    private void ResumeTimeline()
    {
        director.Play();

        // Clean up the event to avoid duplicates
        dialogueController.OnDialogueEnd -= ResumeTimeline;
    }
}
