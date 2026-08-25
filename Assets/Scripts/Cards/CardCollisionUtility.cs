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

    /// <summary>Mass of a single card or pack. Light enough to be thrown, heavy enough not to skate.</summary>
    const float ThrownMass = 0.05f;

    const float ThrownLinearDamping = 0.4f;
    const float ThrownAngularDamping = 0.8f;

    /// <summary>Extra solver work that keeps thin flat items from sinking into each other in a pile.</summary>
    const int ThrownSolverIterations = 12;

    const int ThrownSolverVelocityIterations = 4;

    /// <summary>
    /// Items that do end up overlapping have to ease apart. On the default budget a thin card
    /// overlapped by half its thickness is launched across the room instead of nudged out.
    /// </summary>
    const float ThrownMaxDepenetrationVelocity = 1f;

    /// <summary>Random launch spin, radians per second.</summary>
    const float ThrownSpinPitch = 0.2f;

    const float ThrownSpinYaw = 0.35f;

    static PhysicsMaterial _sharedPhysicMaterial;
    static FirstPersonController _cachedPlayer;
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

    /// <summary>
    /// Physics profile for anything the player throws. Cards and packs land in the same piles, so they
    /// have to share one profile — when these numbers drifted apart, packs jittered where cards did not.
    /// </summary>
    public static void ConfigureThrownBody(Rigidbody body)
    {
        if (body == null)
            return;

        body.mass = ThrownMass;
        body.linearDamping = ThrownLinearDamping;
        body.angularDamping = ThrownAngularDamping;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        // ContinuousDynamic (not just Continuous) is required so two fast items thrown back-to-back
        // resolve their collision against EACH OTHER instead of tunneling/overlapping for a frame.
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.solverIterations = ThrownSolverIterations;
        body.solverVelocityIterations = ThrownSolverVelocityIterations;
        body.maxDepenetrationVelocity = ThrownMaxDepenetrationVelocity;
    }

    /// <summary>
    /// Hands a body over to the solver for a throw. Also re-applies the profile, so an item that was
    /// frozen by an earlier settle and is being thrown again starts from the same physics as a new one.
    /// </summary>
    public static void LaunchThrownBody(Rigidbody body, Vector3 velocity)
    {
        if (body == null)
            return;

        ConfigureThrownBody(body);
        body.isKinematic = false;
        body.useGravity = true;
        body.constraints = RigidbodyConstraints.None;
        body.linearVelocity = velocity;
        // A little spin so a thrown card tumbles instead of gliding like a plate.
        body.angularVelocity = new Vector3(
            Random.Range(-ThrownSpinPitch, ThrownSpinPitch),
            Random.Range(-ThrownSpinYaw, ThrownSpinYaw),
            Random.Range(-ThrownSpinPitch, ThrownSpinPitch));
    }

    /// <summary>Keeps a thrown item from bouncing off the player who is throwing it.</summary>
    public static void IgnorePlayerCollision(Collider itemCollider)
    {
        if (itemCollider == null)
            return;

        if (_cachedPlayer == null)
            _cachedPlayer = Object.FindFirstObjectByType<FirstPersonController>();
        if (_cachedPlayer == null)
            return;

        Collider[] playerColliders = _cachedPlayer.GetComponentsInChildren<Collider>();
        for (int i = 0; i < playerColliders.Length; i++)
        {
            if (playerColliders[i] != null)
                Physics.IgnoreCollision(itemCollider, playerColliders[i], true);
        }
    }

    public static void ApplyFlatWorldSize(BoxCollider collider)
    {
        if (collider == null)
            return;

        collider.size = new Vector3(CardDimensions.Width, CardDimensions.Thickness, CardDimensions.Height);
        collider.center = Vector3.zero;
        ApplyToCollider(collider);
    }

    /// <summary>
    /// Editor Grabbit Fall only. Visual mesh stays at <see cref="CardDimensions.Thickness"/> (~4 mm);
    /// the physical box is slightly thicker so PhysX can generate contacts against a floor BoxCollider.
    /// Bake restores <see cref="ApplyFlatWorldSize"/>.
    /// </summary>
    public static void ApplyAuthoringWorldSize(BoxCollider collider)
    {
        if (collider == null)
            return;

        ApplyToCollider(collider);
        Vector3 size = collider.size;
        float minAxis = Mathf.Min(size.x, size.y, size.z);
        if (minAxis >= AuthoringColliderThickness)
            return;

        if (size.y <= size.x && size.y <= size.z)
            size.y = AuthoringColliderThickness;
        else if (size.x <= size.z)
            size.x = AuthoringColliderThickness;
        else
            size.z = AuthoringColliderThickness;

        collider.size = size;
    }

    /// <summary>~10 mm in card-local space (~13 mm at ground scale). Still a thin card, not a block.</summary>
    public const float AuthoringColliderThickness = 0.01f;

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
