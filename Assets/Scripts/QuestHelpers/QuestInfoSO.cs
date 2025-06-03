using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// to make a new quest, right click on the RESOURCES\QUESTS FOLDER
// new -> scriptable object -> questinfoso
// rename file to the quest name you want
// yipee 
[CreateAssetMenu(fileName = "QuestInfoSO", menuName = "ScriptableObjects/QuestInfoSO", order = 1)]
public class QuestInfoSO : ScriptableObject{
    [field: SerializeField]public string id {get; private set;}
    
    [Header("Image")]
    public Sprite questImage;
    
    [Header("General")]
    public string displayName;
    public string description; // Added quest description

    [Header("Requirements")]
    public int levelRequired;
    public int totalStepsRequired;
    public List<QuestInfoSO> questPrerequesites = new();

    [Header("Steps")]
    public List<GameObject> questStepPrefabs = new();

    [Header("Rewards")]
    public int goldReward;
    public int experienceReward;
    // public Item itemReward; 
    // add item rewards when the inventory is implemented

    private void OnValidate(){
        #if UNITY_EDITOR
        id = this.name;
        UnityEditor.EditorUtility.SetDirty(this);
        #endif
    }
}