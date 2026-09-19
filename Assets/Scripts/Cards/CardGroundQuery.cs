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

    sealed class ShelfCardEntry
    {
        public WorldCard Card;
        public long Order;
        public ShelfGroup Group;
    }

    sealed class ShelfGroup
    {
        public Transform Root;
        public readonly List<ShelfCardEntry> Cards = new List<ShelfCardEntry>(64);
        public Bounds Bounds;
        public Matrix4x4 RootMatrix;
        public bool BoundsDirty = true;
        public bool HasBounds;
    }

    sealed class ShelfCardOrderComparer : IComparer<ShelfCardEntry>
    {
        public int Compare(ShelfCardEntry left, ShelfCardEntry right)
        {
            return left.Order.CompareTo(right.Order);
        }
    }

    static readonly Dictionary<WorldCard, ShelfCardEntry> ShelfCards = new Dictionary<WorldCard, ShelfCardEntry>();
    static readonly Dictionary<Transform, ShelfGroup> ShelfGroupsByRoot = new Dictionary<Transform, ShelfGroup>();
    static readonly List<ShelfGroup> ShelfGroups = new List<ShelfGroup>(128);
    static readonly List<ShelfCardEntry> UngroupedShelfCards = new List<ShelfCardEntry>(16);
    static readonly List<ShelfCardEntry> ShelfCandidateScratch = new List<ShelfCardEntry>(128);
    static readonly ShelfCardOrderComparer ShelfOrderComparer = new ShelfCardOrderComparer();
    static readonly List<WorldCard> GroundCandidateScratch = new List<WorldCard>(128);
    static readonly List<CardRayHit> HitScratch = new List<CardRayHit>(32);
    static readonly List<PackRayHit> PackHitScratch = new List<PackRayHit>(16);
    static long _nextShelfOrder;

    public static void TrackShelfCard(WorldCard card)
    {
        if (card == null)
            return;

        Transform root = GetShelfGroupRoot(card);
        if (ShelfCards.TryGetValue(card, out ShelfCardEntry existing))
        {
            Transform previousRoot = existing.Group != null ? existing.Group.Root : null;
            if (ReferenceEquals(previousRoot, root))
            {
                // Placement/load code can reapply a pose without changing the parent.
                if (existing.Group != null)
                    existing.Group.BoundsDirty = true;
                return;
            }

            RemoveFromShelfGroup(existing);
            AddToShelfGroup(existing, root);
            return;
        }

        var entry = new ShelfCardEntry { Card = card, Order = _nextShelfOrder++ };
        ShelfCards.Add(card, entry);
        AddToShelfGroup(entry, root);
    }

    public static void UntrackShelfCard(WorldCard card)
    {
        if (ReferenceEquals(card, null) || !ShelfCards.TryGetValue(card, out ShelfCardEntry entry))
            return;

        ShelfCards.Remove(card);
        RemoveFromShelfGroup(entry);
    }

    public static void ClearShelfCards()
    {
        ShelfCards.Clear();
        ShelfGroupsByRoot.Clear();
        ShelfGroups.Clear();
        UngroupedShelfCards.Clear();
        ShelfCandidateScratch.Clear();
        _nextShelfOrder = 0;
    }

    static Transform GetShelfGroupRoot(WorldCard card)
    {
        // Unseated or moving cards use the live fallback until their final placement.
        if (card.IsInHand || card.IsFlyingToShelf || card.HasActivePhysics)
            return null;

        CardShelf shelf = card.GetComponentInParent<CardShelf>();
        if (shelf != null)
            return shelf.transform;

        PsaCabinet cabinet = card.GetComponentInParent<PsaCabinet>();
        if (cabinet != null)
            return cabinet.transform;

        PsaCabinetSlot holder = card.GetComponentInParent<PsaCabinetSlot>();
        return holder != null ? holder.transform : null;
    }

    static void AddToShelfGroup(ShelfCardEntry entry, Transform root)
    {
        if (root == null)
        {
            UngroupedShelfCards.Add(entry);
            return;
        }

        if (!ShelfGroupsByRoot.TryGetValue(root, out ShelfGroup group))
        {
            group = new ShelfGroup { Root = root };
            ShelfGroupsByRoot.Add(root, group);
            ShelfGroups.Add(group);
        }

        entry.Group = group;
        group.Cards.Add(entry);
        group.BoundsDirty = true;
    }

    static void RemoveFromShelfGroup(ShelfCardEntry entry)
    {
        ShelfGroup group = entry.Group;
        entry.Group = null;
        if (group == null)
        {
            UngroupedShelfCards.Remove(entry);
            return;
        }

        group.Cards.Remove(entry);
        group.BoundsDirty = true;
        if (group.Cards.Count != 0)
            return;

        RemoveEmptyShelfGroup(group);
    }

    static void RemoveEmptyShelfGroup(ShelfGroup group)
    {
        ShelfGroupsByRoot.Remove(group.Root);
        ShelfGroups.Remove(group);
    }

    static void CollectShelfCandidates(Ray ray, float maxDistance)
    {
        ShelfCandidateScratch.Clear();
        for (int i = 0; i < ShelfGroups.Count; i++)
        {
            ShelfGroup group = ShelfGroups[i];
            if (group.Root != null)
            {
                if (!group.Root.gameObject.activeInHierarchy)
                    continue;

                // One transform read per cabinet; card transforms are only read for
                // nearby ray candidates, a changed cabinet, or a new placement.
                Matrix4x4 rootMatrix = group.Root.localToWorldMatrix;
                if (group.BoundsDirty || !rootMatrix.Equals(group.RootMatrix))
                    RebuildShelfBounds(group, rootMatrix);

                if (group.Cards.Count == 0)
                {
                    RemoveEmptyShelfGroup(group);
                    i--;
                    continue;
                }

                if (!group.HasBounds
                    || (!group.Bounds.Contains(ray.origin)
                        && (!group.Bounds.IntersectRay(ray, out float distance) || distance > maxDistance)))
                    continue;
            }

            // A removed cabinet root must not hide surviving, reparented cards.
            CollectLiveShelfEntries(group.Cards);
            if (group.Cards.Count == 0)
            {
                RemoveEmptyShelfGroup(group);
                i--;
            }
        }

        CollectLiveShelfEntries(UngroupedShelfCards);
        // Cabinet grouping must not change equal-distance targeting order.
        ShelfCandidateScratch.Sort(ShelfOrderComparer);
    }

    static void RebuildShelfBounds(ShelfGroup group, Matrix4x4 rootMatrix)
    {
        group.HasBounds = false;
        for (int i = 0; i < group.Cards.Count; i++)
        {
            WorldCard card = group.Cards[i].Card;
            if (card == null)
            {
                RemoveDeadShelfEntry(group.Cards, i--);
                continue;
            }

            // A sphere encloses every orientation of the exact card/slab query box.
            // Padding also covers the small center shift of shelf completion pulses;
            // those cards are locked until their original local pose is restored.
            float radius = GetHalfExtents(card, card.transform.lossyScale).magnitude + 0.05f;
            var cardBounds = new Bounds(card.GetGroundQueryCenter(), Vector3.one * (radius * 2f));
            if (group.HasBounds)
                group.Bounds.Encapsulate(cardBounds);
            else
            {
                group.Bounds = cardBounds;
                group.HasBounds = true;
            }
        }

        group.RootMatrix = rootMatrix;
        group.BoundsDirty = false;
    }

    static void CollectLiveShelfEntries(List<ShelfCardEntry> entries)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            ShelfCardEntry entry = entries[i];
            if (entry.Card == null)
            {
                // OnDestroy normally removes these immediately. Keep a fallback for
                // destroyed references without retaining them in candidate lists.
                RemoveDeadShelfEntry(entries, i);
                continue;
            }

            if (entry.Card.gameObject.activeInHierarchy)
                ShelfCandidateScratch.Add(entry);
        }
    }

    static void RemoveDeadShelfEntry(List<ShelfCardEntry> entries, int index)
    {
        ShelfCardEntry entry = entries[index];
        if (!ReferenceEquals(entry.Card, null))
            ShelfCards.Remove(entry.Card);
        if (entry.Group != null)
            entry.Group.BoundsDirty = true;
        entry.Group = null;
        entries.RemoveAt(index);
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

        CollectShelfCandidates(ray, maxDistance);
        for (int i = 0; i < ShelfCandidateScratch.Count; i++)
            TryAddHit(ray, maxDistance, ShelfCandidateScratch[i].Card);

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

        for (int i = 0; i < CardGroundStack.PhysicsPackCount; i++)
            TryAddPhysicsPackHit(ray, maxDistance, CardGroundStack.PhysicsPackAt(i));
        for (int i = 0; i < CardGroundStack.TrackedPackCount; i++)
            TryAddFlatPackHit(ray, maxDistance, CardGroundStack.TrackedPackAt(i));

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

        // Use the authored world pose. GetDrawWorldY snaps to the lower-floor stack plane,
        // so Mix packs on the mezzanine were hittable through the demo floor and flew into hand.
        Vector3 center = pack.transform.position;
        if (pack.TryGetComponent(out BoxCollider boxCollider) && boxCollider.enabled)
            center = boxCollider.transform.TransformPoint(boxCollider.center);

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
        if (candidate == null || candidate.IsInHand || candidate.HasActivePhysics || candidate.IsShelfRowCompleteLocked)
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

        Transform cardTransform = card.transform;
        Vector3 lossyScale = cardTransform.lossyScale;
        // Ground-query and display-slot centers both use the current root position.
        // Read the live pose so shelf feedback and parent movement need no cache invalidation.
        Vector3 center = card.GetGroundQueryCenter();
        if (!MayRayHitCardBounds(ray, maxDistance, card, center, lossyScale))
            return false;

        Vector3 halfExtents = GetHalfExtents(card, lossyScale);

        if (!TryRayIntersectOrientedBox(ray, center, cardTransform.rotation, halfExtents, out distance))
            return false;

        return distance >= 0f && distance <= maxDistance;
    }

    static bool MayRayHitCardBounds(
        Ray ray,
        float maxDistance,
        WorldCard card,
        Vector3 center,
        Vector3 lossyScale)
    {
        // Ground and upright shelf boxes swap their height/thickness axes. The ground
        // thickness padding makes its bounding sphere conservative for both poses.
        // This avoids hierarchy walks and matrix inversion for distant/off-ray cards.
        float scale = Mathf.Max(lossyScale.x, CardDimensions.GroundCardScale);
        float halfWidth = CardDimensions.Width * scale * 0.5f;
        float halfHeight = CardDimensions.Height * scale * 0.5f;
        float halfThickness = Mathf.Max(
            CardDimensions.Thickness * scale * 0.5f + CardGroundStack.StackStep,
            0.012f);
        float radiusSquared = halfWidth * halfWidth
            + halfHeight * halfHeight
            + halfThickness * halfThickness;

        if (card.UsesPsaSlab
            && PsaSlabLayoutUtility.TryGetCabinetRootBounds(out _, out _, out Vector3 size))
        {
            Vector3 slabHalfExtents = Vector3.Scale(size * 0.5f, lossyScale);
            radiusSquared = Mathf.Max(radiusSquared, slabHalfExtents.sqrMagnitude);
        }

        Vector3 toCenter = center - ray.origin;
        float directionSquared = ray.direction.sqrMagnitude;
        float alongRay = directionSquared > 0f
            ? Mathf.Clamp(Vector3.Dot(toCenter, ray.direction) / directionSquared, 0f, maxDistance)
            : 0f;
        Vector3 closestOffset = toCenter - ray.direction * alongRay;

        // Conservative tolerance keeps edge hits in the exact box test despite roundoff.
        return !(closestOffset.sqrMagnitude > radiusSquared * 1.0001f + 0.0001f);
    }

    static Vector3 GetHalfExtents(WorldCard card, Vector3 lossyScale)
    {
        if (card.GetComponentInParent<PsaCabinetSlot>() != null && card.UsesPsaSlab)
        {
            if (PsaSlabLayoutUtility.TryGetCabinetRootBounds(out _, out _, out Vector3 size))
            {
                return Vector3.Scale(size * 0.5f, lossyScale);
            }
        }

        float scale = Mathf.Max(lossyScale.x, CardDimensions.GroundCardScale);
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
