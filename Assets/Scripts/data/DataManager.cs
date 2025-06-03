// using System;
// using System.IO;
// using UnityEngine;

// public class SaveDataManager : MonoBehaviour
// {
//     private string stepJsonFilePath;

//     private void Awake()
//     {
//         stepJsonFilePath = Application.persistentDataPath + "/stepData.json";
//     }

//     // Save the step data
//     public void SaveStepData(PlayerData playerData)
//     {
//         string json = JsonUtility.ToJson(playerData);
//         File.WriteAllText(stepJsonFilePath, json);
//     }

//     // Load the step data
//     public PlayerData LoadStepData()
//     {
//         if (File.Exists(stepJsonFilePath))
//         {
//             string json = File.ReadAllText(stepJsonFilePath);
//             return JsonUtility.FromJson<PlayerData>(json);
//         }
//         else
//         {
//             // Return a default PlayerData if no save file exists
//             return new PlayerData
//             {
//                 dailySteps = 0,
//                 overallSteps = 0,
//                 lastTrackedDate = DateTime.Now.ToString()
//             };
//         }
//     }
// }

// [System.Serializable]
// public class PlayerData
// {
//     public int dailySteps;
//     public int overallSteps;
//     public string lastTrackedDate;
// }
