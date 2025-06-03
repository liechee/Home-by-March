using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class exitButton : MonoBehaviour
{
   public void exitGame()
    {
        Application.Quit();
        Debug.Log("Existing Game");
    }
    
}
