using TMPro;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine.Android;
using UnityEngine.InputSystem;

[System.Serializable]
public class StepData{
  public string lastSaveTime;
  public string registrationTime;
  public int numberOfSteps;
  public int dailySteps; 
  public int overallSteps;

}


public class PlayerPositionData{
  public float playerXPosition;
  public float playerYPosition;
  public float playerZPosition;
  
}

public class DailyQuestProgress {
  public bool[] areDailyQuestsClaimed;
  public string lastResetDate;

  public DailyQuestProgress(int numberOfRewards){

    DateTime currentDate;
    currentDate = DateTime.Now;

    this.areDailyQuestsClaimed = new bool[numberOfRewards];
    this.lastResetDate = currentDate.ToString();
    
  }
}


public class PlayerDataSaver {
    public string playerName;
    public float health;

    public int saveData;
    public int level;

    public int lastSavedLevel;
    public int attack;
    public int defense;

    public float cooldown;

    public float movementSpeed;
    public int gold;
    public double attackSpeed;
}
