using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HomeByMarch;

public class Level : MonoBehaviour
{
    [SerializeField] public TMP_Text levelText;

    private UserLevel userLevel;

    void Start()
    {
        userLevel = FindObjectOfType<UserLevel>();
    }


    void Update()
    {
        if (userLevel != null && userLevel.levelText)
        {
            levelText.text = userLevel.playerData.level.ToString();
        }

    }

}