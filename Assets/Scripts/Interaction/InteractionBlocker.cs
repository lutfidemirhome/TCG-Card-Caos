using UnityEngine;

/// <summary>
/// Blocks card pickup and shelf placement through this collider.
/// Also auto-detected on renderers using the TransparentCollider demo material.
/// </summary>
[DisallowMultipleComponent]
public class InteractionBlocker : MonoBehaviour
{
    [SerializeField] Collider blockerCollider;

    public Collider Collider
    {
        get
        {
            if (blockerCollider == null)
                blockerCollider = GetComponent<Collider>();
            return blockerCollider;
        }
    }

    void Reset()
    {
        blockerCollider = GetComponent<Collider>();
    }

    public static bool IsBlocker(Collider collider)
    {
        if (collider == null)
            return false;

        if (collider.GetComponent<InteractionBlocker>() != null)
            return true;

        // Placed shop walls already have solid colliders but do not carry the
        // demo-only marker/material. Include their mesh children as well.
        for (Transform current = collider.transform; current != null; current = current.parent)
        {
            string objectName = current.name;
            if (objectName.StartsWith("Wall_", System.StringComparison.OrdinalIgnoreCase)
                || ExteriorColliderCleanup.IsPlacedHouseWallName(objectName))
                return true;
        }

        MeshRenderer renderer = collider.GetComponent<MeshRenderer>();
        if (renderer == null)
            return false;

        Material material = renderer.sharedMaterial;
        if (material == null)
            return false;

        return material.name.StartsWith("TransparentCollider");
    }
}
