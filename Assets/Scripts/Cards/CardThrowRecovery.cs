using UnityEngine;

/// <summary>
/// When Q-thrown cards or packs rest on cabinet geometry instead of the floor, knock them
/// back down so they cannot become unrecoverable before force-settle snaps them underground.
/// </summary>
public static class CardThrowRecovery
{
    public const float ShelfStuckRecoveryDelay = 2f;

    const float MinElevatedYAboveGround = 0.32f;
    const float HighElevatedYAboveGround = 0.55f;
    const float LiftClearance = 0.04f;
    const float SleepingFloorSkipY = 0.12f;
    const int OverlapBufferSize = 32;

    static readonly RaycastHit[] FloorHitBuffer = new RaycastHit[16];
    static readonly Collider[] OverlapBuffer = new Collider[OverlapBufferSize];

    struct ShelfOverlapResult
    {
        public bool TouchesShelf;
        public CardShelf NearestShelf;
        public Vector3 PenetrationDirection;
        public float PenetrationDepth;
    }

    public enum ShelfSettleAdvance
    {
        Continue,
        RecoverAndContinue,
        BreakSettle,
    }

    public static bool ShouldTrackShelfStuck(
        Transform itemTransform,
        BoxCollider itemCollider,
        Rigidbody body,
        bool slowEnough)
    {
        if (itemTransform == null || !slowEnough)
            return false;

        if (itemTransform.GetComponentInParent<CardShelfSlot>() != null)
            return false;

        float groundY = CardFactory.GroundHeightOffset();
        if (body != null && body.IsSleeping() && itemTransform.position.y <= groundY + SleepingFloorSkipY)
            return false;

        if (TryQueryShelfOverlaps(itemTransform, itemCollider, out ShelfOverlapResult overlap)
            && (overlap.TouchesShelf || overlap.PenetrationDepth > 0.001f))
            return true;

        float minElevatedY = groundY + MinElevatedYAboveGround;
        if (itemTransform.position.y < minElevatedY)
            return false;

        return itemTransform.position.y >= groundY + HighElevatedYAboveGround;
    }

    /// <summary>
    /// Shared shelf-stuck branch for card/pack settle coroutines.
    /// </summary>
    public static ShelfSettleAdvance AdvanceShelfStuckSettle(
        ref float shelfStuckTime,
        ref float elapsed,
        ref float groundedTime,
        Transform itemTransform,
        BoxCollider itemCollider,
        Rigidbody body,
        bool nearGround,
        bool slowEnough,
        float maxFlightTime)
    {
        if (itemCollider != null
            && ShouldTrackShelfStuck(itemTransform, itemCollider, body, slowEnough))
        {
            shelfStuckTime += Time.deltaTime;
            if (shelfStuckTime >= ShelfStuckRecoveryDelay
                && TryRecoverShelfStuckThrow(itemTransform, itemCollider, body))
            {
                shelfStuckTime = 0f;
                elapsed = 0f;
                groundedTime = 0f;
            }
        }
        else
        {
            shelfStuckTime = 0f;
        }

        if (elapsed < maxFlightTime)
            return ShelfSettleAdvance.Continue;

        if (!nearGround && itemCollider != null)
        {
            if (TryRecoverShelfStuckThrow(itemTransform, itemCollider, body)
                || TryForceDropAboveFloor(itemTransform, itemCollider, body))
            {
                shelfStuckTime = 0f;
                elapsed = 0f;
                groundedTime = 0f;
                return ShelfSettleAdvance.RecoverAndContinue;
            }
        }

        return ShelfSettleAdvance.BreakSettle;
    }

    /// <summary>
    /// Re-throw from the current pose — no horizontal teleport that can suck the item into the cabinet.
    /// </summary>
    public static bool TryRecoverShelfStuckThrow(
        Transform itemTransform,
        BoxCollider itemCollider,
        Rigidbody body)
    {
        if (itemTransform == null || body == null || body.isKinematic)
            return false;

        if (itemTransform.GetComponentInParent<CardShelfSlot>() != null)
            return false;

        TryQueryShelfOverlaps(itemTransform, itemCollider, out ShelfOverlapResult overlap);
        Vector3 pushOut = ComputeShelfPushOut(itemTransform, overlap);
        Vector3 pos = itemTransform.position;

        if (overlap.PenetrationDepth > 0.001f)
            pos.y += LiftClearance;

        body.position = pos;
        itemTransform.position = pos;

        body.linearVelocity = pushOut * 2.4f + Vector3.down * 1.6f;
        body.angularVelocity = new Vector3(
            Random.Range(-0.18f, 0.18f),
            Random.Range(-0.25f, 0.25f),
            Random.Range(-0.18f, 0.18f));

        return true;
    }

    /// <summary>
    /// Last resort before force-settle: drop straight above the floor at the current XZ.
    /// </summary>
    public static bool TryForceDropAboveFloor(
        Transform itemTransform,
        BoxCollider itemCollider,
        Rigidbody body)
    {
        if (itemTransform == null)
            return false;

        if (itemTransform.GetComponentInParent<CardShelfSlot>() != null)
            return false;

        if (!TryFindFloorPoint(itemTransform.position, out Vector3 floorPoint))
            floorPoint = new Vector3(itemTransform.position.x, CardFactory.GroundHeightOffset(), itemTransform.position.z);

        Vector3 dropPos = floorPoint + Vector3.up * 0.22f;
        if (body != null && !body.isKinematic)
        {
            body.position = dropPos;
            body.linearVelocity = Vector3.down * 2.8f;
            body.angularVelocity *= 0.35f;
        }

        itemTransform.position = dropPos;
        return true;
    }

