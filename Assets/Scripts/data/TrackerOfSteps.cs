using TMPro;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine.Android;
using UnityEngine.InputSystem;

public class TrackerOfSteps : MonoBehaviour
{
    [SerializeField] TMP_Text messageText, DEBUGTEXT, update;

    int stepOffset;
    string stepJsonFilePath;
    int totalNumberOfSteps;
    int previousTotalNumberOfSteps;
    private int numberOfSteps = 0;
    private int loadedNumberOfSteps = 0;


    void Start()
    {
        InitializeStepCounter();
        DEBUGTEXT.text = "Permission: " + (AndroidRuntimePermissions.RequestPermission("android.permission.CAMERA")).ToString();
    }

    void Update()
    {
        if (Application.isEditor) { return; }

        numberOfSteps = StepCounter.current.stepCounter.ReadValue();
        DEBUGTEXT.text = "Number of steps: " + numberOfSteps.ToString();
        if (stepOffset == 0)
        {
            stepOffset = numberOfSteps;
            DEBUGTEXT.text = ("Step offset " + stepOffset);
        }
        else
        {
            totalNumberOfSteps = loadedNumberOfSteps + numberOfSteps - stepOffset;
            if (totalNumberOfSteps > previousTotalNumberOfSteps)
            {
                updateSteps();
                saveStepData();
            }
        }


    }

    void InitializeStepCounter()
    {
        stepJsonFilePath = Application.persistentDataPath + "/stepData.json";

        // Enable step counter and set frequency
        InputSystem.EnableDevice(StepCounter.current);
        StepCounter.current.samplingFrequency = 60;
        update.text = ("HZ " + StepCounter.current.samplingFrequency.ToString());

        loadStepData();
        messageText.text = "Now tracking steps. If you're still seeing this, something went wrong.";
        updateSteps();
    }

    void updateSteps()
    {
        previousTotalNumberOfSteps = totalNumberOfSteps;
        messageText.text = "Steps: " + totalNumberOfSteps.ToString();
    }

    void saveStepData()
    {
        StepData data = new StepData();
        data.numberOfSteps = totalNumberOfSteps;

        string stepCountString = JsonUtility.ToJson(data);
        System.IO.File.WriteAllText(stepJsonFilePath, stepCountString);
    }

    void loadStepData()
    {
        if (System.IO.File.Exists(stepJsonFilePath))
        {
            string stringCountJson = System.IO.File.ReadAllText(stepJsonFilePath);
            loadedNumberOfSteps = JsonUtility.FromJson<StepData>(stringCountJson).numberOfSteps;
        }
        else
        {
            loadedNumberOfSteps = 0;
        }
    }


}

// Data class for storing step