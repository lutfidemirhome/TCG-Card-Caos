using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Keeps Q-thrown cards and packs in physics until picked up — no flatten/snap settle.
/// </summary>
public static class CardThrownPhysics
{
    const float LandingColliderRefreshInterval = 0.08f;
    const float LandingColliderRadius = 1.75f;
    const float SlowVelocityThresholdSq = 0.35f;
    const float MaxRecoveryFlightSeconds = 4f;

    public static IEnumerator Monitor(
        Transform itemTransform,
        Rigidbody body,
        BoxCollider collider,
        Func<bool> isActive)
    {
        if (itemTransform == null || body == null || isActive == null)
            yield break;

        float shelfStuckTime = 0f;
        float elapsed = 0f;
        float groundedTime = 0f;
        float colliderRefreshTimer = 0f;
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
                    CardGroundStack.RefreshLandingColliderScope(
                        landingScopeId,
                        itemTransform.position,
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
                    yield return null;
                    continue;
                }

                yield return null;
            }
        }
        finally
        {
            CardGroundStack.EndLandingColliderScope(landingScopeId);
        }
    }
}
