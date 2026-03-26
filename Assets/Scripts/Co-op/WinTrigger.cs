using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class WinTrigger : MonoBehaviourPunCallbacks
{
    private HashSet<Collider> detectedPlayers = new HashSet<Collider>();
    [SerializeField] private GameObject youWinPanel;
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private Button exitButton;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI winTimeText;
    [SerializeField] private Button startTimerButton;
    [SerializeField] private float countdownDuration = 300f;
    [SerializeField] private int requiredPlayerPairs = 2;

    private bool timerRunning = false;
    private float remainingTime;

    void Start()
    {
        if (youWinPanel != null) youWinPanel.SetActive(false);
        if (gamePanel != null) gamePanel.SetActive(true);
        if (exitButton != null) exitButton.onClick.AddListener(OnExitButtonClick);
        if (playerCountText != null) playerCountText.text = $"0/{requiredPlayerPairs}";
        if (timerText != null) timerText.text = "0:00";
        if (startTimerButton != null)
        {
            startTimerButton.onClick.AddListener(OnStartTimerButtonClick);
            startTimerButton.gameObject.SetActive(PhotonNetwork.IsMasterClient);
        }
    }

    void Update()
    {
        if (PhotonNetwork.IsMasterClient && timerRunning)
        {
            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0)
            {
                remainingTime = 0;
                timerRunning = false;
                photonView.RPC("ShowWinPanelForAll", RpcTarget.All, PhotonNetwork.Time);
            }
            photonView.RPC("UpdateTimerDisplay", RpcTarget.All, remainingTime);
        }

        if (detectedPlayers.Count >= requiredPlayerPairs)
        {
            photonView.RPC("ShowWinPanelForAll", RpcTarget.All, PhotonNetwork.Time);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (detectedPlayers.Add(other)) // Add only if it's a new player
            {
                Debug.Log($"Player entered: {other.name}");
                photonView.RPC("UpdatePlayerCountForAll", RpcTarget.All, detectedPlayers.Count);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (detectedPlayers.Remove(other)) // Remove only if it was already in the set
            {
                Debug.Log($"Player exited: {other.name}");
                photonView.RPC("UpdatePlayerCountForAll", RpcTarget.All, detectedPlayers.Count);
            }
        }
    }

    [PunRPC]
    private void UpdatePlayerCountForAll(int count)
    {
        if (playerCountText != null)
        {
            playerCountText.text = $"{count}/{requiredPlayerPairs}";
        }
    }

    [PunRPC]
    private void UpdateTimerDisplay(float time)
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(time / 60f);
            int seconds = Mathf.FloorToInt(time % 60f);
            timerText.text = string.Format("{0:D2}:{1:D2}", minutes, seconds);
        }
    }

    [PunRPC]
    private void ShowWinPanelForAll(double networkTime)
    {
        if (youWinPanel != null) youWinPanel.SetActive(true);
        if (gamePanel != null) gamePanel.SetActive(false);

        if (winTimeText != null)
        {
            double elapsedTime = PhotonNetwork.Time - networkTime;
            int minutes = Mathf.FloorToInt((float)elapsedTime / 60f);
            int seconds = Mathf.FloorToInt((float)elapsedTime % 60f);
            winTimeText.text = string.Format("Win Time: {0:D2}:{1:D2}", minutes, seconds);
        }

        Debug.Log($"Game ended at: {PhotonNetwork.Time}");
    }

    public void OnStartTimerButtonClick()
    {
        if (!timerRunning)
        {
            timerRunning = true;
            remainingTime = countdownDuration;
            Debug.Log("Timer started.");
            photonView.RPC("CloseGamePanelForAll", RpcTarget.All);
        }
    }

    [PunRPC]
    private void CloseGamePanelForAll()
    {
        if (gamePanel != null) gamePanel.SetActive(false);
    }

    public void OnExitButtonClick()
    {
        PhotonNetwork.LeaveRoom();
        SceneManager.LoadScene("Lobby");
    }
}
