using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finds world cards along the view ray without enabling thousands of physics colliders.
/// </summary>
public static class CardGroundQuery
{
    struct CardRayHit
    {
        public WorldCard Card;
        public float Distance;
    }

    static readonly List<WorldCard> ShelfCards = new List<WorldCard>(64);
    static readonly HashSet<WorldCard> ShelfCardSet = new HashSet<WorldCard>();
    static readonly List<WorldCard> GroundCandidateScratch = new List<WorldCard>(128);
    static readonly List<CardRayHit> HitScratch = new List<CardRayHit>(32);

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
        HitScratch.Clear();

        CardGroundStack.CollectRayCandidates(ray, maxDistance, GroundCandidateScratch);
        for (int i = 0; i < GroundCandidateScratch.Count; i++)
            TryAddHit(ray, maxDistance, GroundCandidateScratch[i]);

        for (int i = 0; i < ShelfCards.Count; i++)
            TryAddHit(ray, maxDistance, ShelfCards[i]);

        if (HitScratch.Count == 0)
        {
            hitCard = null;
            hitDistance = float.MaxValue;
            return false;
        }

        int nearestIndex = 0;
        for (int i = 1; i < HitScratch.Count; i++)
        {
            if (HitScratch[i].Distance < HitScratch[nearestIndex].Distance)
                nearestIndex = i;
        }

        WorldCard nearest = HitScratch[nearestIndex].Card;
        WorldCard bestCard = nearest;
        int bestLayer = nearest.GroundStackLayer;
        float bestDistance = HitScratch[nearestIndex].Distance;

        for (int i = 0; i < HitScratch.Count; i++)
        {
            WorldCard candidate = HitScratch[i].Card;
            if (candidate == null || candidate == nearest)
                continue;
            if (candidate.GroundStackLayer <= bestLayer)
                continue;
            if (!CardGroundStack.OverlapsOnGround(candidate, nearest))
                continue;

            bestCard = candidate;
            bestLayer = candidate.GroundStackLayer;
            bestDistance = HitScratch[i].Distance;
        }

        hitCard = bestCard;
        hitDistance = bestDistance;
        return hitCard != null;
    }

    static void TryAddHit(Ray ray, float maxDistance, WorldCard candidate)
    {
        if (candidate == null || candidate.IsInHand || candidate.HasActivePhysics)
            return;

        if (!TryRayHitCard(ray, candidate, maxDistance, out float distance))
            return;

        HitScratch.Add(new CardRayHit { Card = candidate, Distance = distance });
    }

    static bool TryRayHitCard(Ray ray, WorldCard card, float maxDistance, out float distance)
    {
        distance = float.MaxValue;
        if (card == null)
            return false;

        Vector3 halfExtents = GetHalfExtents(card);
        bool onShelf = card.GetComponentInParent<CardShelfSlot>() != null;
        Vector3 center = onShelf ? card.transform.position : card.GetGroundQueryCenter();

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

        float halfThickness = Mathf.Max(
            CardDimensions.Thickness * scale * 0.5f + CardGroundStack.StackStep,
            0.012f);
        return new Vector3(
            CardDimensions.Width * scale * 0.5f,
            halfThickness,
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
