using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.Android;
using UnityEngine.InputSystem;

public class SavePlayerPosition : MonoBehaviour{

    public Transform playerTransform;
    Vector3 playerPosition;
    string positionJsonFilePath;
    

    void Start(){

        positionJsonFilePath = Application.persistentDataPath + "/playerPositionData.json";

        if (System.IO.File.Exists(positionJsonFilePath)){
            LoadPositionData();
        }
        playerPosition = playerTransform.position;
        
    }

    void FixedUpdate(){
        playerPosition = playerTransform.position;
        SavePositionData();

    }

    void SavePositionData(){
       
        PlayerPositionData playerPositionData = new PlayerPositionData();

        playerPositionData.playerXPosition = playerPosition.x;
        playerPositionData.playerYPosition = playerPosition.y;
        playerPositionData.playerZPosition = playerPosition.z;

        Debug.Log("x:" + playerPosition.x + "y:" + playerPosition.y + "z:" + playerPosition.z);

        //same concept as the step count saving
        string playerPositionJson = JsonUtility.ToJson(playerPositionData);


        System.IO.File.WriteAllText(positionJsonFilePath, playerPositionJson);
        Debug.Log("position daved");
        

    }

    void LoadPositionData(){

        string positionJson = System.IO.File.ReadAllText(positionJsonFilePath);
        float xPosition = JsonUtility.FromJson<PlayerPositionData>(positionJson).playerXPosition;
        float yPosition = JsonUtility.FromJson<PlayerPositionData>(positionJson).playerYPosition;
        float zPosition = JsonUtility.FromJson<PlayerPositionData>(positionJson).playerZPosition;

        playerTransform.position = new Vector3(xPosition, yPosition, zPosition);

        Debug.Log("position loaded");

    }
    


}