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

    /// <summary>
    /// A standing pack's centre sits about half a card-height above the floor. Stay under shelf
    /// plinths (~32 cm) so a pack that actually landed on furniture keeps the pose physics found.
    /// </summary>
    const float MaxFloorStandingHeight = 0.28f;

    /// <summary>Returns false when the item was resting inside something and needs to settle again.</summary>
    public static bool TrySettle(WorldCard card, BoxCollider collider, Rigidbody body, int attempt)
    {
        if (card == null)
            return true;

        SeatOnPile(card.transform, body, collider, card, null);
        return true;
    }

    /// <summary>Returns false when the item was resting inside something and needs to settle again.</summary>
    public static bool TrySettle(WorldBoosterPack pack, BoxCollider collider, Rigidbody body, int attempt)
    {
        if (pack == null)
            return true;

        CardFactory.LiftAboveFloor(pack.transform, body);

        if (!BelongsToStack(pack.transform) && IsFloorRest(pack.transform))
        {
            pack.FlattenOntoFloor();
            CardCollisionUtility.ResolveRestingPenetration(pack.transform, collider, null, body);
            return true;
        }

        SeatOnPile(pack.transform, body, collider, null, pack);
        return true;
    }

    static void SeatOnPile(
        Transform itemTransform,
        Rigidbody body,
        BoxCollider collider,
        WorldCard card,
        WorldBoosterPack pack)
    {
        CardFactory.LiftAboveFloor(itemTransform, body);

        if (BelongsToStack(itemTransform))
        {
            LevelOntoStackPlane(itemTransform, body);
            if (card != null)
                CardGroundStack.ApplyStackHeight(card, placeOnTop: true, maxDownwardShift: MaxDownwardSnap);
            else if (pack != null)
                CardGroundStack.ApplyStackHeight(pack, placeOnTop: true, maxDownwardShift: MaxDownwardSnap);
        }

        CardCollisionUtility.ResolveRestingPenetration(itemTransform, collider, card, body);

        // Resting on a 4–5 card pile always reports a hair of PhysX overlap (contact offset vs
        // stack gap). Rejecting that used to WakeUp the body 4–5 times — the heartbeat jitter.
        // Lift one layer once if still intersecting, then freeze.
        if (CardCollisionUtility.OverlapsOtherItem(itemTransform, collider, card, body))
        {
            Vector3 position = itemTransform.position;
            position.y += CardGroundStack.StackStep;
            if (card != null)
                card.SetGroundRestPosition(position);
            else if (pack != null)
                pack.SetGroundRestPosition(position);
            else
                itemTransform.position = position;

            if (body != null)
                body.position = itemTransform.position;

            CardCollisionUtility.ResolveRestingPenetration(itemTransform, collider, card, body);
        }
    }

    static bool IsFloorRest(Transform itemTransform)
    {
        if (itemTransform == null)
            return false;

        if (itemTransform.GetComponentInParent<CardShelfSlot>() != null)
            return false;

        return itemTransform.position.y <= CardFactory.GroundHeightOffset() + MaxFloorStandingHeight;
    }

    /// <summary>
    /// An item leaning harder than <see cref="FlatUpDot"/> is standing on an edge or propped against
    /// geometry: flat stack heights say nothing about it, so physics keeps the pose it found.
    /// </summary>
    static bool BelongsToStack(Transform itemTransform) => Mathf.Abs(itemTransform.up.y) >= FlatUpDot;

    /// <summary>
    /// Takes the residual lean out of a settled item, keeping its facing and its yaw. Physics rests
    /// cards on a pile leaning a few degrees, and that lean is what the stack snap turned into
    /// interpenetration: a card tilted 10° has its two ends 2 cm above and below its own centre, so
    /// snapping the centre onto a 5 mm layer drove one end down through several cards underneath.
    /// Levelling first makes every card in a pile parallel, and then the layer spacing alone keeps
    /// them apart.
    /// </summary>
    static void LevelOntoStackPlane(Transform itemTransform, Rigidbody body)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(itemTransform.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 1e-6f)
            return;

        Quaternion level = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
        if (itemTransform.up.y < 0f)
            level *= Quaternion.Euler(0f, 0f, 180f);

        itemTransform.rotation = level;

        // With Auto Sync Transforms off the solver only reads the body pose, so the level has to go
        // there too — otherwise a rejected settle springs straight back to the leaning pose.
        if (body == null)
            return;

        body.rotation = level;
        if (!body.isKinematic)
            body.angularVelocity = Vector3.zero;
    }
}
