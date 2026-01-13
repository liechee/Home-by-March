using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ClickButtonStep : QuestStep
{


    int stepcount = 30;
    void OnEnable(){

        

        // button.onClick.AddListener(CompleteQuest);
        //listener

    }

    void OnDisable(){
        // button.onClick.RemoveListener(CompleteQuest);
         //listener
    }



    protected override void SetQuestStepState(string state){
        // when stepcount = 50
    }
}
