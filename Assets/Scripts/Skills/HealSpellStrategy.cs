using System.Collections;
using UnityEngine;
using Utilities;
using HomeByMarch;

[CreateAssetMenu(fileName = "HealSpellStrategy", menuName = "Spells/HealSpellStrategy")]
public class HealSpellStrategy : SpellStrategy
{

    
    public GameObject HealPrefab; // Visual effect for healing
    public float duration = 5f;  // Duration of the healing effect
    public float healPercentage = 0.2f; // 20% healing

    public int soundref;

    public override void CastSpell(Transform origin)
    {
        Debug.Log("Heal spell is casted");
        var healEffect = Instantiate(HealPrefab, origin.position, Quaternion.identity, origin); // Visual effect
        SFXManager.PlaySFX(SoundTypes.Skill_Heal, soundref);

        Destroy(healEffect, duration); // Automatically destroy visual effect after duration

        var playerHealth = origin.GetComponent<Health>(); // Get the Health component on the origin
        if (playerHealth != null)
        {
            origin.GetComponent<MonoBehaviour>().StartCoroutine(HealOverTime(playerHealth));
        }
        else
        {
            Debug.LogWarning("No Health component found on the origin.");
        }
    }

    private IEnumerator HealOverTime(Health health)
    {
        float totalHealing = health.MaxHealth * healPercentage; // Total amount to heal (20% of max health)
        float healingPerSecond = totalHealing / duration;       // Healing per second
        float elapsedTime = 0f;
        float healingAccumulator = 0f; // Accumulates fractional healing

        // Debug.Log($"Starting heal over time: totalHealing={totalHealing}, healingPerSecond={healingPerSecond}, duration={duration}");

        while (elapsedTime < duration && !health.IsDead)
        {
            float healingThisFrame = healingPerSecond * Time.deltaTime; // Healing for this frame
            healingAccumulator += healingThisFrame; // Accumulate fractional healing

            int healAmount = Mathf.FloorToInt(healingAccumulator); // Apply only the whole numbers
            if (healAmount > 0)
            {
                health.Heal(healAmount); // Heal the player
                healingAccumulator -= healAmount; // Remove the applied healing from the accumulator

                // Debug log current health
                // Debug.Log($"Healing... Current Health: {health.CurrentHealth}, Heal Applied: {healAmount}");
            }

            elapsedTime += Time.deltaTime;
            yield return null; // Wait until the next frame
        }

        // Debug.Log("Healing process completed.");
    }

}
