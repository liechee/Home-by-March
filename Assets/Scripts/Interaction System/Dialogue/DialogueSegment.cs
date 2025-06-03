using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DialogueSegment
{
    [TextArea(3, 10)]
    public List<string> lines;
}
