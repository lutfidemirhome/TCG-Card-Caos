using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finds world cards along the view ray without enabling thousands of physics colliders.
/// </summary>
public static class CardGroundQuery
{
    static readonly List<WorldCard> ShelfCards = new List<WorldCard>(64);
    static readonly HashSet<WorldCard> ShelfCardSet = new HashSet<WorldCard>();
    static readonly List<WorldCard> GroundCandidateScratch = new List<WorldCard>(128);

    public static void TrackShelfCard(WorldCard card)
    {
        if (card == null || !ShelfCardSet.Add(card))
            return;

        ShelfCards.Add(card);
    }

    public static void UntrackShelfCard(WorldCard card)
    {
        if (card == null || !ShelfCardSet.Remove(card))
            return;

        ShelfCards.Remove(card);
    }

    public static void ClearShelfCards()
    {
        ShelfCards.Clear();
        ShelfCardSet.Clear();
    }

    public static bool TryRaycastWorldCard(Ray ray, float maxDistance, out WorldCard hitCard, out float hitDistance)
    {
        WorldCard bestCard = null;
        float bestDistance = float.MaxValue;

        CardGroundStack.CollectRayCandidates(ray, maxDistance, GroundCandidateScratch);
        for (int i = 0; i < GroundCandidateScratch.Count; i++)
            TryUpdateBestHit(ray, maxDistance, GroundCandidateScratch[i], ref bestCard, ref bestDistance);

        for (int i = 0; i < ShelfCards.Count; i++)
            TryUpdateBestHit(ray, maxDistance, ShelfCards[i], ref bestCard, ref bestDistance);

        hitCard = bestCard;
        hitDistance = bestDistance;
        return hitCard != null;
    }

    static void TryUpdateBestHit(
        Ray ray,
        float maxDistance,
        WorldCard candidate,
        ref WorldCard bestCard,
        ref float bestDistance)
    {
        if (candidate == null || candidate.IsInHand)
            return;

        if (candidate.GetComponent<Rigidbody>() != null)
            return;

        if (!TryRayHitCard(ray, candidate, maxDistance, out float distance))
            return;

        if (distance >= bestDistance)
            return;

        bestDistance = distance;
        bestCard = candidate;
    }

    static bool TryRayHitCard(Ray ray, WorldCard card, float maxDistance, out float distance)
    {
        distance = float.MaxValue;
        if (card == null)
            return false;

        Vector3 halfExtents = GetHalfExtents(card);
        bool onShelf = card.GetComponentInParent<CardShelfSlot>() != null;
        Vector3 center = card.transform.position;
        if (!onShelf)
            center.y = CardGroundStack.GetDrawWorldY(card);

        if (!TryRayIntersectOrientedBox(ray, center, card.transform.rotation, halfExtents, out distance))
            return false;

        return distance >= 0f && distance <= maxDistance;
    }

    static Vector3 GetHalfExtents(WorldCard card)
    {
        float scale = Mathf.Max(card.transform.lossyScale.x, CardDimensions.WorldCardScale);
        bool onShelf = card.GetComponentInParent<CardShelfSlot>() != null;
        if (onShelf)
        {
            return new Vector3(
                CardDimensions.Width * scale * 0.5f,
                CardDimensions.Height * scale * 0.5f,
                CardDimensions.Thickness * scale * 0.5f);
        }

        return new Vector3(
            CardDimensions.Width * scale * 0.5f,
            CardDimensions.Thickness * scale * 0.5f,
            CardDimensions.Height * scale * 0.5f);
    }

    static bool TryRayIntersectOrientedBox(
        Ray worldRay,
        Vector3 center,
        Quaternion rotation,
        Vector3 halfExtents,
        out float distance)
    {
        Matrix4x4 toLocal = Matrix4x4.TRS(center, rotation, Vector3.one).inverse;
        Vector3 localOrigin = toLocal.MultiplyPoint3x4(worldRay.origin);
        Vector3 localDirection = toLocal.MultiplyVector(worldRay.direction);

        float tMin = 0f;
        float tMax = float.MaxValue;

        TestAxis(localOrigin.x, localDirection.x, halfExtents.x, ref tMin, ref tMax);
        TestAxis(localOrigin.y, localDirection.y, halfExtents.y, ref tMin, ref tMax);
        TestAxis(localOrigin.z, localDirection.z, halfExtents.z, ref tMin, ref tMax);

        if (tMin > tMax)
        {
            distance = float.MaxValue;
            return false;
        }

        distance = tMin >= 0f ? tMin : tMax;
        return distance >= 0f;
    }

    static void TestAxis(float origin, float direction, float halfExtent, ref float tMin, ref float tMax)
    {
        if (Mathf.Abs(direction) < 1e-6f)
        {
            if (origin < -halfExtent || origin > halfExtent)
                tMin = float.MaxValue;
            return;
        }

        float t1 = (-halfExtent - origin) / direction;
        float t2 = (halfExtent - origin) / direction;
        if (t1 > t2)
        {
            float swap = t1;
            t1 = t2;
            t2 = swap;
        }

        tMin = Mathf.Max(tMin, t1);
        tMax = Mathf.Min(tMax, t2);
    }
}
