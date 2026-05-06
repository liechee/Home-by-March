using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class exitButton : MonoBehaviour
{
    [SerializeField] private GameObject exitPanel;
   
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("Escape key pressed - toggling exit panel.");
            if (exitPanel.activeSelf)
            {
                exitPanel.SetActive(false);
            }
            else
            {
                exitPanel.SetActive(true);
            }
        }
    }
    public void exitGame()
    {
        Application.Quit();
        Debug.Log("Existing Game");
    }
    public void cancelExit()
    {
        exitPanel.SetActive(false);
    }
}
