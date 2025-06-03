using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;



public class ClickButtonStep2 : QuestStep

{
    [SerializeField] private Button button;


    void OnEnable(){
        button.onClick.AddListener(CompleteQuest);
        button.onClick.AddListener(() => { Debug.Log("You are clicked button !"); });

    }

    void OnDisable(){
        button.onClick.RemoveListener(CompleteQuest);
        button.onClick.RemoveListener(() => { Debug.Log("You are clicked button !"); });
    }



    protected override void SetQuestStepState(string state){
        ///
    }
}
