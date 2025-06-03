using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; 
using System;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    public AudioClip[] musicSounds;
    public AudioSource musicSource;


    [System.Serializable]
    public class SceneMusicMapping
    {
        public string sceneName;
        public AudioClip musicClip;
    }

    public List<SceneMusicMapping> sceneMusicMappings = new List<SceneMusicMapping>();

    private string currentMusicName = "";  

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {

        PlayMusicForCurrentScene();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {

        PlayMusicForCurrentScene();
    }

    private void PlayMusicForCurrentScene()
    {
        
        string currentScene = SceneManager.GetActiveScene().name;

        
        SceneMusicMapping sceneMusic = sceneMusicMappings.Find(mapping => mapping.sceneName == currentScene);

        if (sceneMusic != null)
        {
           
            if (sceneMusic.musicClip != null && sceneMusic.musicClip.name != currentMusicName)
            {
                PlayMusic(sceneMusic.musicClip.name);
                currentMusicName = sceneMusic.musicClip.name;  
            }
        }
        else
        {
            Debug.LogWarning("No music mapped for scene: " + currentScene);
        }
    }

    public void PlayMusic(string name)
    {
        AudioClip s = Array.Find(musicSounds, sound => sound.name == name);
        if (s == null)
        {
            Debug.LogWarning("Music: " + name + " not found!");
        }
        else
        {
            musicSource.clip = s;
            musicSource.Play();
        }
    }

    public void MuteMusic()
    {
        musicSource.mute = !musicSource.mute;
    }

    public void MusicVolume(float volume)
    {
        musicSource.volume = volume;
    }
}