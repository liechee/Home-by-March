using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class PlayerSave : MonoBehaviour
{
    public Transform playerTransform;
    private Vector3 playerPosition;
    private string positionJsonFilePath;

    private float saveInterval = 5f;
    private float saveTimer = 0f;
    private const string FileName = "playerPositionData.json";



    void Start()
    { positionJsonFilePath = Path.Combine(Application.persistentDataPath, FileName);

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                playerTransform = playerObj.transform;
            else
                Debug.LogError("PlayerSave: No playerTransform assigned and no GameObject with tag 'Player' found!");
        }

        if (File.Exists(positionJsonFilePath))
        {
            LoadPositionData();
            SavePositionData(); // Optional: lock current state
        }

        playerPosition = playerTransform.position;
    }

    void Update()
    {
        saveTimer += Time.deltaTime;

        if (saveTimer >= saveInterval)
        {
            playerPosition = playerTransform.position;
            SavePositionData();
            saveTimer = 0f;
        }

        // Optional: Manual Save on F5
        if (Input.GetKeyDown(KeyCode.F5))
        {
            playerPosition = playerTransform.position;
            SavePositionData();
        }
    }

    void SavePositionData()
    {
        var dataToSave = new PlayerPositionData
        {
            playerXPosition = playerPosition.x,
            playerYPosition = playerPosition.y,
            playerZPosition = playerPosition.z,
            //savedAtTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        string json = JsonUtility.ToJson(dataToSave, true);
        File.WriteAllText(positionJsonFilePath, json);

       // Debug.Log($"[Save] Player Position Saved at {dataToSave.savedAtTime}: ({playerPosition.x}, {playerPosition.y}, {playerPosition.z})");
    }

    void LoadPositionData()
    {
        try
        {
            string json = File.ReadAllText(positionJsonFilePath);
            PlayerPositionData loadedData = JsonUtility.FromJson<PlayerPositionData>(json);

            Vector3 loadedPosition = new Vector3(loadedData.playerXPosition, loadedData.playerYPosition, loadedData.playerZPosition);
            playerTransform.position = loadedPosition;

            Debug.Log($"[Load] Player Position Loaded: ({loadedPosition.x}, {loadedPosition.y}, {loadedPosition.z})");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Failed to load player position: " + e.Message);
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && playerTransform != null)
        {
            playerPosition = playerTransform.position;
            SavePositionData();
        }
    }

    void OnApplicationQuit()
    {
        playerPosition = playerTransform.position;
        SavePositionData();
    }
}


