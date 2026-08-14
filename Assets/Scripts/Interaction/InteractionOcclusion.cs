using UnityEngine;

/// <summary>
/// Checks whether an interaction target is hidden behind glass or other blockers.
/// </summary>
public static class InteractionOcclusion
{
    const float Clearance = 0.03f;
    static readonly RaycastHit[] Hits = new RaycastHit[16];

    public static bool IsOccluded(Ray ray, float targetDistance, float maxDistance)
    {
        if (targetDistance >= float.MaxValue * 0.5f)
            return false;

        float probeDistance = Mathf.Min(maxDistance, targetDistance - Clearance);
        if (probeDistance <= 0f)
            return false;

        CardLayers.EnsureInitialized();
        int mask = ~CardLayers.WorldCardMask;
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            Hits,
            probeDistance,
            mask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = Hits[i].collider;
            if (collider == null)
                continue;

            if (!InteractionBlocker.IsBlocker(collider))
                continue;

            if (Hits[i].distance <= targetDistance - Clearance)
                return true;
        }

        return false;
    }
}
