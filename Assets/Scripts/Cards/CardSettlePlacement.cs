using UnityEngine;

/// <summary>
/// Decides where a thrown card/pack actually comes to rest and whether it is safe to hand it over
/// to the sleep/freeze optimization. Runs once per settle attempt — never per frame.
/// </summary>
public static class CardSettlePlacement
{
    /// <summary>cos(20°) — below this the item leans or stands on an edge, so flat stack heights do not describe it.</summary>
    const float FlatUpDot = 0.94f;

    /// <summary>
    /// How far down the stack snap is allowed to pull an item. Physics rests a thin card a fraction
    /// of a millimetre off its ideal height, but a card resting on a cabinet plinth or wedged
    /// against one sits far above the floor-relative stack height — snapping that down is what
    /// buried cards inside geometry.
    /// </summary>
    const float MaxDownwardSnap = 0.0035f;

    /// <summary>Returns false when the item was resting inside static geometry and needs to settle again.</summary>
    public static bool TrySettle(WorldCard card, BoxCollider collider, Rigidbody body)
    {
        if (card == null)
            return true;

        if (IsFlat(card.transform))
            CardGroundStack.ApplyStackHeight(card, placeOnTop: true, maxDownwardShift: MaxDownwardSnap);

        return Accept(CardCollisionUtility.ResolveStaticPenetration(card.transform, collider, card, body), body);
    }

    /// <summary>Returns false when the item was resting inside static geometry and needs to settle again.</summary>
    public static bool TrySettle(WorldBoosterPack pack, BoxCollider collider, Rigidbody body)
    {
        if (pack == null)
            return true;

        if (IsFlat(pack.transform))
            CardGroundStack.ApplyStackHeight(pack, placeOnTop: true, maxDownwardShift: MaxDownwardSnap);

        return Accept(CardCollisionUtility.ResolveStaticPenetration(pack.transform, collider, null, body), body);
    }

    static bool IsFlat(Transform itemTransform) => Mathf.Abs(itemTransform.up.y) >= FlatUpDot;

    static bool Accept(bool wasBuried, Rigidbody body)
    {
        if (!wasBuried)
            return true;

        // It came to rest inside static geometry. Let it drop the last bit and settle again rather
        // than freezing it half-buried where nothing can push it back out.
        if (body != null && !body.isKinematic)
            body.WakeUp();

        return false;
    }
}
