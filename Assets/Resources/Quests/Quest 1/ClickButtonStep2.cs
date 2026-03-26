// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.UI;
// using TMPro;


// public class ClickButtonStep2 : QuestStep
// {

//     [SerializeField] private Button button;
    
//     private int currentClickAmount = 0;
//     private int requiredClickAmount = 10;

//     void Start(){
//         UpdateState();
//     }

//     void OnEnable(){
//         button.onClick.AddListener(ButtonClickForQuest);
//     }

//     void OnDisable(){
//         button.onClick.RemoveListener(ButtonClickForQuest);
//     }
//     public void ButtonClickForQuest(){
        

//         Debug.Log("click! take a paus jdfghsg");
//         UpdateState();
//         CompleteQuest();

//     }

//     private void UpdateState(){
//         string state = "";
//         string status = "skibidi toilet";
//         ChangeState(state, status);
//     }



//     protected override void SetQuestStepState(string state){
//        this.currentClickAmount = System.Int32.Parse(state);

//     }
// }


