using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
//quest points are the start/end point of a quest
//require a sphere collider here if player has to be close to the npc to trigger
//if it's a button, then add StartOrEndQuest to the OnPress
public class QuestPoint : MonoBehaviour{

    [SerializeField]private Button progressorButton;

    [Header("Quest")]
    [SerializeField] private QuestInfoSO questInfoForPoint;

    [Header("Config")]
    [SerializeField] private bool isStartPoint = true;
    [SerializeField] private bool isFinishPoint = true;
    private string questId;
    private QuestState currentQuestState;

    private void Awake(){
        questId = questInfoForPoint.id;
    }

    private void OnEnable(){
        GameEventsManager.instance.questEvents.onQuestStateChange += QuestStateChange;
        progressorButton.onClick.AddListener(StartOrEndQuest);
        
        // add npc listener here
    }

    private void OnDisable(){
        GameEventsManager.instance.questEvents.onQuestStateChange -= QuestStateChange;
        progressorButton.onClick.RemoveListener(StartOrEndQuest);
       
        // remove npc listener here
    }


    

    private void StartOrEndQuest(){
        if (currentQuestState.Equals(QuestState.CAN_START) && isStartPoint){
            Debug.Log("should have started");
            GameEventsManager.instance.questEvents.StartQuest(questId);
        } else if (currentQuestState.Equals(QuestState.CAN_FINISH) && isFinishPoint){
            Debug.Log("should have ended");
            GameEventsManager.instance.questEvents.FinishQuest(questId);
        }
    }



    private void QuestStateChange(Quest quest){
        if (quest.info.id.Equals(questId)){
            currentQuestState = quest.state;
            Debug.Log(quest.state);
        }
    }
}