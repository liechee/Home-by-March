using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using UnityEngine.UI;

public class TabsManager : MonoBehaviour
{
    public GameObject[] tabs;   
    public GameObject[] tabButtons;
    public Color InactiveTabColor, ActiveTabColor;

    void Start()
    {
        // Automatically highlight the first tab
        SwitchTab(0);
    }

    public void SwitchTab(int index)
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            if (i == index)
            {
                tabButtons[i].GetComponent<Image>().color = ActiveTabColor;
                tabs[i].SetActive(true);
            }
            else
            {
                tabButtons[i].GetComponent<Image>().color = InactiveTabColor;
                tabs[i].SetActive(false);
            }
        }
    }

}
