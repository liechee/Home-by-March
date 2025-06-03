using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class QuestLogScrollingList: MonoBehaviour{

    [Header("Components")]
    [SerializeField] private GameObject contentParent;

    [Header("Quest Log Button")]
    [SerializeField] private GameObject questLogButtonPrefab;


    private Dictionary<string, QuestLogButton> idToButtonMap = new Dictionary<string, QuestLogButton>();


    public bool doesButtonExist(Quest quest){
        return idToButtonMap.ContainsKey(quest.info.id);
    }

    public bool isQuestViewable(Quest quest){
        return !(quest.state == QuestState.REQUIREMENTS_NOT_MET | quest.state == QuestState.FINISHED);
    }

    public QuestLogButton CreateScrollListButton(Quest quest, UnityAction pointerClickAction){

        QuestLogButton questLogButton = null;
        if (!doesButtonExist(quest) && isQuestViewable(quest)){
            questLogButton = InstantiateQuestLogButton(quest, pointerClickAction);
        } 
        else {
            if(isQuestViewable(quest)){
            questLogButton = idToButtonMap[quest.info.id];
            //should only show quests you can view (met requirements, in progress, and claimable)
            }
        }


        return questLogButton;
    }
    private QuestLogButton InstantiateQuestLogButton(Quest quest, UnityAction pointerClickAction){
        QuestLogButton questLogButton = Instantiate(questLogButtonPrefab, contentParent.transform).GetComponent<QuestLogButton>();

        questLogButton.gameObject.name = quest.info.id + "_button";
        questLogButton.Initialize(quest.info.displayName, pointerClickAction);


        idToButtonMap[quest.info.id] = questLogButton;
  

        return questLogButton;

    }

    public void DestroyQuestLogButton(Quest quest){
        QuestLogButton questLogButton = idToButtonMap[quest.info.id];
        Destroy(questLogButton.gameObject);
        idToButtonMap.Remove(quest.info.id);
    }
}