using System;
using UnityEngine;

public class GameEventsManager : MonoBehaviour
{
    public static GameEventsManager instance { get; private set; }

    public StepEvents stepEvents;
    public QuestEvents questEvents;



    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("Found more than one Game Events Manager in the scene.");
        }

        instance = this;


        // initialize all events
        stepEvents = new StepEvents(); 
        questEvents = new QuestEvents();


    }
}