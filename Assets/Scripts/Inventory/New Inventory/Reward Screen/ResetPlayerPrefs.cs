using UnityEngine;

public class ResetPlayerPrefs : MonoBehaviour
{
    void Update()
    {
        // Press 'R' to reset PlayerPrefs
        if (Input.GetKeyDown(KeyCode.R))
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("PlayerPrefs have been reset!");
        }
    }
}