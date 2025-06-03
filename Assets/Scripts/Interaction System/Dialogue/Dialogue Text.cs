using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Dialogue", menuName = "Dialogue/Dialogue Text")]

public class DialogueText : ScriptableObject
{
    public string NPCName;
    public string PlayerName = "Ezenik";

    public DialogueSegment[] DialogueLines;
    public DialogueSegment[] ReturnLines;

}