    static bool TryQueryShelfOverlaps(
        Transform itemTransform,
        BoxCollider itemCollider,
        out ShelfOverlapResult result)
    {
        result = default;
        if (itemCollider == null)
            return false;

        if (!TryGetOverlapBox(itemTransform, itemCollider, out Vector3 center, out Vector3 halfExtents))
            return false;

        Collider[] overlaps = OverlapBuffer;
        int overlapCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            overlaps,
            itemTransform.rotation,
            ~0,
            QueryTriggerInteraction.Ignore);

        float bestShelfDistanceSq = float.PositiveInfinity;
        float bestPenetrationDistance = 0f;
        Vector3 bestPenetrationDirection = default;

        for (int i = 0; i < overlapCount; i++)
        {
            Collider overlap = overlaps[i];
            if (!IsShelfOverlapCandidate(overlap, itemCollider, itemTransform))
                continue;

            result.TouchesShelf = true;

            CardShelf shelf = overlap.GetComponentInParent<CardShelf>();
            if (shelf != null)
            {
                float distanceSq = (shelf.transform.position - itemTransform.position).sqrMagnitude;
                if (distanceSq < bestShelfDistanceSq)
                {
                    bestShelfDistanceSq = distanceSq;
                    result.NearestShelf = shelf;
                }
            }

            if (!Physics.ComputePenetration(
                    itemCollider,
                    itemTransform.position,
                    itemTransform.rotation,
                    overlap,
                    overlap.transform.position,
                    overlap.transform.rotation,
                    out Vector3 direction,
                    out float distance))
            {
                continue;
            }

            result.PenetrationDepth = Mathf.Max(result.PenetrationDepth, distance);
            if (distance > bestPenetrationDistance)
            {
                bestPenetrationDistance = distance;
                bestPenetrationDirection = direction;
            }
        }

        if (bestPenetrationDistance > 0.0001f)
            result.PenetrationDirection = bestPenetrationDirection;

        return result.TouchesShelf || result.PenetrationDepth > 0.0001f;
    }

    static Vector3 ComputeShelfPushOut(Transform itemTransform, ShelfOverlapResult overlap)
    {
        Vector3 aisle = Vector3.zero;
        if (overlap.NearestShelf != null)
        {
            aisle = overlap.NearestShelf.GetCustomerFacingDirection();
            aisle.y = 0f;
            if (aisle.sqrMagnitude > 0.0001f)
                aisle.Normalize();
        }

        Vector3 separation = overlap.PenetrationDirection;
        separation.y = 0f;
        if (separation.sqrMagnitude > 0.0001f)
        {
            separation.Normalize();
            if (aisle.sqrMagnitude > 0.0001f && Vector3.Dot(separation, aisle) < 0f)
                return aisle;
            return separation;
        }

        if (aisle.sqrMagnitude > 0.0001f)
            return aisle;

        Vector3 forward = Vector3.ProjectOnPlane(itemTransform.forward, Vector3.up);
        if (forward.sqrMagnitude > 0.0001f)
            return forward.normalized;

        return Vector3.forward;
    }

    static bool TryGetOverlapBox(
        Transform itemTransform,
        BoxCollider itemCollider,
        out Vector3 center,
        out Vector3 halfExtents)
    {
        center = default;
        halfExtents = default;
        if (itemTransform == null || itemCollider == null)
            return false;

        center = itemTransform.TransformPoint(itemCollider.center);
        halfExtents = Vector3.Scale(itemCollider.size * 0.5f, itemTransform.lossyScale);
        halfExtents.x = Mathf.Max(halfExtents.x, 0.001f);
        halfExtents.y = Mathf.Max(halfExtents.y, 0.001f);
        halfExtents.z = Mathf.Max(halfExtents.z, 0.001f);
        return true;
    }

    static bool IsShelfOverlapCandidate(Collider overlap, BoxCollider itemCollider, Transform itemTransform)
    {
        if (overlap == null || overlap == itemCollider || overlap.isTrigger)
            return false;
        if (overlap.transform.IsChildOf(itemTransform))
            return false;
        return overlap.GetComponentInParent<CardShelf>() != null;
    }

    static bool TryFindFloorPoint(Vector3 fromPosition, out Vector3 floorPoint)
    {
        floorPoint = default;
        Vector3 origin = fromPosition + Vector3.up * 0.75f;
        const float maxDistance = 4f;

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            FloorHitBuffer,
            maxDistance,
            ~0,
            QueryTriggerInteraction.Ignore);

        float bestY = float.NegativeInfinity;
        bool found = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = FloorHitBuffer[i];
            if (hit.collider == null || hit.collider.isTrigger)
                continue;
            if (hit.collider.GetComponentInParent<CardShelf>() != null)
                continue;
            if (hit.collider.GetComponentInParent<WorldCard>() != null)
                continue;
            if (hit.collider.GetComponentInParent<WorldBoosterPack>() != null)
                continue;
            if (hit.collider.GetComponentInParent<FirstPersonController>() != null)
                continue;

            string objectName = hit.collider.gameObject.name;
            if (objectName.StartsWith("Shelf", System.StringComparison.OrdinalIgnoreCase)
                || objectName.StartsWith("Wall", System.StringComparison.OrdinalIgnoreCase)
                || objectName.StartsWith("Ceiling", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (hit.point.y > bestY)
            {
                bestY = hit.point.y;
                floorPoint = hit.point;
                found = true;
            }
        }

        if (!found)
            floorPoint = new Vector3(fromPosition.x, CardFactory.GroundSurfaceY(), fromPosition.z);

        return true;
    }
}
