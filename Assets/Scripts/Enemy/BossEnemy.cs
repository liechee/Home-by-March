// using System.Collections.Generic;
// using Cinemachine;
// using KBCore.Refs;
// using UnityEngine;
// using Utilities;
// using Photon.Pun;
// using System.Collections;
// using UnityEngine.UI;

// namespace HomeByMarch
// {
//     [RequireComponent(typeof(UnityEngine.AI.NavMeshAgent))]
//     [RequireComponent(typeof(PlayerDetector))]
//     public class BossEnemy : Entity
//     {
//         private DungeonGameController gameController;
//         float currentHealth;
//         [SerializeField] public int maxHealth;
//         [SerializeField] PlayerDetector playerDetector;
//         [SerializeField] private UnityEngine.AI.NavMeshAgent agent;
//         [SerializeField] private Animator animator;
//         [SerializeField] float timeBetweenAttack = 1f;
//         [SerializeField] float attackDelay = 0.8f;
//         [SerializeField] float OnHitDelay = 0.5f;
//         [SerializeField] float wanderRadius = 10f;
//         [SerializeField] private GameObject healthBarPrefab;
//         [SerializeField] public float attackDistance = 30f;
//         [SerializeField] public int attackDamage = 10;
//         public LayerMask attackLayer;

//         private StateMachine stateMachine;
//         public Transform Player { get; private set; }
//         public Health PlayerHealth { get; private set; }
//         CountdownTimer attackTimer;
//         CountdownTimer onHitTimer;

//         [SerializeField] public SFXManager SFXManager;

//         public Image healthBar;
//         bool isHit;

//         // Reference to the BloodParticle effect
//         [SerializeField] private BloodParticle bloodParticle;

//         void Awake()
//         {
//             Player = GameObject.FindGameObjectWithTag("Player")?.transform;
//             if (Player == null)
//             {
//                 Debug.LogError("Player not found! Ensure the Player GameObject is tagged as 'Player'.");
//             }

//             PlayerHealth = Player?.GetComponent<Health>();
//             if (PlayerHealth == null)
//             {
//                 Debug.LogError("Health component not found on Player GameObject.");
//             }

//             currentHealth = maxHealth;
//         }

//         void Start()
//         {
//             gameController = FindObjectOfType<DungeonGameController>();
//             attackTimer = new CountdownTimer(timeBetweenAttack);
//             onHitTimer = new CountdownTimer(OnHitDelay);

//             stateMachine = new StateMachine();

//             var wanderState = new EnemyWanderState(this, animator, agent, wanderRadius);
//             var chaseState = new EnemyChaseState(this, animator, agent, playerDetector.Player);
//             var attackState = new EnemyAttackState(this, animator, agent, playerDetector.Player);
//             var deathState = new EnemyDeathState(this, animator, agent);
//             var onHitState = new EnemyOnHitState(this, animator, agent);

//             At(wanderState, chaseState, new FuncPredicate(() => playerDetector.CanDetectPlayer()));
//             At(chaseState, wanderState, new FuncPredicate(() => !playerDetector.CanDetectPlayer()));
//             At(chaseState, attackState, new FuncPredicate(() => playerDetector.CanAttackPlayer()));
//             At(attackState, chaseState, new FuncPredicate(() => !playerDetector.CanAttackPlayer()));
//             At(attackState, onHitState, new FuncPredicate(() => isHit));
//             At(onHitState, attackState, new FuncPredicate(() => !isHit && !onHitTimer.IsRunning));
//             Any(deathState, new FuncPredicate(() => currentHealth <= 0));

//             stateMachine.SetState(wanderState);
//         }

//         void At(IState from, IState to, IPredicate condition) => stateMachine.AddTransition(from, to, condition);
//         void Any(IState to, IPredicate condition) => stateMachine.AddAnyTransition(to, condition);

//         void Update()
//         {
//             if (currentHealth <= 0)
//             {
//                 stateMachine.SetState(new EnemyDeathState(this, animator, agent));  // Enforce death state if health is zero
//             }
//             else
//             {
//                 stateMachine.Update();
//             }

//             attackTimer.Tick(Time.deltaTime);
//             onHitTimer.Tick(Time.deltaTime);

//             healthBar.fillAmount = Mathf.Clamp(currentHealth / maxHealth, 0.0f, 1.0f);

