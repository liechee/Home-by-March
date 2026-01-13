using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;

[CreateAssetMenu(fileName = "BlueSlash", menuName = "Spells/BlueSlash")]
public class BlueSlash : SpellStrategy
{
    public GameObject BlueSlashPrefab;
    public float duration = 1f;

    public override void CastSpell(Transform origin)
    {
        Debug.Log("Slash is casted");

        // Instantiate the BlueSlash prefab as a child of the player (origin)
        var BlueSlash = Instantiate(BlueSlashPrefab, origin);

        // Adjust the local position, rotation, and scale
        BlueSlash.transform.localPosition = new Vector3(-0.16f, 0.56f, 0.44f);
        BlueSlash.transform.localRotation = Quaternion.Euler(1.556f, -295.755f, 174.35f);
        BlueSlash.transform.localScale = new Vector3(0.5590714f, 0.5590714f, 0.5590714f);

        // Destroy the object after the specified duration
        Destroy(BlueSlash, duration);
    }
}
