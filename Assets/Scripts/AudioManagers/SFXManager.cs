using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Audio;

[RequireComponent(typeof(AudioSource)), ExecuteInEditMode]
public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance;
    [SerializeField] public SoundList[] soundList;
    public AudioSource sfxSource;

    private void Awake()
    {
        if (!Instance)
        {
            Instance = this;
            sfxSource = GetComponent<AudioSource>();
            Debug.Log("SFXManager Start called. Current SFX Volume: " + sfxSource.volume);
        }
        // if (Application.isPlaying)
        // {
        //     DontDestroyOnLoad(gameObject);
        // }
    }

    public static void PlaySFX(SoundTypes sfx, int index = -1)
    {
        if (Instance != null && Application.isPlaying)
        {
            AudioClip[] clips = Instance.soundList[(int)sfx].Sounds;

            // If a valid index is provided, play that specific sound
            AudioClip selectedClip = index >= 0 && index < clips.Length
                ? clips[index]
                : clips[UnityEngine.Random.Range(0, clips.Length)];

            Instance.sfxSource.PlayOneShot(selectedClip);
        }
    }

    //for uicontrollers
    public void MuteSFX()
    {
        if (sfxSource != null)
        {
            sfxSource.mute = !sfxSource.mute;
        }
    }
    public void SFXVolume(float volume)
    {
        if (sfxSource != null)
        {
            sfxSource.volume = volume;
        }
    }

    private void OnValidate()
    {
        string[] names = Enum.GetNames(typeof(SoundTypes));
        Array.Resize(ref soundList, names.Length);

        for (int i = 0; i < soundList.Length; i++)
        {
            soundList[i].name = names[i];
        }
    }


    [Serializable]
    public struct SoundList
    {
        public AudioClip[] Sounds => sfx;
        [HideInInspector] public string name;
        [SerializeField] public AudioClip[] sfx;
    }

}
