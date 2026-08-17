using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Keeps Q-thrown cards and packs in physics until they settle — no flatten/snap, no rotation/texture
/// changes. Once truly at rest on the floor, cards freeze (kinematic) so they stop costing solver time
/// and can no longer be woken up and jittered/sunk into by a freshly thrown item landing nearby.
/// Packs opt out of the freeze (allowKinematicFreeze = false) because WorldBoosterPack.HasActivePhysics
/// treats "no longer active physics" as "already flattened to ground orientation", which this settle
/// path intentionally never does.
/// </summary>
public static class CardThrownPhysics
{
    const float LandingColliderRefreshInterval = 0.03f;
    const float LandingColliderRadius = 2f;
    const float LandingLookaheadSeconds = 0.1f;
    const float SlowVelocityThresholdSq = 0.35f;
    const float MaxRecoveryFlightSeconds = 4f;
    const float RestSettleDelay = 0.2f;

    public static IEnumerator Monitor(
        Transform itemTransform,
        Rigidbody body,
        BoxCollider collider,
        Func<bool> isActive,
        Action onSettled = null,
        bool allowKinematicFreeze = true)
    {
        if (itemTransform == null || body == null || isActive == null)
            yield break;

        float shelfStuckTime = 0f;
        float elapsed = 0f;
        float groundedTime = 0f;
        // Start "due" so the very first frame already solidifies nearby ground cards — a short, fast
        // hand-drop can otherwise reach the floor before the first timed refresh ever fires.
        float colliderRefreshTimer = LandingColliderRefreshInterval;
        float restSettleTime = 0f;
        bool hasSettled = false;
        int landingScopeId = CardGroundStack.BeginLandingColliderScope();

        try
        {
            while (isActive())
            {
                elapsed += Time.deltaTime;
                colliderRefreshTimer += Time.deltaTime;
                if (!body.IsSleeping() && colliderRefreshTimer >= LandingColliderRefreshInterval)
                {
                    colliderRefreshTimer = 0f;
                    // Solidify slightly ahead of the current velocity too, so a fast-falling item meets an
                    // already-solid collider instead of racing a same-frame trigger-to-solid toggle.
                    Vector3 lookahead = itemTransform.position + body.linearVelocity * LandingLookaheadSeconds;
                    CardGroundStack.RefreshLandingColliderScope(
                        landingScopeId,
                        lookahead,
                        LandingColliderRadius);
                }

                bool slowEnough = body.linearVelocity.sqrMagnitude < SlowVelocityThresholdSq;
                float groundY = CardFactory.GroundHeightOffset();
                float maxGroundedY = groundY + CardGroundStack.StackStep * 64f + 0.25f;
                bool nearGround = itemTransform.position.y <= maxGroundedY;

                if (collider != null)
                {
                    CardThrowRecovery.AdvanceShelfStuckSettle(
                        ref shelfStuckTime,
                        ref elapsed,
                        ref groundedTime,
                        itemTransform,
                        collider,
                        body,
                        nearGround,
                        slowEnough,
                        MaxRecoveryFlightSeconds);
                }

                if (body.IsSleeping())
                {
                    if (nearGround && shelfStuckTime <= 0f)
                    {
                        restSettleTime += Time.deltaTime;
                        if (restSettleTime >= RestSettleDelay && !hasSettled)
                        {
                            hasSettled = true;

                            // Physics can rest a thin flat item a hair below/above where it should sit
                            // (or fully miss a trigger-based ground card whose landing collider toggled
                            // on a frame late). Snap ONLY the Y position onto the real stack height on
                            // top of whatever it is actually overlapping — position/rotation from the
                            // physics tumble are left untouched, so this never affects front/back facing.
                            onSettled?.Invoke();

                            // Truly at rest — freeze physics instead of leaving the solver running on it
                            // forever. This is what stops fast back-to-back throws from jittering/sinking
                            // into an already-settled pile, and stops paying per-frame physics cost for
                            // cards/packs that already stopped moving.
                            if (allowKinematicFreeze)
                            {
                                body.isKinematic = true;
                                yield break;
                            }
                        }
                    }
                    else
                    {
                        restSettleTime = 0f;
                        hasSettled = false;
                    }

                    yield return null;
                    continue;
                }

                restSettleTime = 0f;
                hasSettled = false;
                yield return null;
            }
        }
        finally
        {
            CardGroundStack.EndLandingColliderScope(landingScopeId);
        }
    }
}
