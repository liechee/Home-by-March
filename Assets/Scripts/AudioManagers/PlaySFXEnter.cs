using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaySFXEnter : StateMachineBehaviour
{
    [SerializeField] private SoundTypes sound;
    // [SerializeField, Range(0,1)] private float volume = 1f;
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        SFXManager.PlaySFX(sound);
    }
}