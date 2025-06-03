using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;
using HomeByMarch;

[CreateAssetMenu(fileName = "MeteorSpellStrategy", menuName = "Spells/MeteorSpellStrategy")]
public class ChnyBossSkill : SpellStrategy
{
    public GameObject meteorPrefab;
    public float damage = 50f;
    public float radius = 5f;
    public float spawnDistance = 2f; // Distance in front of the player
    public float spawnHeight = 5f; // Height above the player
    public float duration = 5f; // Duration of the meteor spell
    public float detectionDelay = 1f; // Delay before starting detection
    public int soundref;

    private GameObject player; // Reference to the player object

    public override void CastSpell(Transform origin)
    {
        Debug.Log("Meteor is casted");

        // Find the player object (assuming there's only one player in the scene)
        if (player == null)
        {
            player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError("Player not found!");
                return;
            }
        }

        // Use the player's position as the spawn position
        Vector3 spawnPosition = player.transform.position;

        var meteor = Instantiate(meteorPrefab, spawnPosition, Quaternion.identity);
        SFXManager.PlaySFX(SoundTypes.Skill_Spell, soundref);
        Destroy(meteor, duration); // Destroy the meteor after the duration

        // Start detection after a delay
        origin.GetComponent<MonoBehaviour>().StartCoroutine(DelayedDetection(spawnPosition));
    }

    private IEnumerator DelayedDetection(Vector3 spawnPosition)
    {
        yield return new WaitForSeconds(detectionDelay);

        // Detect and apply damage to the player within the radius of the spawn position
        Collider[] hitColliders = Physics.OverlapSphere(spawnPosition, radius);
        foreach (var hitCollider in hitColliders)
        {
            Health playerHealth = hitCollider.GetComponent<Health>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage((int)damage);
            }   
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize the radius of the meteor's damage area
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(Vector3.zero, radius);
    }
}
