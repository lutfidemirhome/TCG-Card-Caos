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
    const int ThrownSolverIterations = 18;

    const int ThrownSolverVelocityIterations = 8;

    /// <summary>
    /// Items that do end up overlapping have to ease apart. On the default budget a thin card
    /// overlapped by half its thickness is launched across the room instead of nudged out.
    /// Stay above 1 m/s so two cards that spawn already overlapping can separate in a few steps.
    /// </summary>
    const float ThrownMaxDepenetrationVelocity = 2f;

    /// <summary>
    /// Largest in-flight unstick per physics step. One card thickness — enough to leave coincident
    /// throws, not enough to slide them toward a shared landing point.
    /// </summary>
    static float MaxSpawnUnstick => CardDimensions.Thickness * CardDimensions.GroundCardScale;

    /// <summary>Relative speed used to peel two overlapping thrown items apart without a teleport.</summary>
    const float ThrownOverlapSeparateSpeed = 1.25f;

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
        // Auto Sync Transforms is off: the pose after unparenting lives on the transform until we
        // copy it, otherwise the first physics step sweeps from the old (hand / last freeze) pose.
        body.position = body.transform.position;
        body.rotation = body.transform.rotation;
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

    /// <summary>~14 mm in card-local space (~18 mm at ground scale). Still a card, less likely to rest on edge.</summary>
    public const float AuthoringColliderThickness = 0.014f;

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
            if (!TryResolveSinglePass(cardTransform, cardCollider, self, body, ignoreMovingBodies: true))
                break;

            moved = true;
        }

        return moved;
    }

    /// <summary>
    /// True when this item's box is inside another card or pack, including ones still in flight.
    /// The resting pass ignores moving bodies so it does not fight the solver; freezing while two
    /// thrown cards still overlap is what left rapid Q-throws clipped through each other.
    /// </summary>
    public static bool OverlapsOtherItem(
        Transform cardTransform,
        BoxCollider cardCollider,
        WorldCard self,
        Rigidbody body)
    {
        if (cardTransform == null || cardCollider == null || !cardCollider.enabled)
            return false;

        Vector3 center = cardTransform.TransformPoint(cardCollider.center);
        Vector3 halfExtents = ScaledHalfExtents(cardCollider, cardTransform);
        int overlapCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            OverlapBuffer,
            cardTransform.rotation,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider other = OverlapBuffer[i];
            if (ShouldIgnoreCollider(other, cardCollider, self, body, ignoreMovingBodies: false))
                continue;
            if (!IsCardOrPackCollider(other))
                continue;

            if (Physics.ComputePenetration(
                    cardCollider,
                    cardTransform.position,
                    cardTransform.rotation,
                    other,
                    other.transform.position,
                    other.transform.rotation,
                    out _,
                    out float distance)
                && distance > MinPenetration)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// One-shot unstick at throw spawn or a rejected settle. Caps travel at one card thickness so
    /// coincident throws separate without sliding toward a shared landing point.
    /// </summary>
    public static bool UnstickThrownSpawnOverlap(
        Transform cardTransform,
        BoxCollider cardCollider,
        WorldCard self,
        Rigidbody body)
    {
        return ResolveThrownFlightOverlap(cardTransform, cardCollider, self, body);
    }

    /// <summary>
    /// ContinuousDynamic does not build contacts for items that spawn already overlapping, so rapid
    /// Q-throws stay clipped until they freeze. Walk the in-flight list (not PhysX overlap, which
    /// misses the same-frame pair) and peel them apart along the penetration normal.
    /// </summary>
    public static bool ResolveThrownFlightOverlap(
        Transform cardTransform,
        BoxCollider cardCollider,
        WorldCard self,
        Rigidbody body)
    {
        if (cardTransform == null || cardCollider == null || !cardCollider.enabled || body == null)
            return false;

        bool moved = false;
        int selfId = cardTransform.GetInstanceID();
        WorldBoosterPack selfPack = self == null ? cardTransform.GetComponent<WorldBoosterPack>() : null;

        CardGroundStack.ForEachPhysicsCard(other =>
        {
            if (other == null || other == self)
                return;
            if (SeparateFromThrownItem(
                    cardTransform,
                    cardCollider,
                    body,
                    selfId,
                    other.PhysCollider as BoxCollider,
                    other.PhysicsBody,
                    other.IsPhysicsSimulating))
                moved = true;
        });

        CardGroundStack.ForEachPhysicsPack(other =>
        {
            if (other == null || other == selfPack)
                return;
            if (SeparateFromThrownItem(
                    cardTransform,
                    cardCollider,
                    body,
                    selfId,
                    other.PhysCollider,
                    other.PhysicsBody,
                    other.IsPhysicsSimulating))
                moved = true;
        });

        if (moved)
            body.WakeUp();

        return moved;
    }

    static bool SeparateFromThrownItem(
        Transform cardTransform,
        BoxCollider cardCollider,
        Rigidbody body,
        int selfId,
        BoxCollider otherCollider,
        Rigidbody otherBody,
        bool otherSimulating)
    {
        if (otherCollider == null || !otherCollider.enabled || otherCollider.isTrigger)
            return false;
        if (otherCollider == cardCollider)
            return false;
        if (otherBody != null && otherBody == body)
            return false;

        if (otherSimulating && otherBody != null && otherCollider.transform.GetInstanceID() < selfId
            && CardGroundStack.IsTrackedPhysicsTransform(cardTransform))
            return false;

        if (!TryGetSeparation(
                cardCollider,
                cardTransform,
                otherCollider,
                out Vector3 direction,
                out float distance))
            return false;

        bool share = otherSimulating
            && otherBody != null
            && !otherBody.isKinematic
            && CardGroundStack.IsTrackedPhysicsTransform(cardTransform);
        float push = Mathf.Min(distance + SeparationPush, MaxSpawnUnstick);
        if (share)
        {
            Vector3 half = direction * (push * 0.5f);
            cardTransform.position += half;
            body.position = cardTransform.position;
            otherBody.transform.position -= half;
            otherBody.position = otherBody.transform.position;

            ApplySeparatingVelocity(body, otherBody, direction);
            otherBody.WakeUp();
        }
        else
        {
            cardTransform.position += direction * push;
            body.position = cardTransform.position;
            ApplySeparatingVelocity(body, null, direction);
        }

        return true;
    }

    static bool TryGetSeparation(
        BoxCollider cardCollider,
        Transform cardTransform,
        BoxCollider otherCollider,
        out Vector3 direction,
        out float distance)
    {
        direction = Vector3.up;
        distance = 0f;

        if (Physics.ComputePenetration(
                cardCollider,
                cardTransform.position,
                cardTransform.rotation,
                otherCollider,
                otherCollider.transform.position,
                otherCollider.transform.rotation,
                out direction,
                out distance)
            && distance > MinPenetration)
        {
            return true;
        }

        if (!cardCollider.bounds.Intersects(otherCollider.bounds))
            return false;

        Vector3 delta = cardTransform.position - otherCollider.transform.position;
        if (delta.sqrMagnitude < 1e-8f)
            delta = Vector3.up;

        direction = delta.normalized;
        distance = MaxSpawnUnstick;
        return true;
    }

    static void ApplySeparatingVelocity(Rigidbody body, Rigidbody otherBody, Vector3 direction)
    {
        if (body == null)
            return;

        Vector3 relative = otherBody != null
            ? body.linearVelocity - otherBody.linearVelocity
            : body.linearVelocity;
        float closing = Vector3.Dot(relative, direction);
        if (closing >= ThrownOverlapSeparateSpeed)
            return;

        float add = ThrownOverlapSeparateSpeed - closing;
        if (otherBody != null && !otherBody.isKinematic)
        {
            float half = add * 0.5f;
            body.linearVelocity += direction * half;
            otherBody.linearVelocity -= direction * half;
            return;
        }

        body.linearVelocity += direction * add;
    }

    static bool TryResolveSinglePass(
        Transform cardTransform,
        BoxCollider cardCollider,
        WorldCard self,
        Rigidbody body,
        bool ignoreMovingBodies,
        float maxPush = -1f,
        bool cardsAndPacksOnly = false)
    {
        Vector3 center = cardTransform.TransformPoint(cardCollider.center);
        Vector3 halfExtents = ScaledHalfExtents(cardCollider, cardTransform);

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
            if (ShouldIgnoreCollider(other, cardCollider, self, body, ignoreMovingBodies))
                continue;
            if (cardsAndPacksOnly && !IsCardOrPackCollider(other))
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

            float push = distance + SeparationPush;
            if (maxPush > 0f)
                push = Mathf.Min(push, maxPush);

            // Hard teleport, not MovePosition: this runs on an already-resting body, and with
            // Auto Sync Transforms off the solver only sees the new pose through Rigidbody.position.
            cardTransform.position += direction * push;
            if (body != null)
                body.position = cardTransform.position;

            moved = true;
        }

        return moved;
    }

    static Vector3 ScaledHalfExtents(BoxCollider cardCollider, Transform cardTransform)
    {
        Vector3 halfExtents = Vector3.Scale(cardCollider.size * 0.5f, cardTransform.lossyScale);
        halfExtents.x = Mathf.Max(halfExtents.x - SeparationSkin, 0.0002f);
        halfExtents.y = Mathf.Max(halfExtents.y - SeparationSkin, 0.0002f);
        halfExtents.z = Mathf.Max(halfExtents.z - SeparationSkin, 0.0002f);
        return halfExtents;
    }

    static bool IsCardOrPackCollider(Collider other)
    {
        return other.GetComponentInParent<WorldCard>() != null
            || other.GetComponentInParent<WorldBoosterPack>() != null;
    }

    static bool ShouldIgnoreCollider(
        Collider other,
        Collider selfCollider,
        WorldCard self,
        Rigidbody body,
        bool ignoreMovingBodies)
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
        if (ignoreMovingBodies)
        {
            Rigidbody otherBody = other.attachedRigidbody;
            if (otherBody != null && !otherBody.isKinematic)
                return true;
        }

        return false;
    }
}
