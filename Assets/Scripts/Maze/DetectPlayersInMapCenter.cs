using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class DetectPlayersInMapCenter : MonoBehaviour{


    public int playersInCenter = 0;



    void OnTriggerEnter(Collider player){
        if(player.tag == "Player"){
            playersInCenter++;
        }

        if(playersInCenter == 2){
            
        }

        Debug.Log(playersInCenter);
    }

    void OnTriggerExit(Collider player){
        if(player.tag == "Player"){
            playersInCenter--;

        }
    }
}
