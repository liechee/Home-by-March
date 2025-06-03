using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;

[CreateAssetMenu(fileName = "ShieldSpellStrategy", menuName = "Spells/ShieldSpellStrategy")]
public class ShieldSpellStrategy : SpellStrategy {

    public GameObject shieldPrefab;
    public float duration = 5f;
    public int defense; // New field to add to defenseBuff
    public int soundref;
    

    public override void CastSpell(Transform origin) {
        Debug.Log("Shield is casted");
        var shield = Instantiate(shieldPrefab, origin.position, Quaternion.identity, origin);

        SFXManager.PlaySFX(SoundTypes.Skill_Shield, soundref);
        Destroy(shield, duration);

        // Find the PlayerData component and add the defense value to defenseBuff
        PlayerData playerData = origin.GetComponent<PlayerData>();
        if (playerData != null) {
            playerData.StartCoroutine(ApplyDefenseBuff(playerData));
        } else {
            Debug.LogError("PlayerData component not found on the origin object.");
        }
    }

    private IEnumerator ApplyDefenseBuff(PlayerData playerData) {
        playerData.defenseBuff += defense;
        playerData.UpdateCurrentStats(); // Update the current stats to reflect the new defenseBuff

        yield return new WaitForSeconds(duration);

        playerData.defenseBuff -= defense;
        playerData.UpdateCurrentStats(); // Update the current stats to reflect the removed defenseBuff
    }
}