using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Playables;
public class ChapterController : MonoBehaviour
{
    [Header("Chapter Settings")]
    public int chapterIndex = 0;
    public PlayableDirector timelineDirector;
    public Transform player;
    public string chapterKeyPrefix = "ChapterPlayed_";
    public string positionKeyPrefix = "PlayerPos_";

    void Start()
    {
        if (player == null)
        {
            Debug.LogError("[ChapterManager] Player reference is missing!");
            return;
        }

        LoadPlayerPosition();

        string chapterKey = chapterKeyPrefix + chapterIndex;

        if (PlayerPrefs.GetInt(chapterKey, 0) == 0)
        {
            Debug.Log("[ChapterManager] First time playing chapter, playing timeline.");
            if (timelineDirector != null)
            {
                timelineDirector.gameObject.SetActive(true);
                timelineDirector.Play();
            }

            PlayerPrefs.SetInt(chapterKey, 1);
            PlayerPrefs.Save();
        }
    }

    void OnApplicationQuit()
    {
        SavePlayerPosition();
    }

    void OnApplicationPause(bool pause)
    {
        if (pause)
            SavePlayerPosition();
    }

    public void SavePlayerPosition()
    {
        if (player != null)
        {
            string keyX = positionKeyPrefix + chapterIndex + "_X";
            string keyY = positionKeyPrefix + chapterIndex + "_Y";
            string keyZ = positionKeyPrefix + chapterIndex + "_Z";

            PlayerPrefs.SetFloat(keyX, player.position.x);
            PlayerPrefs.SetFloat(keyY, player.position.y);
            PlayerPrefs.SetFloat(keyZ, player.position.z);
            PlayerPrefs.Save();

            Debug.Log("[ChapterManager] Player position saved.");
        }
    }

    public void LoadPlayerPosition()
    {
        string keyX = positionKeyPrefix + chapterIndex + "_X";
        string keyY = positionKeyPrefix + chapterIndex + "_Y";
        string keyZ = positionKeyPrefix + chapterIndex + "_Z";

        if (PlayerPrefs.HasKey(keyX) && PlayerPrefs.HasKey(keyY) && PlayerPrefs.HasKey(keyZ))
        {
            Vector3 pos = new Vector3(
                PlayerPrefs.GetFloat(keyX),
                PlayerPrefs.GetFloat(keyY),
                PlayerPrefs.GetFloat(keyZ)
            );

            player.position = pos;
            Debug.Log("[ChapterManager] Player position loaded.");
        }
    }
}
