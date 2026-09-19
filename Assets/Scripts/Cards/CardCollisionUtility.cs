using UnityEngine;
using System.Collections.Generic;

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

    // Keep the visible faces inside the physical box despite small solver penetration and the
    // authored instanced-ground depth bias (up to 0.4 mm). This is a world-space margin per face.
    const float CardSurfacePadding = 0.001f;
    const float SpawnPenetrationTolerance = 0.0005f;
    static readonly Collider[] PileOverlapBuffer = new Collider[128];

    /// <summary>Random launch spin, radians per second.</summary>
    const float ThrownSpinPitch = 0.2f;

    const float ThrownSpinYaw = 0.35f;

    static PhysicsMaterial _sharedPhysicMaterial;
    static FirstPersonController _cachedPlayer;
    static readonly List<Collider> PlayerColliderScratch = new List<Collider>(32);
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
                    bounciness = 0f,
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

        PlayerColliderScratch.Clear();
        if (_cachedPlayer != null)
            _cachedPlayer.GetComponentsInChildren<Collider>(false, PlayerColliderScratch);
        for (int i = 0; i < PlayerColliderScratch.Count; i++)
        {
            Collider playerCollider = PlayerColliderScratch[i];
            if (playerCollider == null || playerCollider == itemCollider)
                continue;

            // HandAnchor is under the player's camera. Its held cards/packs are items,
            // not player colliders, even when their Collider component is disabled.
            // Ignoring those pairs also lets later throws pass through earlier throws.
            if (IsCardOrPackCollider(playerCollider))
            {
                RestoreItemCollision(itemCollider, playerCollider);
                continue;
            }

            Physics.IgnoreCollision(itemCollider, playerCollider, true);
        }
        PlayerColliderScratch.Clear();

        // Item pairs are never ignored above. There is no need to scan every world item
        // on each throw/restore; ignored pairs are not persisted in saves.
    }

    static void RestoreItemCollision(Collider itemCollider, Collider other)
    {
        if (other == null || other == itemCollider || !itemCollider.enabled || !other.enabled
            || !itemCollider.gameObject.activeInHierarchy || !other.gameObject.activeInHierarchy)
            return;

        if (Physics.GetIgnoreCollision(itemCollider, other))
            Physics.IgnoreCollision(itemCollider, other, false);
    }

    public static void ApplyFlatWorldSize(BoxCollider collider)
    {
        if (collider == null)
            return;

        collider.size = new Vector3(CardDimensions.Width, CardDimensions.Thickness, CardDimensions.Height);
        if (Application.isPlaying)
        {
            Vector3 size = collider.size;
            size.y += 2f * CardSurfacePadding / Mathf.Max(Mathf.Abs(collider.transform.lossyScale.y), 0.001f);
            collider.size = size;
        }
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
    /// Separate genuinely overlapping throw spawns at their final size. A one-card-thickness budget
    /// cannot clear several coincident/tilted cards. Recheck the new pose after each full minimum
    /// translation, including walls and floor, without imparting velocity or moving the old pile.
    /// </summary>
    public static bool UnstickThrownSpawnOverlap(
        Transform cardTransform, BoxCollider cardCollider, WorldCard self, Rigidbody body)
    {
        return ResolveSpawnOverlaps(cardTransform, cardCollider, self, body);
    }

    static bool ResolveSpawnOverlaps(
        Transform itemTransform, BoxCollider collider, WorldCard self, Rigidbody body)
    {
        if (itemTransform == null || collider == null || !collider.enabled || body == null || body.isKinematic)
            return false;

        bool moved = false;
        for (int iteration = 0; iteration < MaxResolveIterations; iteration++)
        {
            Vector3 position = body.position;
            Quaternion rotation = body.rotation;
            Vector3 half = Vector3.Scale(collider.size * 0.5f, itemTransform.lossyScale);
            half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
            Vector3 center = position + rotation * Vector3.Scale(collider.center, itemTransform.lossyScale);
            float radius = half.magnitude;
            int count = Physics.OverlapBoxNonAlloc(center, half, PileOverlapBuffer, rotation,
                ~0, QueryTriggerInteraction.Ignore);
            Collider[] overlaps = PileOverlapBuffer;
            if (count == overlaps.Length)
            {
                // A throw starting inside a dense pile must not lose contacts at the buffer limit.
                // This allocation is confined to spawn correction, never a frame loop.
                overlaps = Physics.OverlapBox(center, half, rotation, ~0, QueryTriggerInteraction.Ignore);
                count = overlaps.Length;
            }

            float deepest = SpawnPenetrationTolerance;
            Vector3 correction = Vector3.zero;
            for (int i = 0; i < count; i++)
                ConsiderSpawnOverlap(collider, body, self, overlaps[i], ref deepest, ref correction);

            // A second throw can be created before PhysX updates its broadphase. Read the
            // tracked bodies' current poses explicitly so that same-frame spawns are covered.
            for (int i = 0; i < CardGroundStack.PhysicsCardCount; i++)
            {
                WorldCard other = CardGroundStack.PhysicsCardAt(i);
                if (other != null && other != self
                    && CouldOverlapSpawn(center, radius, other.PhysCollider, other.PhysicsBody))
                    ConsiderSpawnOverlap(collider, body, self, other.PhysCollider, ref deepest, ref correction);
            }
            for (int i = 0; i < CardGroundStack.PhysicsPackCount; i++)
            {
                WorldBoosterPack other = CardGroundStack.PhysicsPackAt(i);
                if (other != null
                    && CouldOverlapSpawn(center, radius, other.PhysCollider, other.PhysicsBody))
                    ConsiderSpawnOverlap(collider, body, self, other.PhysCollider, ref deepest, ref correction);
            }

            if (correction.sqrMagnitude == 0f)
                break;

            position += correction;
            itemTransform.position = position;
            body.position = position;
            moved = true;
        }

        return moved;
    }

    static bool CouldOverlapSpawn(Vector3 center, float radius, Collider other, Rigidbody otherBody)
    {
        if (other == null || !other.enabled || !other.gameObject.activeInHierarchy)
            return false;
        // The registered items use root BoxColliders. Fall back to the exact check for
        // any other shape/hierarchy instead of assuming its dimensions or world pose.
        if (!(other is BoxCollider box) || otherBody == null || box.transform != otherBody.transform)
            return true;

        // Reject distant items before the component lookups and penetration query. Use the
        // body's pose, not collider.bounds: a same-frame throw may not be in the broadphase yet.
        Vector3 scale = box.transform.lossyScale;
        Vector3 otherCenter = otherBody.position + otherBody.rotation * Vector3.Scale(box.center, scale);
        float otherRadius = Vector3.Scale(box.size * 0.5f, scale).magnitude;
        float maxDistance = radius + otherRadius + SeparationPush;
        return (center - otherCenter).sqrMagnitude <= maxDistance * maxDistance;
    }

    static void ConsiderSpawnOverlap(
        BoxCollider collider, Rigidbody body, WorldCard self, Collider other,
        ref float deepest, ref Vector3 correction)
    {
        if (ShouldIgnoreCollider(other, collider, self, body, ignoreMovingBodies: false)
            || !other.enabled || !other.gameObject.activeInHierarchy)
            return;
        if ((collider.excludeLayers.value & (1 << other.gameObject.layer)) != 0
            || Physics.GetIgnoreLayerCollision(collider.gameObject.layer, other.gameObject.layer)
            || Physics.GetIgnoreCollision(collider, other))
            return;

        Rigidbody otherBody = other.attachedRigidbody;
        Vector3 otherPosition = other.transform.position;
        Quaternion otherRotation = other.transform.rotation;
        if (otherBody != null && other.transform == otherBody.transform)
        {
            // Interpolated render transforms lag a physics step. Collision checks must compare
            // physics poses, otherwise visually lagging cards can produce a false correction.
            otherPosition = otherBody.position;
            otherRotation = otherBody.rotation;
        }
        if (!Physics.ComputePenetration(collider, body.position, body.rotation,
                other, otherPosition, otherRotation, out Vector3 direction, out float distance)
            || distance <= deepest)
            return;

        deepest = distance;
        correction = direction * (distance + SeparationPush);
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
        // Physics queries do not apply per-collider layer exclusions themselves.
        if (selfCollider != null && (other.excludeLayers.value & (1 << selfCollider.gameObject.layer)) != 0)
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
