using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun; // Ensure Photon.Pun is included for PhotonNetwork

public class PlayerSpawner : MonoBehaviour
{
    public GameObject[] playerPrefab;
    public Transform[] spawnPoints; // Corrected the name to spawnPoints

    private void Start()
    {
        int randomIndex = Random.Range(0, spawnPoints.Length); // Corrected to spawnPoints.Length
        Transform spawnPoint = spawnPoints[randomIndex];
        GameObject playerToSpawn = playerPrefab[(int)PhotonNetwork.LocalPlayer.CustomProperties["PlayerAvatar"]];

        PhotonNetwork.Instantiate(playerToSpawn.name, spawnPoint.position, Quaternion.identity);
    }
}