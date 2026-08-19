using UnityEngine;

/// <summary>
/// Keeps flat world cards from resting inside static geometry (wall bases, shelf lips, etc.).
/// </summary>
public static class CardCollisionUtility
{
    const int MaxResolveIterations = 10;
    const int OverlapBufferSize = 32;

    /// <summary>
    /// How much the overlap query box is pulled in from the collider so that merely touching a surface
    /// does not read as being inside it. It has to stay far below a card's own half thickness (2.6 mm at
    /// ground scale): a larger skin collapsed the query box in Y down to a plane, so a card frozen
    /// halfway inside another card reported no overlap at all and was never pushed back out.
    /// </summary>
    const float SeparationSkin = 0.0003f;

    /// <summary>Clearance left behind after separating, roughly a fifth of a card thickness.</summary>
    const float SeparationPush = 0.0004f;

    /// <summary>
    /// Overlaps shallower than this are contact-offset noise from a card resting on a surface, not
    /// penetration. Acting on them would keep re-settling every card that landed perfectly well, and
    /// at a sixth of a card thickness they are invisible anyway.
    /// </summary>
    const float MinPenetration = 0.0008f;

    /// <summary>
    /// Unity's 1 cm project default is roughly twice a card's own physical thickness, so a flat card
    /// generates contacts a full card-height away from its surface: piles jitter and a landed card
    /// keeps nudging itself. Stay well under half the card thickness instead.
    /// </summary>
    const float ContactOffset = 0.0018f;

    static PhysicsMaterial _sharedPhysicMaterial;
    static readonly Collider[] OverlapBuffer = new Collider[OverlapBufferSize];

    public static PhysicsMaterial SharedPhysicMaterial
    {
        get
        {
            if (_sharedPhysicMaterial == null)
            {
                _sharedPhysicMaterial = new PhysicsMaterial("CardSurface")
                {
                    dynamicFriction = 0.65f,
                    staticFriction = 0.75f,
                    bounciness = 0.04f,
                    frictionCombine = PhysicsMaterialCombine.Maximum,
                    bounceCombine = PhysicsMaterialCombine.Minimum,
                };
            }

            return _sharedPhysicMaterial;
        }
    }

    public static void ApplyToCollider(BoxCollider collider)
    {
        if (collider == null)
            return;

        collider.material = SharedPhysicMaterial;
        collider.contactOffset = ContactOffset;
    }

    public static void ApplyFlatWorldSize(BoxCollider collider)
    {
        if (collider == null)
            return;

        collider.size = new Vector3(CardDimensions.Width, CardDimensions.Thickness, CardDimensions.Height);
        collider.center = Vector3.zero;
        ApplyToCollider(collider);
    }

    public static void ApplyUprightShelfSize(BoxCollider collider)
    {
        if (collider == null)
            return;

        collider.size = new Vector3(CardDimensions.Width, CardDimensions.Height, CardDimensions.Thickness);
        collider.center = Vector3.zero;
    }

    /// <summary>
    /// Pushes the card out of anything already at rest that it is currently inside — static geometry
    /// (floor, cabinet plinths, wall bases) as well as cards and packs that have already settled.
    /// Returns true when the card actually had to be moved.
    /// </summary>
    public static bool ResolveRestingPenetration(
        Transform cardTransform,
        BoxCollider cardCollider,
        WorldCard self,
        Rigidbody body = null)
    {
        if (cardTransform == null || cardCollider == null || !cardCollider.enabled)
            return false;

        bool moved = false;
        for (int iteration = 0; iteration < MaxResolveIterations; iteration++)
        {
            if (!TryResolveSinglePass(cardTransform, cardCollider, self, body))
                break;

            moved = true;
        }

        return moved;
    }

    static bool TryResolveSinglePass(
        Transform cardTransform,
        BoxCollider cardCollider,
        WorldCard self,
        Rigidbody body)
    {
        Vector3 center = cardTransform.TransformPoint(cardCollider.center);
        Vector3 halfExtents = Vector3.Scale(cardCollider.size * 0.5f, cardTransform.lossyScale);
        halfExtents.x = Mathf.Max(halfExtents.x - SeparationSkin, 0.0002f);
        halfExtents.y = Mathf.Max(halfExtents.y - SeparationSkin, 0.0002f);
        halfExtents.z = Mathf.Max(halfExtents.z - SeparationSkin, 0.0002f);

        int overlapCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            OverlapBuffer,
            cardTransform.rotation,
            ~0,
            QueryTriggerInteraction.Ignore);

        bool moved = false;
        for (int i = 0; i < overlapCount; i++)
        {
            Collider other = OverlapBuffer[i];
            if (ShouldIgnoreCollider(other, cardCollider, self, body))
                continue;

            if (!Physics.ComputePenetration(
                    cardCollider,
                    cardTransform.position,
                    cardTransform.rotation,
                    other,
                    other.transform.position,
                    other.transform.rotation,
                    out Vector3 direction,
                    out float distance))
            {
                continue;
            }

            if (distance <= MinPenetration)
                continue;

            // Hard teleport, not MovePosition: this runs on an already-resting body, and with
            // Auto Sync Transforms off the solver only sees the new pose through Rigidbody.position.
            cardTransform.position += direction * (distance + SeparationPush);
            if (body != null)
                body.position = cardTransform.position;

            moved = true;
        }

        return moved;
    }

    static bool ShouldIgnoreCollider(Collider other, Collider selfCollider, WorldCard self, Rigidbody body)
    {
        if (other == null || other == selfCollider)
            return true;
        if (other.isTrigger)
            return true;

        // Own colliders, including a pack's inner card proxy — separating from those would shove the
        // item across the room chasing its own body.
        if (body != null && other.attachedRigidbody == body)
            return true;
        if (self != null && other.GetComponentInParent<WorldCard>() == self)
            return true;

        if (other.GetComponentInParent<FirstPersonController>() != null)
            return true;

        // Anything the solver is still moving will sort itself out on its own; only items already at
        // rest are separated here. Settled cards and packs keep a frozen kinematic body, so they pass
        // this test — without that, two cards that crossed while landing froze inside each other and
        // nothing could ever push them apart again.
        Rigidbody otherBody = other.attachedRigidbody;
        if (otherBody != null && !otherBody.isKinematic)
            return true;

        return false;
    }
}
