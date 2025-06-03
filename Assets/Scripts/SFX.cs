using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SFX : MonoBehaviour

{
    public AudioSource sound;
    public void click()
    {
        SFXManager.PlaySFX(SoundTypes.Button);
    }

}