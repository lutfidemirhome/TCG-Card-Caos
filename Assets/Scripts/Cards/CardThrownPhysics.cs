using System;
using System.Collections;
using UnityEngine;

[Serializable]
public sealed class ThrownPhysicsSaveState
{
    public bool isSimulating;
    public bool isSleeping;
    public Vector3 linearVelocity;
    public Vector3 angularVelocity;

    public static ThrownPhysicsSaveState Capture(Rigidbody body)
    {
        if (body == null || body.isKinematic)
            return null;
        bool sleeping = body.IsSleeping();
        return new ThrownPhysicsSaveState
        {
            isSimulating = true,
            isSleeping = sleeping,
            linearVelocity = sleeping ? Vector3.zero : body.linearVelocity,
            angularVelocity = sleeping ? Vector3.zero : body.angularVelocity,
        };
    }

    public void Apply(Rigidbody body)
    {
        body.isKinematic = false;
        body.useGravity = true;
        body.constraints = RigidbodyConstraints.None;
        CardCollisionUtility.ConfigureThrownBody(body);
        body.position = body.transform.position;
        body.rotation = body.transform.rotation;
        body.linearVelocity = IsFinite(linearVelocity) ? linearVelocity : Vector3.zero;
        body.angularVelocity = IsFinite(angularVelocity) ? angularVelocity : Vector3.zero;
        if (isSleeping)
            body.Sleep();
        else
            body.WakeUp();
    }

    static bool IsFinite(Vector3 value) =>
        !float.IsNaN(value.x) && !float.IsInfinity(value.x)
        && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
        && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
}

/// <summary>
/// Preserves the solver's pose, including tilted and elevated piles. Resting bodies sleep naturally
/// so new impacts and removal of their support can wake them again. Their local support colliders
/// stay solid for the whole throw lifetime, including sleep.
/// </summary>
public static class CardThrownPhysics
{
    const float LandingColliderRefreshInterval = 0.1f;
    const float LandingColliderRadius = 2f;
    const float LandingLookaheadSeconds = 0.2f;
    const float SlowVelocityThresholdSq = 0.35f;
    const float LandingRefreshTravelSq = 0.2f * 0.2f;
    const float SupportContactPadding = 0.012f;
    static readonly WaitForFixedUpdate WaitFixed = new WaitForFixedUpdate();
    static readonly WaitForSeconds WaitSleeping = new WaitForSeconds(0.25f);

    /// <param name="onSettled">
    /// Registers the sleeping pose without changing it or waking the body.
    /// </param>
    public static IEnumerator Monitor(
        Transform itemTransform,
        Rigidbody body,
        BoxCollider collider,
        Func<bool> isActive,
        Action onSettled = null,
        int landingScopeId = 0)
    {
        if (itemTransform == null || body == null || isActive == null)
            yield break;

        float shelfStuckTime = 0f;
        float recoveryTimer = 0f;
        float colliderRefreshTimer = LandingColliderRefreshInterval;
        bool registeredSleep = false;
        bool observedMotion = !body.IsSleeping();
        bool hasLandingPosition = false;
        Vector3 lastLandingPosition = default;
        bool ownsScope = landingScopeId == 0;
        if (ownsScope)
            landingScopeId = CardGroundStack.BeginLandingColliderScope();

        try
        {
            // Prime supports before the first simulation, but register sleep only after a physics
            // step. Save restore creates all bodies and restores their sleep states synchronously;
            // wait for that batch to end before recording the pile's resting poses.
            lastLandingPosition = body.IsSleeping()
                ? itemTransform.position
                : itemTransform.position + body.linearVelocity * LandingLookaheadSeconds;
            CardGroundStack.RefreshLandingColliderScope(landingScopeId, lastLandingPosition,
                body.IsSleeping() ? GetRestingSupportRadius(collider) : LandingColliderRadius);
            hasLandingPosition = true;
            colliderRefreshTimer = 0f;
            yield return WaitFixed;

            while (itemTransform != null && body != null && isActive())
            {
                if (body.IsSleeping())
                {
                    // A genuine cabinet penetration can itself fall asleep. Check once on landing,
                    // then only while that confirmed problem persists; ordinary sleepers do no queries.
                    if (observedMotion && collider != null && (!registeredSleep || shelfStuckTime > 0f))
                    {
                        CardThrowRecovery.AdvanceShelfStuckSettle(
                            ref shelfStuckTime, itemTransform, collider, body, true, 0.25f);
                        if (!body.IsSleeping())
                            continue;
                    }
                    if (!registeredSleep)
                    {
                        // Keep nearby authored supports solid for the sleeping pile's lifetime.
                        CardGroundStack.RefreshLandingColliderScope(landingScopeId, itemTransform.position,
                            GetRestingSupportRadius(collider));
                        if (!body.IsSleeping())
                            continue;
                        // Do not depenetrate a solver-settled pile here. Moving one resting card
                        // breaks its neighbours' contacts and causes repeated wake/settle cycles.
                        onSettled?.Invoke();
                        registeredSleep = true;
                        if (observedMotion)
                            GameSaveDirtyTracker.MarkDirty();
                    }
                    // No per-frame coroutine, overlap query or transform write for a sleeping pile.
                    yield return WaitSleeping;
                    continue;
                }

                if (registeredSleep)
                {
                    registeredSleep = false;
                    hasLandingPosition = false;
                    colliderRefreshTimer = LandingColliderRefreshInterval;
                    shelfStuckTime = 0f;
                    GameSaveDirtyTracker.MarkDirty();
                }
                observedMotion = true;
                colliderRefreshTimer += Time.fixedDeltaTime;
                Vector3 lookahead = itemTransform.position + body.linearVelocity * LandingLookaheadSeconds;
                if (!hasLandingPosition || (colliderRefreshTimer >= LandingColliderRefreshInterval
                    && (lookahead - lastLandingPosition).sqrMagnitude >= LandingRefreshTravelSq))
                {
                    colliderRefreshTimer = 0f;
                    lastLandingPosition = lookahead;
                    hasLandingPosition = true;
                    CardGroundStack.RefreshLandingColliderScope(landingScopeId, lookahead, LandingColliderRadius);
                }

                // The authored ground offset includes visual padding. Clamping to it every step
                // lifts an already resting card off its contact and prevents natural sleep.
                RecoverBelowFloor(itemTransform, body, collider);
                recoveryTimer += Time.fixedDeltaTime;
                if (collider != null && recoveryTimer >= LandingColliderRefreshInterval)
                {
                    CardThrowRecovery.AdvanceShelfStuckSettle(
                        ref shelfStuckTime, itemTransform, collider, body,
                        body.linearVelocity.sqrMagnitude < SlowVelocityThresholdSq, recoveryTimer);
                    recoveryTimer = 0f;
                }
                yield return WaitFixed;
            }
        }
        finally
        {
            if (ownsScope)
                CardGroundStack.EndLandingColliderScope(landingScopeId);
        }
    }

