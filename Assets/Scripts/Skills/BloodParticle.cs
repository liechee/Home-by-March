using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;

[CreateAssetMenu(fileName = "BloodParticle", menuName = "Spells/BloodParticle")]
public class BloodParticle : SpellStrategy
{
    public GameObject BloodParticlePrefab;
    public float duration = 5f;

    public override void CastSpell(Transform origin)
    {
        Debug.Log("Blood particle is casted");

        // Adjust position to instantiate at the center of the character
        Vector3 centerPosition = origin.position + new Vector3(0f, 1f, 0f); // Adjust the 'Y' offset as needed

        // Instantiate the blood particle effect
        var BloodParticle = Instantiate(BloodParticlePrefab, centerPosition, Quaternion.identity, origin);

        // Destroy the effect after the specified duration
        Destroy(BloodParticle, duration);
    }
}
