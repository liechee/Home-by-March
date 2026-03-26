using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Cinemachine;
using HomeByMarch;

public class SpawnPlayers : MonoBehaviour
{
    public FixedJoystick joystickPrefab; // Reference to the joystick prefab
    public GameObject playerPrefab; // Player prefab to spawn
    public Camera mainCamera; // Reference to the main camera
    public CinemachineVirtualCamera cinemachineCamera; // Reference to the Cinemachine camera
    public Canvas uiCanvas; // Reference to the canvas where the joystick should be instantiated
    public List<Transform> spawnPoints; // List of predefined spawn points

    private FixedJoystick instantiatedJoystick;

    // Start is called before the first frame update
    void Start()
    {
        Vector3 spawnPosition;
        

        // Determine which spawn point to use based on the player's actor number
        int playerIndex = PhotonNetwork.LocalPlayer.ActorNumber - 1; // ActorNumber starts at 1
        if (spawnPoints != null && playerIndex < spawnPoints.Count)
        {
            spawnPosition = spawnPoints[playerIndex].position;
        }
        else
        {
            Debug.LogError("No valid spawn point available for player index: " + playerIndex);
            return;
        }

        // Instantiate the player using PhotonNetwork
        GameObject player = PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, Quaternion.identity);
        player.GetComponent<PlayerMark>().Mark.SetActive(true);

        // Ensure the PlayerController is found
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            // Enable the PlayerController if it's disabled
            if (!playerController.isActiveAndEnabled)
            {
                playerController.enabled = true; // Enable the PlayerController component
            }

            // Instantiate the joystick prefab inside the UI Canvas
            if (joystickPrefab != null && uiCanvas != null)
            {
                instantiatedJoystick = Instantiate(joystickPrefab, uiCanvas.transform);
                RectTransform joystickRect = instantiatedJoystick.GetComponent<RectTransform>();
                if (joystickRect != null)
                {
                    joystickRect.anchoredPosition = new Vector2(245, 246); // Adjust based on your UI layout
                }
                else
                {
                    Debug.LogError("RectTransform not found on joystick prefab.");
                }
                playerController.joystick = instantiatedJoystick; // Assign joystick to PlayerController
            }
            else
            {
                Debug.LogError("Joystick prefab or UI Canvas reference not set.");
            }

            // Assign the Cinemachine camera follow
            if (cinemachineCamera != null)
            {
                cinemachineCamera.Follow = player.transform;
                cinemachineCamera.LookAt = player.transform;
            }
            else
            {
                Debug.LogError("CinemachineVirtualCamera reference not set.");
            }
        }
        else
        {
            Debug.LogError("PlayerController component not found on the instantiated player.");
        }
    }
}
