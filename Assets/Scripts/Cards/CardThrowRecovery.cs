using UnityEngine;

/// <summary>
/// Recover a thrown item genuinely wedged inside cabinet geometry. Height alone is never an
/// error: an item can rest naturally on a tall pile, on furniture, or on the upper floor.
/// </summary>
public static class CardThrowRecovery
{
    public const float ShelfStuckRecoveryDelay = 2f;

    const float MinShelfPenetration = 0.001f;
    const float LiftClearance = 0.04f;
    const int OverlapBufferSize = 32;

    static readonly Collider[] OverlapBuffer = new Collider[OverlapBufferSize];

    struct ShelfOverlapResult
    {
        public bool TouchesShelf;
        public CardShelf NearestShelf;
        public Vector3 PenetrationDirection;
        public float PenetrationDepth;
    }

    static bool ShouldTrackShelfStuck(
        Transform itemTransform,
        BoxCollider itemCollider,
        bool slowEnough)
    {
        if (itemTransform == null || !slowEnough)
            return false;

        if (itemTransform.GetComponentInParent<CardShelfSlot>() != null)
            return false;

        if (IsRestingOnLadder(itemTransform, itemCollider))
            return false;

        return TryQueryShelfOverlaps(itemTransform, itemCollider, out ShelfOverlapResult overlap)
            && overlap.PenetrationDepth > MinShelfPenetration;
    }

    /// <summary>
    /// Check cabinet penetration at the monitor's reduced polling rate. The caller supplies
    /// elapsed simulation time since its previous check so the delay does not depend on polling rate.
    /// </summary>
    public static void AdvanceShelfStuckSettle(
        ref float shelfStuckTime,
        Transform itemTransform,
        BoxCollider itemCollider,
        Rigidbody body,
        bool slowEnough,
        float deltaTime)
    {
        if (itemCollider != null
            && ShouldTrackShelfStuck(itemTransform, itemCollider, slowEnough))
        {
            shelfStuckTime += Mathf.Max(0f, deltaTime);
            if (shelfStuckTime >= ShelfStuckRecoveryDelay
                && TryRecoverShelfStuckThrow(itemTransform, itemCollider, body))
            {
                shelfStuckTime = 0f;
            }
        }
        else
        {
            shelfStuckTime = 0f;
        }
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

        if (IsRestingOnLadder(itemTransform, itemCollider))
            return false;

        if (!TryQueryShelfOverlaps(itemTransform, itemCollider, out ShelfOverlapResult overlap)
            || overlap.PenetrationDepth <= MinShelfPenetration)
            return false;

        Vector3 pushOut = ComputeShelfPushOut(itemTransform, overlap);
        Vector3 pos = itemTransform.position;
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

    static bool IsRestingOnLadder(Transform itemTransform, BoxCollider itemCollider)
    {
        if (itemTransform == null || itemCollider == null)
            return false;

        if (!TryGetOverlapBox(itemTransform, itemCollider, out Vector3 center, out Vector3 halfExtents))
            return false;

        int overlapCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            OverlapBuffer,
            itemTransform.rotation,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider overlap = OverlapBuffer[i];
            if (overlap == null || overlap == itemCollider || overlap.isTrigger)
                continue;
            if (overlap.transform.IsChildOf(itemTransform))
                continue;
            if (IsLadderObject(overlap.gameObject))
                return true;
        }

        return false;
    }

    static bool IsLadderObject(GameObject go)
    {
        Transform current = go != null ? go.transform : null;
        while (current != null)
        {
            string objectName = current.name;
            if (objectName.Equals("Ladder", System.StringComparison.OrdinalIgnoreCase)
                || objectName.IndexOf("Stair", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            current = current.parent;
        }

        return false;
    }

    static bool IsShelfOverlapCandidate(Collider overlap, BoxCollider itemCollider, Transform itemTransform)
    {
        if (overlap == null || overlap == itemCollider || overlap.isTrigger)
            return false;
        if (overlap.transform.IsChildOf(itemTransform))
            return false;
        if ((overlap.excludeLayers.value & (1 << itemCollider.gameObject.layer)) != 0
            || (itemCollider.excludeLayers.value & (1 << overlap.gameObject.layer)) != 0)
            return false;
        // A card placed in a shelf slot inherits the shelf parent, but it is still an item;
        // landing against it must not trigger cabinet-recovery impulses.
        if (overlap.GetComponentInParent<WorldCard>() != null
            || overlap.GetComponentInParent<WorldBoosterPack>() != null)
            return false;
        return overlap.GetComponentInParent<CardShelf>() != null;
    }
}
