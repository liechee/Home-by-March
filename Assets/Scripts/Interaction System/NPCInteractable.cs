using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCInteractable : MonoBehaviour
{
    public void Interact()
    {
        NPC[] npcs = FindObjectsOfType<NPC>();

        foreach (NPC npc in npcs)
        {
            if (npc.InteractionDistanceCheck())
            {
                npc.OnInteractButtonClicked();
                break; // Stop after one successful interaction
            }
        }
    }
}