//             // Show health bar only when health is below max or player is detected
//             healthBarPrefab.SetActive(currentHealth < maxHealth || playerDetector.CanDetectPlayer());
//         }

//         void FixedUpdate()
//         {
//             stateMachine.FixedUpdate();
//         }

//         public void Attack()
//         {
//             if (attackTimer.IsRunning) return;

//             attackTimer.Start();
//             agent.isStopped = true;
//             StartCoroutine(DelayedAttack());
//         }

//         [SerializeField] private BlueSlash[] blueSlashSpells;

//         public void ActivateBlueSlash()
//         {
//             foreach (var slash in blueSlashSpells)
//             {
//                 if (slash != null)
//                 {
//                     slash.CastSpell(transform); // Use the enemy's transform as the origin
//                 }
//             }
//         }

//         private IEnumerator DelayedAttack()
//         {
//             Debug.Log("Attack initiated");

//             yield return new WaitForSeconds(attackDelay);

//             if (playerDetector.CanAttackPlayer())
//             {
//                 Debug.Log("Player in range. Executing attack.");
//                 AttackRayCast();

//                 // Activate BlueSlash as part of the attack
//                 ActivateBlueSlash();
//             }
//             else
//             {
//                 Debug.LogWarning("Player out of attack range.");
//             }

//             agent.isStopped = false;
//         }

//         public void OnHit()
//         {
//             isHit = true;
//             onHitTimer.Start();
//             StartCoroutine(OnHitDelayed());
//         }

//         private IEnumerator OnHitDelayed()
//         {
//             yield return new WaitForSeconds(OnHitDelay);
//             isHit = false;
//         }

//         public void TakeDamage(int amount)
//         {
//             if (currentHealth <= 0) return; // Prevent further damage after death

//             currentHealth -= amount;
//             currentHealth = Mathf.Clamp(currentHealth, 0.0f, maxHealth);

//             if (currentHealth <= 0)
//             {
//                 SFXManager.PlaySFX(SoundTypes.Damage, 1);
//                 ActivateBloodParticle();
//                 Destroy(healthBar);
//                 gameController?.OnEnemyDefeated(); // Trigger defeat only once
//                 stateMachine.SetState(new EnemyDeathState(this, animator, agent));  // Transition to death state
//             }
//             else
//             {
//                 SFXManager.PlaySFX(SoundTypes.Damage, 1);
//                 ActivateBloodParticle();
//                 OnHit();
//                 // Trigger blood particle effect when damage is taken
//             }
//         }

//         void AttackRayCast()
//         {
//             Vector3 rayOrigin = transform.position + Vector3.up * 1f;

//             Vector3[] rayDirections = new Vector3[10]
//             {
//                 transform.forward,
//                 transform.forward + transform.right,
//                 transform.forward - transform.right,
//                 transform.forward + transform.up,
//                 transform.forward - transform.up,
//                 transform.forward + transform.right + transform.up,
//                 transform.forward + transform.right - transform.up,
//                 transform.forward - transform.right + transform.up,
//                 transform.forward - transform.right - transform.up,
//                 transform.forward + transform.up * 2
//             };

//             foreach (var direction in rayDirections)
//             {
//                 Vector3 rayEnd = rayOrigin + direction * attackDistance;
//                 Debug.DrawLine(rayOrigin, rayEnd, Color.red, 0.1f);

//                 if (Physics.Raycast(rayOrigin, direction, out RaycastHit hit, attackDistance))
//                 {
//                     Debug.Log($"Hit object: {hit.transform.name}");

//                     if (hit.transform.CompareTag("Player"))
//                     {
//                         Debug.Log("Player hit! Applying damage.");
//                         PlayerHealth.TakeDamage(attackDamage);
//                     }
//                 }
//                 else
//                 {
//                     Debug.Log("No object hit by ray.");
//                 }
//             }
//         }

//         // Method to activate the BloodParticle effect
//         private void ActivateBloodParticle()
//         {
//             if (bloodParticle != null)
//             {
//                 bloodParticle.CastSpell(transform); // Cast the blood particle effect on the enemy
//             }
//             else
//             {
//                 Debug.LogWarning("BloodParticle is not assigned.");
//             }
//         }
//     }
// }
