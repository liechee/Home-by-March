using UnityEngine;

public class MaterialChanger : MonoBehaviour
{
    public SkinnedMeshRenderer skinnedMeshRenderer; // Reference to the Skinned Mesh Renderer
    public Material[] materials;                   // Array to hold different materials
    public int materialSlot = 0;                   // Which material slot to change (e.g., 0 or 1)

    private int currentMaterialIndex = 0;          // Track the current material index

    // This method will be called to change the material
    public void ChangeMaterial()
    {
        if (skinnedMeshRenderer != null && materials.Length > 0 && materialSlot < skinnedMeshRenderer.materials.Length)
        {
            // Get all current materials
            Material[] currentMaterials = skinnedMeshRenderer.materials;

            // Change the material in the specified slot
            currentMaterials[materialSlot] = materials[currentMaterialIndex];

            // Apply the updated materials
            skinnedMeshRenderer.materials = currentMaterials;

            // Cycle to the next material
            currentMaterialIndex = (currentMaterialIndex + 1) % materials.Length;

            Debug.Log("Material changed to: " + materials[currentMaterialIndex].name);
        }
        else
        {
            Debug.LogError("Invalid material slot, materials array empty, or SkinnedMeshRenderer not assigned!");
        }
    }
}
