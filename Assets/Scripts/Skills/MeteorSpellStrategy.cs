using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;
using HomeByMarch;

[CreateAssetMenu(fileName = "MeteorSpellStrategy", menuName = "Spells/MeteorSpellStrategy")]
public class MeteorSpellStrategy : SpellStrategy
{

    public GameObject meteorPrefab;
    public float damage = 50f;
    public float radius = 5f;
    public float spawnDistance = 2f; // Distance in front of the player
    public float spawnHeight = 5f; // Height above the player
    public float duration = 5f; // Duration of the meteor spell
    public float damageInterval = 1f; // Interval between damage applications
    public int soundref;
    public override void CastSpell(Transform origin)
    {
        Debug.Log("Meteor is casted");

        // Calculate the spawn position in front of and above the player
        Vector3 spawnPosition = origin.position + origin.forward * spawnDistance + Vector3.up * spawnHeight;

        var meteor = Instantiate(meteorPrefab, spawnPosition, Quaternion.identity);
        SFXManager.PlaySFX(SoundTypes.Skill_Spell, soundref);
        Destroy(meteor, duration); // Destroy the meteor after the duration

        // Start the coroutine to apply damage over time
        origin.GetComponent<MonoBehaviour>().StartCoroutine(ApplyDamageOverTime(spawnPosition));
    }

    private IEnumerator ApplyDamageOverTime(Vector3 spawnPosition)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            // Apply damage to enemies within the radius of the spawn position
            Collider[] hitColliders = Physics.OverlapSphere(spawnPosition, radius);
            foreach (var hitCollider in hitColliders)
            {
                Enemy enemy = hitCollider.GetComponent<Enemy>();
                if (enemy != null)
                {
                    enemy.TakeDamage((int)damage);
                }
            }

            // Wait for the next damage interval
            yield return new WaitForSeconds(damageInterval);
            elapsedTime += damageInterval;
        }
    }
}