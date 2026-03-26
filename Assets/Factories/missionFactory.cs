using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public abstract class MissionFactory : MonoBehaviour
{
    public int missionId {get; private set;}
    public bool isFinished;

    protected void FinishQuest(){

        if (!isFinished){
            isFinished = true;

            Destroy(this.gameObject);
        }
    }
    
}