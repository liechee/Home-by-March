using System;
using UnityEngine;

namespace HomeByMarch
{
    public class GameEventsManager : MonoBehaviour
    {
        public static GameEventsManager instance { get; private set; }

        // public InputEvents inputEvents;
        // public PlayerEvents playerEvents;
        // public GoldEvents goldEvents;
        public StepCountEvents stepCountEvents; // Added StepCountEvents
        // public MiscEvents miscEvents;
        public QuestEvents questEvents;

        private void Awake()
        {
            if (instance != null)
            {
                Debug.LogError("Found more than one Game Events Manager in the scene.");
            }
            instance = this;

            // Initialize all events
            // inputEvents = new InputEvents();
            // playerEvents = new PlayerEvents();
            // goldEvents = new GoldEvents();
            stepCountEvents = new StepCountEvents(); // Initialize StepCountEvents
            // miscEvents = new MiscEvents();
            questEvents = new QuestEvents();
        }
    }
}
