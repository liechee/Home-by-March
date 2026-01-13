using UnityEngine;
using UnityEngine.Playables;

public class TimelineControl : MonoBehaviour
{
    public static TimelineControl Instance { get; private set; }

    [SerializeField] private PlayableDirector director;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (director == null)
            director = GetComponent<PlayableDirector>();
    }

    public void PauseTimeline()
    {
        if (director != null && director.state == PlayState.Playing)
        {
            director.Pause();
            Debug.Log("Timeline Paused");
        }
    }

    public void ContinueTimeline()
    {
        if (director != null && director.state != PlayState.Playing)
        {
            director.Play();
            Debug.Log("Timeline Resumed");
        }
    }
    [SerializeField] private PlayableDirector timeline;
    private bool hasPlayed = false;

    public void PlayTimelineOnce()
    {
        if (hasPlayed || timeline == null) return;

        timeline.Play();
        hasPlayed = true;
        Debug.Log("Timeline played once.");
    }
}