    static float GetRestingSupportRadius(BoxCollider collider)
    {
        if (collider == null)
            return 0.5f;
        float cardHalfDiagonal = new Vector2(CardDimensions.Width, CardDimensions.Height).magnitude
            * CardDimensions.GroundCardScale * 0.5f;
        return Mathf.Max(0.5f, collider.bounds.extents.magnitude + cardHalfDiagonal + 0.02f);
    }

    static void RecoverBelowFloor(Transform itemTransform, Rigidbody body, BoxCollider collider)
    {
        if (collider == null)
            return;
        float floorY = CardFactory.GroundSurfaceY();
        Bounds bounds = collider.bounds;
        if (bounds.max.y >= floorY - 0.02f)
            return;

        // Only recover a collider that has completely passed below the lowest floor. Its rotated
        // bounds determine the lift, so an edge-on card or thick pack is also brought fully above it.
        Vector3 position = body.position + Vector3.up * (floorY + 0.002f - bounds.min.y);
        itemTransform.position = position;
        body.position = position;
        Vector3 velocity = body.linearVelocity;
        velocity.y = Mathf.Max(0f, velocity.y);
        body.linearVelocity = velocity;
    }

    /// <summary>
    /// Removing a static authored card is not an impact. Wake only touching physical neighbours so
    /// a sleeping pile can follow its missing support; leave distant piles asleep and apply no forces.
    /// </summary>
    public static void WakeSupportedBodies(Collider support)
    {
        if (support == null || !support.enabled || !support.gameObject.activeInHierarchy || support.isTrigger)
            return;

        Bounds contactBounds = support.bounds;
        contactBounds.Expand(SupportContactPadding * 2f);
        for (int i = 0; i < CardGroundStack.PhysicsCardCount; i++)
        {
            WorldCard card = CardGroundStack.PhysicsCardAt(i);
            if (card != null)
                WakeTouchingBody(card.PhysicsBody, card.PhysCollider, support, contactBounds);
        }
        for (int i = 0; i < CardGroundStack.PhysicsPackCount; i++)
        {
            WorldBoosterPack pack = CardGroundStack.PhysicsPackAt(i);
            if (pack != null)
                WakeTouchingBody(pack.PhysicsBody, pack.PhysCollider, support, contactBounds);
        }
    }

    static void WakeTouchingBody(Rigidbody body, Collider collider, Collider support, Bounds contactBounds)
    {
        if (body == null || body.isKinematic || !body.IsSleeping() || collider == null
            || collider == support || !collider.enabled || collider.isTrigger)
            return;
        if (contactBounds.Intersects(collider.bounds))
            body.WakeUp();
    }
}
