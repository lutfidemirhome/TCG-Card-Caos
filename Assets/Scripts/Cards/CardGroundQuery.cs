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

    struct PackRayHit
    {
        public WorldBoosterPack Pack;
        public float Distance;
    }

    static readonly List<WorldCard> ShelfCards = new List<WorldCard>(64);
    static readonly HashSet<WorldCard> ShelfCardSet = new HashSet<WorldCard>();
    static readonly List<WorldCard> GroundCandidateScratch = new List<WorldCard>(128);
    static readonly List<CardRayHit> HitScratch = new List<CardRayHit>(32);
    static readonly List<PackRayHit> PackHitScratch = new List<PackRayHit>(16);

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

        CardLayers.EnsureInitialized();
        int worldCardLayer = CardLayers.WorldCard;
        if (worldCardLayer >= 0
            && Physics.Raycast(
                ray,
                out RaycastHit physicsHit,
                maxDistance,
                1 << worldCardLayer,
                QueryTriggerInteraction.Ignore))
        {
            WorldCard physicsCard = physicsHit.collider.GetComponentInParent<WorldCard>();
            if (physicsCard != null && !physicsCard.IsInHand && physicsCard.HasActivePhysics)
            {
                HitScratch.Add(new CardRayHit
                {
                    Card = physicsCard,
                    Distance = physicsHit.distance,
                });
            }
        }

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

    public static bool TryRaycastWorldPack(Ray ray, float maxDistance, out WorldBoosterPack hitPack, out float hitDistance)
    {
        PackHitScratch.Clear();

        CardLayers.EnsureInitialized();
        int worldCardLayer = CardLayers.WorldCard;
        if (worldCardLayer >= 0
            && Physics.Raycast(
                ray,
                out RaycastHit physicsHit,
                maxDistance,
                1 << worldCardLayer,
                QueryTriggerInteraction.Ignore))
        {
            WorldBoosterPack physicsPack = physicsHit.collider.GetComponentInParent<WorldBoosterPack>();
            if (physicsPack != null && !physicsPack.IsInHand)
            {
                PackHitScratch.Add(new PackRayHit
                {
                    Pack = physicsPack,
                    Distance = physicsHit.distance,
                });
            }
        }

        if (CardGroundStack.PhysicsPackCount > 0)
            CardGroundStack.ForEachPhysicsPack(pack => TryAddPhysicsPackHit(ray, maxDistance, pack));
        if (CardGroundStack.TrackedPackCount > 0)
            CardGroundStack.ForEachTrackedPack(pack => TryAddFlatPackHit(ray, maxDistance, pack));

        if (PackHitScratch.Count == 0)
        {
            hitPack = null;
            hitDistance = float.MaxValue;
            return false;
        }

        int nearestIndex = 0;
        for (int i = 1; i < PackHitScratch.Count; i++)
        {
            if (PackHitScratch[i].Distance < PackHitScratch[nearestIndex].Distance)
                nearestIndex = i;
        }

        hitPack = PackHitScratch[nearestIndex].Pack;
        hitDistance = PackHitScratch[nearestIndex].Distance;
        return hitPack != null;
    }

    static void TryAddPhysicsPackHit(Ray ray, float maxDistance, WorldBoosterPack pack)
    {
        if (pack == null || pack.IsInHand || !pack.HasActivePhysics)
            return;

        float scale = Mathf.Max(pack.transform.lossyScale.x, CardDimensions.GroundCardScale);
        Vector3 halfExtents = new Vector3(
            CardDimensions.Width * scale * 0.58f,
            CardDimensions.Height * scale * 0.42f,
            CardDimensions.Height * scale * 0.58f);

        Vector3 center = pack.transform.position;
        if (pack.TryGetComponent(out BoxCollider boxCollider) && boxCollider.enabled)
            center = boxCollider.transform.TransformPoint(boxCollider.center);

        if (!TryRayIntersectOrientedBox(ray, center, pack.transform.rotation, halfExtents, out float distance))
            return;

        if (distance < 0f || distance > maxDistance)
            return;

        AddPackHit(pack, distance);
    }

    static void TryAddFlatPackHit(Ray ray, float maxDistance, WorldBoosterPack pack)
    {
        if (pack == null || pack.IsInHand || pack.HasActivePhysics)
            return;

        float scale = Mathf.Max(pack.transform.lossyScale.x, CardDimensions.GroundCardScale);
        float halfThickness = Mathf.Max(
            CardDimensions.Thickness * scale * 0.5f + CardGroundStack.StackStep,
            0.012f);
        Vector3 halfExtents = new Vector3(
            CardDimensions.Width * scale * 0.5f,
            halfThickness,
            CardDimensions.Height * scale * 0.5f);
        Vector3 center = pack.transform.position;
        center.y = CardGroundStack.GetDrawWorldY(pack);

        if (!TryRayIntersectOrientedBox(ray, center, pack.transform.rotation, halfExtents, out float distance))
            return;

        if (distance < 0f || distance > maxDistance)
            return;

        AddPackHit(pack, distance);
    }

    static void AddPackHit(WorldBoosterPack pack, float distance)
    {
        for (int i = 0; i < PackHitScratch.Count; i++)
        {
            if (PackHitScratch[i].Pack != pack)
                continue;

            if (distance < PackHitScratch[i].Distance)
                PackHitScratch[i] = new PackRayHit { Pack = pack, Distance = distance };

            return;
        }

        PackHitScratch.Add(new PackRayHit { Pack = pack, Distance = distance });
    }

    static void TryAddHit(Ray ray, float maxDistance, WorldCard candidate)
    {
        if (candidate == null || candidate.IsInHand || candidate.HasActivePhysics)
            return;

        if (!TryRayHitCard(ray, candidate, maxDistance, out float distance))
            return;

        HitScratch.Add(new CardRayHit { Card = candidate, Distance = distance });
    }

    static bool IsOnDisplaySlot(WorldCard card) =>
        card.GetComponentInParent<CardShelfSlot>() != null
        || card.GetComponentInParent<PsaCabinetSlot>() != null;

    static bool TryRayHitCard(Ray ray, WorldCard card, float maxDistance, out float distance)
    {
        distance = float.MaxValue;
        if (card == null)
            return false;

        Vector3 halfExtents = GetHalfExtents(card);
        bool onDisplaySlot = IsOnDisplaySlot(card);
        Vector3 center = onDisplaySlot ? card.transform.position : card.GetGroundQueryCenter();

        if (!TryRayIntersectOrientedBox(ray, center, card.transform.rotation, halfExtents, out distance))
            return false;

        return distance >= 0f && distance <= maxDistance;
    }

    static Vector3 GetHalfExtents(WorldCard card)
    {
        if (card.GetComponentInParent<PsaCabinetSlot>() != null && card.UsesPsaSlab)
        {
            Vector3 lossyScale = card.transform.lossyScale;
            if (PsaSlabLayoutUtility.TryGetCabinetRootBounds(out _, out _, out Vector3 size))
            {
                return Vector3.Scale(size * 0.5f, lossyScale);
            }
        }

        float scale = Mathf.Max(card.transform.lossyScale.x, CardDimensions.GroundCardScale);
        if (card.GetComponentInParent<CardShelfSlot>() != null)
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
