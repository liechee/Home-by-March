using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    public Slider musicSlider, sfxSlider;

    private void Start()
    {
        // Load saved volume settings when the game starts
        LoadMusicSettings();
        LoadSFXSettings();

        // Ensure that sliders trigger volume change events in real-time
        if (musicSlider != null)
            musicSlider.onValueChanged.AddListener(delegate { MusicVolume(); });

        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(delegate { SFXVolume(); });
    }

    // Music management
    public void MuteMusic()
    {
        MusicManager.Instance.MuteMusic();
        SaveMusicSettings();
    }

    public void MusicVolume()
    {
        if (musicSlider != null)
        {
            MusicManager.Instance.MusicVolume(musicSlider.value);
            SaveMusicSettings();
        }
    }

    // SFX management
    public void MuteSFX()
    {
        SFXManager.Instance.MuteSFX();
        SaveSFXSettings();
    }

    public void SFXVolume()
    {
        if (sfxSlider != null)
        {
            SFXManager.Instance.SFXVolume(sfxSlider.value);
            SaveSFXSettings();
        }
    }

    // Save volume settings 
    private void SaveMusicSettings()
    {
        if (musicSlider != null)
        {
            PlayerPrefs.SetFloat("MusicVolume", musicSlider.value);
        }

        PlayerPrefs.Save(); // Ensure the data is saved
    }
    private void SaveSFXSettings()
    {
        if (sfxSlider != null)
        {
            PlayerPrefs.SetFloat("SFXVolume", sfxSlider.value);
        }
        PlayerPrefs.Save(); // Ensure the data is saved
    }

    // Load volume settings
    private void LoadMusicSettings()
    {
        if (musicSlider != null && PlayerPrefs.HasKey("MusicVolume"))
        {
            float savedMusicVolume = PlayerPrefs.GetFloat("MusicVolume");
            Debug.Log("Loaded Music Volume: " + savedMusicVolume);
            musicSlider.value = savedMusicVolume;
            MusicManager.Instance.MusicVolume(savedMusicVolume); // Apply the saved volume
        }
    }
    private void LoadSFXSettings()
    {
        if (sfxSlider != null && PlayerPrefs.HasKey("SFXVolume"))
        {
            float savedSFXVolume = PlayerPrefs.GetFloat("SFXVolume");
            Debug.Log("Loaded SFX Volume: " + savedSFXVolume);
            sfxSlider.value = savedSFXVolume;
            SFXManager.Instance.SFXVolume(savedSFXVolume); // Apply the saved volume
        }
    }

}
