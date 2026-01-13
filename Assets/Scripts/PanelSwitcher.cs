using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PanelSwitcher : MonoBehaviour
{
    public GameObject[] panels; // Assign panels in the Inspector
    private int currentIndex = 0;

    public void NextPanel()
    {
        // Deactivate the current panel
        panels[currentIndex].SetActive(false);

        // Move to the next panel (loop back to 0 if at the last panel)
        currentIndex = (currentIndex + 1) % panels.Length;

        // Activate the new panel
        panels[currentIndex].SetActive(true);
    }

    public void PreviousPanel()
    {
        // Deactivate the current panel
        panels[currentIndex].SetActive(false);

        // Move to the previous panel (loop to last panel if at the first)
        currentIndex = (currentIndex - 1 + panels.Length) % panels.Length;

        // Activate the new panel
        panels[currentIndex].SetActive(true);
    }
}
