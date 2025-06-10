using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class videoCutscene : MonoBehaviour
{
    // private MusicManager musicManager;
    private VideoPlayer videoPlayer;

    void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();
    }

    void Start()
    {
        // Check if the scene has been run before
        if (PlayerPrefs.GetInt("Cutscene PH", 0) == 0) // == 1 if scene need to be run 
        {
            // First time running the scene
            Debug.Log("Running the scene for the first time.");
            
            // Start the cutscene
            videoPlayer.Play();
            videoPlayer.loopPointReached += EndReached; // Subscribe to the event when video ends
        }
        else
        {
            // Scene has already been run, so load the next scene or skip this one
            Debug.Log("Scene has already run before, skipping...");
            stopCutscene();
            LoadNextScene();
        }
    }

    void EndReached(VideoPlayer vp)
    {
        // Set PlayerPrefs to mark that the scene has been run
        PlayerPrefs.SetInt("Cutscene PH", 1);
        PlayerPrefs.Save();  // Save PlayerPrefs to ensure it persists

        // Stop the cutscene and load the next scene
        stopCutscene();
        LoadNextScene();
    }

    void stopCutscene()
    {
        videoPlayer.Stop();
    }

    void LoadNextScene()
    {
        SceneManager.LoadSceneAsync("Story 1-1");
        // UnityEngine.SceneManagement.SceneManager.LoadScene("Main Menu 1");
    }
}
