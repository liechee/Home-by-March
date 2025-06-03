using UnityEngine;

public class PlaySFXExit : StateMachineBehaviour
{
    [SerializeField] private SoundTypes sound;
    // [SerializeField, Range(0,1)] private float volume = 1f;
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        SFXManager.PlaySFX(sound);
    }
}