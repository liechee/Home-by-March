using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [System.Serializable]
// public class DialogueSegment
// {
//     [TextArea(5, 10)]
//     public List<string> lines;
// }

public class DialogueController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private float typingSpeed = 30f;
    [SerializeField] private float maxTime = 1f;

    private Queue<string> npcLines = new Queue<string>();
    private Queue<string> playerLines = new Queue<string>();
    private bool conversationEnded;
    private bool isTyping;
    private string currentLine;
    private Coroutine dialogueCoroutine;
    private const string HTML_ALPHA = "<color=#00000000>";

    [SerializeField] private GameObject[] HideUI = null;

    private string npcName;
    private string playerName;
    private bool isNPCsTurn = true;

    public Action OnDialogueEnd;
    public Timeline_DialogueTrigger timelineTrigger;

    private void Start()
    {
        if (HideUI != null && HideUI.Length > 0)
        {
            foreach (GameObject obj in HideUI)
            {
                obj.SetActive(false);
            }
        }
    }

    public void DisplayDialogue(DialogueText dialogue)
    {
        if (npcLines.Count == 0 && playerLines.Count == 0)
        {
            if (!conversationEnded)
            {
                StartConversation(dialogue);
            }
            else if (conversationEnded && !isTyping)
            {
                // If the conversation has ended and the player clicks, end the conversation
                // and hide the dialogue UI.
                EndConversation();
                return;
            }
        }

        if (!isTyping)
        {

            if (isNPCsTurn && npcLines.Count > 0)
            {
                currentLine = npcLines.Dequeue();
                speakerNameText.text = npcName;
            }
            else if (!isNPCsTurn && playerLines.Count > 0)
            {
                currentLine = playerLines.Dequeue();
                speakerNameText.text = playerName;
            }
            else
            {
                // If one queue is empty, fallback to the other
                if (npcLines.Count > 0)
                {
                    currentLine = npcLines.Dequeue();
                    speakerNameText.text = npcName;
                }
                else if (playerLines.Count > 0)
                {
                    currentLine = playerLines.Dequeue();
                    speakerNameText.text = playerName;
                }
                else
                {
                    EndConversation();
                    return;
                }
            }

            isNPCsTurn = !isNPCsTurn;
            dialogueCoroutine = StartCoroutine(TypeDialogueText(currentLine));
        }
        else
        {
            FinishTyping();
        }

        if (npcLines.Count == 0 && playerLines.Count == 0)
        {
            conversationEnded = true;
        }
    }

    private void StartConversation(DialogueText dialogue)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        npcName = dialogue.NPCName;
        playerName = dialogue.PlayerName;

        npcLines.Clear();
        playerLines.Clear();

        foreach (DialogueSegment segment in dialogue.DialogueLines) // Use full name
        {
            foreach (string line in segment.lines)
            {
                npcLines.Enqueue(line);
            }
        }

        foreach (DialogueSegment segment in dialogue.ReturnLines) // Use full name
        {
            foreach (string line in segment.lines)
            {
                playerLines.Enqueue(line);
            }
        }

        isNPCsTurn = true;
        conversationEnded = false;

    }

    private void EndConversation()
    {
        conversationEnded = false;
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
        if (HideUI != null && HideUI.Length > 0)
        {
            foreach (GameObject obj in HideUI)
            {
                obj.SetActive(true); // Re-enable the UI elements
            }
        }
        OnDialogueEnd?.Invoke();
    }

    private IEnumerator TypeDialogueText(string line)
    {
        isTyping = true;
        dialogueText.text = ""; // Clear the dialogue text before typing
        string displayedText = "";
        int alphaIndex = 0;

        foreach (char c in line.ToCharArray())
        {
            alphaIndex++;
            displayedText = line.Substring(0, alphaIndex) + HTML_ALPHA + line.Substring(alphaIndex);
            dialogueText.text = displayedText;
            yield return new WaitForSeconds(maxTime / typingSpeed);
        }

        dialogueText.text = line; // Ensure the full line is displayed at the end
        isTyping = false;
    }

    private void FinishTyping()
    {
        StopCoroutine(dialogueCoroutine);
        dialogueText.text = currentLine;
        isTyping = false;
    }
    public void OnContinueButtonPressed()
    {
        DisplayDialogue(null); // Pass null; it won't restart because dialogue queues are already filled
    }
}
