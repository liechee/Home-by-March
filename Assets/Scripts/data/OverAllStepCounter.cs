using TMPro;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine.Android;
using UnityEngine.InputSystem;
using Repforge.StepCounterPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

public class OverallStepCounter : MonoBehaviour{

    public StepData stepData;
    public int overallSteps;
    public string stepDataJsonFilePath;
    public int overallStepsBeforeToday;
    
    // //for debug purposes
    // public TMP_Text overallStepsText;
    // public TMP_Text overallStepsBeforeTodayText;
    

    void Awake(){
        stepDataJsonFilePath = Application.persistentDataPath + "/stepData.json";

        LoadStepData();
        GetOverallSteps();
        
    }

    void Update(){
        GetOverallSteps();
        // overallStepsText.text = "Overall steps: " +  overallSteps;
        // overallStepsBeforeTodayText.text = "Overall steps before today: " + overallStepsBeforeToday;
    }

    public void GetOverallSteps(){
        StepCounterRequest request = new StepCounterRequest();
        if (string.IsNullOrEmpty(stepData.registrationTime) || string.IsNullOrEmpty(stepData.lastSaveTime))
        {
            // Debug.LogWarning("Registration time or last save time is null or empty.");
            return;
        }
        DateTime registrationTime = DateTime.Parse(stepData.registrationTime).Date;
        DateTime lastSaveTime = DateTime.Parse(stepData.lastSaveTime).Date;
        
        if (registrationTime == lastSaveTime){
            request.Since(DateTime.Today).OnQuerySuccess((stepCount) => overallSteps = stepCount).Execute();
            // if first day, overall steps = daily steps
        }
        else {
            Debug.Log("days since last save: " + GetDaysSinceLastSave());
            if(GetDaysSinceLastSave() == 1){
                overallStepsBeforeToday = stepData.numberOfSteps; //basically if new day
                Debug.Log("this indicates that there's been one day " + overallStepsBeforeToday);
                request.Since(DateTime.Today).OnQuerySuccess((stepCount) => overallSteps = (overallStepsBeforeToday + stepCount)).Execute(); 
                Debug.Log("current step (1 day since save): " + overallSteps);      
            } if(GetDaysSinceLastSave() >= 11){
                overallStepsBeforeToday = stepData.numberOfSteps;
                request.From(DateTime.Today.AddDays(-1 * GetDaysSinceLastSave())).To(DateTime.Today).OnQuerySuccess((stepCount) => overallStepsBeforeToday = stepCount).Execute();
                request.Since(DateTime.Today).OnQuerySuccess((stepCount) => overallSteps = overallStepsBeforeToday + stepCount).Execute();
                Debug.Log("current step (11+ days since save): " + overallSteps);   
                

            } if (GetDaysSinceLastSave() == 0){
                Debug.Log("current step (0 day since save): " + overallSteps);   
                //should have no changes
                overallSteps = stepData.numberOfSteps;

            }
            if (GetDaysSinceLastSave() >= 2 && GetDaysSinceLastSave() <= 10){
                overallStepsBeforeToday = stepData.numberOfSteps;
                request.From(lastSaveTime).To(DateTime.Today).OnQuerySuccess((stepCount) => overallStepsBeforeToday += stepCount).Execute();
                request.Since(DateTime.Today).OnQuerySuccess((stepCount) => overallSteps = overallStepsBeforeToday + stepCount).Execute();
                Debug.Log("current step (2-10 days since save): " + overallSteps);   

            }
        }
        

        SaveStepData();

        //gets overall steps from before + overall steps from today
    }

    public int GetDaysSinceRegistration(){
        if (string.IsNullOrEmpty(stepData.registrationTime))
        {
            Debug.LogError("Registration time is null or empty.");
            return 0;
        }
        DateTime today = DateTime.Today;
        DateTime registrationTime = DateTime.Parse(stepData.registrationTime).Date;

        TimeSpan daysSinceRegistration = today - registrationTime;
        return daysSinceRegistration.Days;

    }

    public int GetDaysSinceLastSave(){
        if (string.IsNullOrEmpty(stepData.lastSaveTime))
        {
            Debug.LogError("Last save time is null or empty.");
            return 0;
        }
        DateTime today = DateTime.Today;
        DateTime lastSaveTime = DateTime.Parse(stepData.lastSaveTime).Date;

        TimeSpan daysSinceLastSave = today - lastSaveTime;
        return daysSinceLastSave.Days;
    }

    


    
    
    public void SaveStepData(){
        stepData.lastSaveTime = DateTime.Today.ToString();
        stepData.numberOfSteps = overallSteps;
        string stepDataJson = JsonUtility.ToJson(stepData);
        System.IO.File.WriteAllText(stepDataJsonFilePath, stepDataJson);

    }

    public void InitializeStepData(){
        StepCounterRequest request = new StepCounterRequest();
        request.Since(DateTime.Today).OnQuerySuccess((stepCount) => overallSteps = stepCount).Execute();
        stepData.registrationTime = DateTime.Today.ToString();
        stepData.lastSaveTime = DateTime.Today.ToString();

        Debug.Log("reged" + stepData.registrationTime);
        Debug.Log("skibidi" + stepData.lastSaveTime);
        SaveStepData();
        
        


    }



    public void LoadStepData(){
            if(System.IO.File.Exists(stepDataJsonFilePath)){
                string stepDataJson = System.IO.File.ReadAllText(stepDataJsonFilePath);
                stepData = JsonUtility.FromJson<StepData>(stepDataJson);


                Debug.Log("Player Data Loaded");
            }
            else{
                InitializeStepData();
            }

            


        }


    public async void SaveStepDataToCloud(){
        CloudSaver.SaveDataToCloud("stepData", stepData);
    }

    public async void LoadStepDataFromCloud(){
        string stepDataJson = await CloudSaver.LoadDataFromCloud("stepData");

        stepData = JsonUtility.FromJson<StepData>(stepDataJson);
    

    }
}