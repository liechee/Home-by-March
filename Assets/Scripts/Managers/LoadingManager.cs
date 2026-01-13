using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoadingManager : MonoBehaviour
{
    [Header("Panel Settings")]
    public List<GameObject> panels; // List of panels to activate/deactivate
    public GameObject loadingPanel; // Loading panel to show during activation

    [Header("Delay Settings")]
    public float panelSwitchDelay = 0.5f; // Delay between activating panels

    private void Start()
    {
        // Start the panel activation process automatically when the scene loads
        StartCoroutine(SwitchPanels());
    }

    private IEnumerator SwitchPanels()
    {
        // Show the loading panel
        loadingPanel.SetActive(true);

        // Iterate through the panels list
        foreach (GameObject panel in panels)
        {
            // Deactivate all panels
            foreach (GameObject p in panels)
            {
                p.SetActive(false);
            }

            // Activate the current panel
            panel.SetActive(true);

            // Wait for the specified delay
            yield return new WaitForSeconds(panelSwitchDelay);
        }

        // Deactivate the last panel in the list
        if (panels.Count > 0)
        {
            panels[panels.Count - 1].SetActive(false);
        }

        // Hide the loading panel after all panels are processed
        loadingPanel.SetActive(false);
    }
}
