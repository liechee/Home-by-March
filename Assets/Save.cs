using UnityEngine;
using System.IO;

public class SimpleSave : MonoBehaviour
{
    public Transform player;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5))
        {
            Vector3 pos = player.position;
            string json = JsonUtility.ToJson(new Vector3Data { x = pos.x, y = pos.y, z = pos.z });
            File.WriteAllText(Application.persistentDataPath + "/test.json", json);
            Debug.Log("Saved position: " + json);
        }

        if (Input.GetKeyDown(KeyCode.F9))
        {
            string json = File.ReadAllText(Application.persistentDataPath + "/test.json");
            Vector3Data data = JsonUtility.FromJson<Vector3Data>(json);
            player.position = new Vector3(data.x, data.y, data.z);
            Debug.Log("Loaded position: " + json);
        }
    }

    [System.Serializable]
    class Vector3Data { public float x, y, z; }
}

