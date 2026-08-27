using UnityEngine;

/// <summary>
/// Checks whether an interaction target is hidden behind glass, floors, or other blockers.
/// </summary>
public static class InteractionOcclusion
{
    const float Clearance = 0.03f;
    const float FloorThroughMargin = 0.08f;
    static readonly RaycastHit[] Hits = new RaycastHit[32];

    public static bool IsOccluded(Ray ray, float targetDistance, float maxDistance)
    {
        if (targetDistance >= float.MaxValue * 0.5f)
            return false;

        float probeDistance = Mathf.Min(maxDistance, targetDistance - Clearance);
        if (probeDistance <= 0f)
            return false;

        CardLayers.EnsureInitialized();
        int mask = Physics.DefaultRaycastLayers & ~CardLayers.WorldCardMask;
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            Hits,
            probeDistance,
            mask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = Hits[i];
            Collider collider = hit.collider;
            if (collider == null || hit.distance > targetDistance - Clearance)
                continue;

            if (collider is CharacterController)
                continue;

            if (IsFloorSlab(collider))
            {
                if (BlocksThroughFloor(hit, ray, targetDistance))
                    return true;

                continue;
            }

            if (InteractionBlocker.IsBlocker(collider))
                return true;
        }

        return false;
    }

    static bool IsFloorSlab(Collider collider)
    {
        return collider != null && NameLooksLikeFloorSlab(collider.gameObject.name);
    }

    static bool NameLooksLikeFloorSlab(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        return objectName.StartsWith("Cube", System.StringComparison.OrdinalIgnoreCase)
            || objectName.StartsWith("Floor", System.StringComparison.OrdinalIgnoreCase)
            || objectName.StartsWith("Ceiling", System.StringComparison.OrdinalIgnoreCase)
            || objectName.Equals("Ground", System.StringComparison.OrdinalIgnoreCase);
    }

    static bool BlocksThroughFloor(RaycastHit hit, Ray ray, float targetDistance)
    {
        float normalY = hit.normal.y;

        if (normalY < -0.15f)
            return true;

        if (normalY > 0.15f)
        {
            Vector3 targetPoint = ray.GetPoint(targetDistance);
            return targetPoint.y < hit.point.y - FloorThroughMargin;
        }

        return true;
    }
}
