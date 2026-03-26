// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public abstract class StepMissionFactory : MissionFactory{

//     public int stepsAchieved = 0;
//     public int stepsToAchieve;
    
//     public TrackerOfSteps trackerOfSteps;
//     int questStepOffset;

//     private void onEnable(){
//         questStepOffset = trackerOfSteps.overallSteps;
//     }

//     private void Update(){
//         stepsAchieved = trackerOfSteps.overallSteps - questStepOffset; //REALLY LAGGY. NEED SOMETHING IN GAME MANAGER

//         if (stepsAchieved >= stepsToAchieve){
//             FinishQuest();
//         }
//     } //need something in the game manager to update when there is a step

    


// }