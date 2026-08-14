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

        if (collider.GetComponentInParent<InteractionBlocker>() != null)
            return true;

        MeshRenderer renderer = collider.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = collider.GetComponentInParent<MeshRenderer>();

        if (renderer == null)
            return false;

        Material material = renderer.sharedMaterial;
        if (material == null)
            return false;

        return material.name.StartsWith("TransparentCollider");
    }
}
