using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenuUI;

    public static bool GameIsPaused = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (GameIsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
        
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        GameIsPaused = false;
        Debug.Log("resume clicked");
    }

    public void Pause()
    {
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        GameIsPaused = true;
        Debug.Log("pause clicked");
    }

    public void Help()
    {
        Debug.Log("Help");

    }

    public void BacktoMenu()
    {
        Debug.Log("Back to Menu");
        Time.timeScale = 1f;
        SceneManager.LoadScene("Main Screen");
    }
    public void BacktoStorySceen()
    {
        Debug.Log("Back to Menu");
        Time.timeScale = 1f;
        SceneManager.LoadScene("Story Sceen");
    }

}
