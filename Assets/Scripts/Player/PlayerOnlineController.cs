using UnityEngine;
using Photon.Pun;
using Cinemachine;
using UnityEngine.UI;  // For accessing UI elements

namespace HomeByMarch
{
    public class PlayerOnlineController : MonoBehaviourPunCallbacks, IPunObservable
    {
        [Header("Movement")]
        public float moveSpeed = 5f;
        public Rigidbody m_Body;

        [Header("Attack")]
        public float attackCooldown = 1f;
        private bool readyToAttack = true;

        [Header("Health")]
        public Health health;

        [Header("Animation")]
        public Animator animator;
        private static readonly int Speed = Animator.StringToHash("Speed");

        private Vector3 networkPosition;
        private Quaternion networkRotation;

        // Direct reference to the fixed joystick (UI GameObject with Joystick script)
        public Joystick joystick;  // Reference to the joystick component

        void Start()
        {
            PhotonNetwork.OfflineMode = true;

            if (!PhotonNetwork.IsConnected)
            {
                Debug.LogError("Photon is not connected. Ensure that you have connected to the server.");
                enabled = false;
                return;
            }

            if (photonView == null || m_Body == null || health == null || animator == null)
            {
                Debug.LogError("Missing essential component references in PlayerController.");
                enabled = false;
                return;
            }

            // Check if the joystick is properly set
            if (joystick == null)
            {
                Debug.LogError("Joystick is not assigned!");
                enabled = false;
                return;
            }

            if (photonView.IsMine)
            {
                var cinemachineFreeLook = FindObjectOfType<CinemachineFreeLook>();
                if (cinemachineFreeLook != null)
                {
                    cinemachineFreeLook.Follow = transform;
                    cinemachineFreeLook.LookAt = transform;
                }
                else
                {
                    Debug.LogWarning("No CinemachineFreeLook found in the scene.");
                }
            }
            else
            {
                m_Body.isKinematic = true;
            }
        }

        void Update()
        {
            if (!PhotonNetwork.IsConnected)
            {
                Debug.LogWarning("Photon network is not connected.");
                return;
            }

            if (photonView.IsMine)
            {
                HandleMovement();
                HandleAttack();
            }
            else
            {
                SmoothSyncRemotePlayer();
            }

            UpdateAnimator();
        }

        private void HandleMovement()
        {
            // Get input from the fixed joystick directly (UI-based)
            Vector2 joystickInput = joystick.Direction; // Assuming Joystick script provides a Direction property

            // Normalize direction vector
            Vector3 direction = new Vector3(joystickInput.x, 0, joystickInput.y).normalized;
            Vector3 velocity = direction * moveSpeed;

            if (m_Body != null)
            {
                m_Body.MovePosition(m_Body.position + velocity * Time.deltaTime);
            }

            if (direction.magnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        private void HandleAttack()
        {
            if (PhotonNetwork.IsConnected && Input.GetMouseButtonDown(0) && readyToAttack)
            {
                readyToAttack = false;
                photonView.RPC("Attack", RpcTarget.All);
                Invoke(nameof(ResetAttack), attackCooldown);
            }
            else if (!PhotonNetwork.IsConnected)
            {
                Debug.LogWarning("Cannot attack because the network is not connected.");
            }
        }

        [PunRPC]
        private void Attack()
        {
            Debug.Log("Attack triggered");
            // TODO: Add range validation and raycast logic
        }

        private void ResetAttack()
        {
            readyToAttack = true;
        }

        private void SmoothSyncRemotePlayer()
        {
            if (m_Body != null)
            {
                float interpolationFactor = Time.deltaTime * 10f;
                m_Body.position = Vector3.Lerp(m_Body.position, networkPosition, interpolationFactor);
                transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, interpolationFactor);
            }
        }

        private void UpdateAnimator()
        {
            if (photonView.IsMine && animator != null && m_Body != null)
            {
                animator.SetFloat(Speed, m_Body.velocity.magnitude);
            }
        }

        public void TakeDamage(int damage)
        {
            photonView.RPC("ApplyDamage", RpcTarget.All, damage);
        }

        [PunRPC]
        private void ApplyDamage(int damage)
        {
            if (health != null)
            {
                health.CurrentHealth -= damage;

                if (health.CurrentHealth <= 0 && !health.IsDead)
                {
                    health.CurrentHealth = 0;
                    photonView.RPC("Die", RpcTarget.All);
                }
            }
        }

        [PunRPC]
        private void Die()
        {
            Debug.Log("Player has died");
            // TODO: Trigger death animation and disable controls
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(m_Body?.position ?? Vector3.zero);
                stream.SendNext(transform.rotation);
            }
            else
            {
                networkPosition = (Vector3)stream.ReceiveNext();
                networkRotation = (Quaternion)stream.ReceiveNext();
            }
        }
    }
}
