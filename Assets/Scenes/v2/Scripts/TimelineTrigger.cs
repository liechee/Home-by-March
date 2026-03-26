using UnityEngine;
using UnityEngine.Playables;

public class TimelineTrigger : MonoBehaviour
{
    public PlayableDirector director;
    public GameObject timeline;
    public GameObject player;  // Drag your player GameObject here

    private PlayerMovement playerMovement;
    private void Start()
    {
        if (player != null)
        {
            playerMovement = player.GetComponent<PlayerMovement>();
        }

        // Optional: Pause timeline if it's set to play automatically
        if (director != null)
        {
            director.stopped += OnTimelineStopped;
        }
    }

     private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (timeline != null && !timeline.activeSelf)
            {
                timeline.SetActive(true);
            }

            if (playerMovement != null)
            {
                playerMovement.enabled = false;  // Disable movement
            }

            if (director != null)
            {
                director.Play();
            }
        }
    }

    private void OnTimelineStopped(PlayableDirector d)
    {
        if (playerMovement != null)
        {
            playerMovement.enabled = true;  // Re-enable movement when timeline finishes
        }
    }
}