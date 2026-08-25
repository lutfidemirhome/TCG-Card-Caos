using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps overlapping flat world cards at distinct heights (GPU instancing stays enabled).
/// Every ground card also gets a unique micro depth bias so same-layer cards never z-fight.
/// </summary>
public static class CardGroundStack
{
    /// <summary>
    /// Air between two stacked cards. It has to clear <see cref="UniqueDepthBiasRange"/>: a layer-0 card
    /// is lifted by up to that bias, so with a thinner gap the card on layer 1 above it ends up inside it.
    /// </summary>
    const float StackGap = 0.0006f;

    const float UniqueDepthBiasRange = 0.0004f;
    const int UniqueDepthBiasSteps = 4096;
    const int BulkFlatStackThreshold = 256;

    /// <summary>Thickness of the footprint slab used for ground overlap tests.</summary>
    const float FootprintSlabHeight = 0.01f;

    static readonly List<WorldCard> GroundCards = new List<WorldCard>(512);
    static readonly HashSet<WorldCard> GroundCardSet = new HashSet<WorldCard>();
    static readonly List<WorldCard> ClusterScratch = new List<WorldCard>(16);
    static readonly List<WorldCard> OpenScratch = new List<WorldCard>(16);
    static readonly List<WorldCard> CellScratch = new List<WorldCard>(32);
    static readonly List<WorldCard> CellSeedScratch = new List<WorldCard>(16);
    static readonly HashSet<WorldCard> RelayoutSeen = new HashSet<WorldCard>();
    static readonly List<WorldCard> LandingColliderScratch = new List<WorldCard>(32);
    static readonly Dictionary<long, List<WorldCard>> SpatialBuckets = new Dictionary<long, List<WorldCard>>(512);
    static readonly Dictionary<WorldCard, int> LandingColliderRefCounts = new Dictionary<WorldCard, int>(64);
    static readonly Dictionary<int, HashSet<WorldCard>> LandingColliderScopes = new Dictionary<int, HashSet<WorldCard>>(8);
    static readonly HashSet<WorldCard> LandingCandidateSet = new HashSet<WorldCard>();
    static int _nextLandingScopeId = 1;

    public static float StackStep =>
        CardDimensions.Thickness * CardDimensions.GroundCardScale + StackGap;

    public static float GetStackedWorldY(int layer)
    {
        return CardFactory.GroundHeightOffset() + Mathf.Max(0, layer) * StackStep;
    }

    /// <summary>
    /// Stable per-card depth offset so thousands of layer-0 cards do not share the exact same Y.
    /// Must be used by GPU instanced draw matrices (transform.y alone is overwritten there).
    /// </summary>
    public static float GetUniqueDepthBias(WorldCard card)
    {
        if (card == null)
            return 0f;

        int id = Mathf.Abs(card.GetInstanceID());
        return (id % UniqueDepthBiasSteps) * (UniqueDepthBiasRange / UniqueDepthBiasSteps);
    }

    public static float GetDrawWorldY(WorldCard card)
    {
        if (card == null)
            return CardFactory.GroundHeightOffset();

        float y = GetStackedWorldY(card.GroundStackLayer) + card.GroundRestLift;
        if (card.GroundStackLayer == 0)
            y += GetUniqueDepthBias(card);

        return y;
    }

    public static void Track(WorldCard card)
    {
        if (card == null || !GroundCardSet.Add(card))
            return;

        GroundCards.Add(card);
    }

    public static void Untrack(WorldCard card)
    {
        if (card == null || !GroundCardSet.Remove(card))
            return;

        Vector3 removedPos = card.transform.position;
        RemoveFromList(card);

        if (GroundCards.Count >= BulkFlatStackThreshold)
            RefreshCellAt(removedPos);
    }

    static void RemoveFromList(WorldCard card)
    {
        for (int i = GroundCards.Count - 1; i >= 0; i--)
        {
            if (GroundCards[i] != card)
                continue;

            int last = GroundCards.Count - 1;
            if (i != last)
                GroundCards[i] = GroundCards[last];

            GroundCards.RemoveAt(last);
            return;
        }
    }

    public static void ClearAll()
    {
        GroundCards.Clear();
        GroundCardSet.Clear();
        SpatialBuckets.Clear();
    }

    static readonly HashSet<WorldCard> RayCandidateSeen = new HashSet<WorldCard>();

    /// <summary>Collect ground cards near a view ray (spatial cells when many cards are tracked).</summary>
    public static void CollectRayCandidates(Ray ray, float maxDistance, List<WorldCard> results)
    {
        results.Clear();
        if (GroundCards.Count == 0)
            return;

        if (GroundCards.Count < BulkFlatStackThreshold)
        {
            for (int i = 0; i < GroundCards.Count; i++)
            {
                WorldCard card = GroundCards[i];
                if (card == null || card.IsInHand || card.GetComponentInParent<CardShelfSlot>() != null)
                    continue;

                results.Add(card);
            }

            return;
        }

        if (SpatialBuckets.Count == 0)
            RebuildSpatialBuckets();

        float cellSize = SpatialCellSize;
        float step = Mathf.Max(cellSize * 0.75f, 0.08f);
        int steps = Mathf.CeilToInt(maxDistance / step);
        RayCandidateSeen.Clear();

        for (int s = 0; s <= steps; s++)
        {
            float dist = Mathf.Min(maxDistance, s * step);
            Vector3 point = ray.origin + ray.direction * dist;
            int cx = Mathf.FloorToInt(point.x / cellSize);
            int cz = Mathf.FloorToInt(point.z / cellSize);

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    long key = PackCell(cx + dx, cz + dz);
                    if (!SpatialBuckets.TryGetValue(key, out List<WorldCard> bucket))
                        continue;

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        WorldCard card = bucket[i];
                        if (card == null || card.IsInHand || card.GetComponentInParent<CardShelfSlot>() != null)
                            continue;
                        if (!RayCandidateSeen.Add(card))
                            continue;

                        results.Add(card);
                    }
                }
            }
        }
    }

    public static void RebuildAll()
    {
        RebuildCellPiles();
    }

    public static IEnumerator RebuildAllAsync()
    {
        yield return null;
        RebuildSpatialBuckets();

        int processed = 0;
        foreach (KeyValuePair<long, List<WorldCard>> pair in SpatialBuckets)
        {
            ApplyPileLayers(pair.Value);
            processed++;
            if (processed % 24 == 0)
                yield return null;
        }
    }

    /// <summary>
    /// One pile per XZ cell — never flood-fills across the floor (that launched cards into the sky).
    /// </summary>
    static void RebuildCellPiles()
    {
        RebuildSpatialBuckets();
        foreach (KeyValuePair<long, List<WorldCard>> pair in SpatialBuckets)
            ApplyPileLayers(pair.Value);
    }

    static void RebuildSpatialBuckets()
    {
        SpatialBuckets.Clear();
        float cellSize = SpatialCellSize;

        for (int i = 0; i < GroundCards.Count; i++)
        {
            WorldCard card = GroundCards[i];
            if (card == null || card.IsInHand)
                continue;
            if (card.GetComponentInParent<CardShelfSlot>() != null
                || card.GetComponentInParent<PsaCabinetSlot>() != null)
                continue;

            long key = CellKey(card.transform.position, cellSize);
            if (!SpatialBuckets.TryGetValue(key, out List<WorldCard> bucket))
            {
                bucket = new List<WorldCard>(8);
                SpatialBuckets[key] = bucket;
            }

            bucket.Add(card);
        }
    }

    /// <summary>
    /// A card left this cell, so whatever was lying on it has to drop a layer. Relayout runs per
    /// overlapping pile, not per cell: a cell is smaller than a card, so cards that never sat on each
    /// other share one — handing the whole cell to the pile layout stacked them into a floating
    /// staircase, and this runs on every pickup once the floor holds a few hundred cards.
    /// </summary>
    static void RefreshCellAt(Vector3 worldPos)
    {
        float cellSize = SpatialCellSize;
        long key = CellKey(worldPos, cellSize);
        CellSeedScratch.Clear();

        for (int i = 0; i < GroundCards.Count; i++)
        {
            WorldCard card = GroundCards[i];
            if (card == null || card.IsInHand || card.IsPhysicsSimulating)
                continue;
            if (CellKey(card.transform.position, cellSize) != key)
                continue;

            CellSeedScratch.Add(card);
        }

        RelayoutSeen.Clear();
        for (int i = 0; i < CellSeedScratch.Count; i++)
        {
            WorldCard seed = CellSeedScratch[i];
            if (seed == null || RelayoutSeen.Contains(seed))
                continue;

            BuildLocalPile(seed, ClusterScratch);
            if (ClusterScratch.Count == 0)
                continue;

            for (int j = 0; j < ClusterScratch.Count; j++)
                RelayoutSeen.Add(ClusterScratch[j]);

            ApplyPileLayers(ClusterScratch);
        }

        RelayoutSeen.Clear();
    }

    static void ApplyPileLayers(List<WorldCard> pile, WorldCard forceOnTop = null)
    {
        if (pile == null || pile.Count == 0)
            return;

        pile.Sort(CompareGroundOrder);
        if (forceOnTop != null)
        {
            for (int i = pile.Count - 1; i >= 0; i--)
            {
                if (pile[i] != forceOnTop)
                    continue;
                pile.RemoveAt(i);
                break;
            }

            pile.Add(forceOnTop);
        }

        for (int layer = 0; layer < pile.Count; layer++)
        {
            WorldCard card = pile[layer];
            // A thrown card is placed by the settle pass, against the colliders actually under it — a
            // flat layer height cannot describe a card resting on a pack or on a tilted neighbour, so
            // it keeps its own height and only reserves this layer for the cards around it.
            if (card == null || card.IsInHand || card.HasActivePhysics)
                continue;
            // Shelf / PSA cards must keep their display pose — never flatten them to ground Y.
            if (card.GetComponentInParent<CardShelfSlot>() != null
                || card.GetComponentInParent<PsaCabinetSlot>() != null)
                continue;

            card.SetGroundStackLayer(layer);
            Vector3 position = card.transform.position;
            position.y = GetDrawWorldY(card);
            card.SetGroundRestPosition(position);
        }
    }

    /// <summary>Turn nearby flat ground cards into solid surfaces while an item is in flight.</summary>
    public static int BeginLandingColliderScope()
    {
        int scopeId = _nextLandingScopeId++;
        LandingColliderScopes[scopeId] = new HashSet<WorldCard>();
        return scopeId;
    }

    public static void RefreshLandingColliderScope(int scopeId, Vector3 worldPos, float radius)
    {
        if (!LandingColliderScopes.TryGetValue(scopeId, out HashSet<WorldCard> scope))
            return;

        LandingColliderScratch.Clear();
        CollectFlatLandingCandidates(worldPos, radius, LandingColliderScratch);
        LandingCandidateSet.Clear();
        for (int i = 0; i < LandingColliderScratch.Count; i++)
            LandingCandidateSet.Add(LandingColliderScratch[i]);

        foreach (WorldCard card in scope)
        {
            if (card == null || LandingCandidateSet.Contains(card))
                continue;

            ReleaseLandingColliderRef(card);
        }

        scope.RemoveWhere(card => card == null || !LandingCandidateSet.Contains(card));

        for (int i = 0; i < LandingColliderScratch.Count; i++)
        {
            WorldCard card = LandingColliderScratch[i];
            if (card == null || !scope.Add(card))
                continue;

            AcquireLandingColliderRef(card);
        }
    }

    public static void EndLandingColliderScope(int scopeId)
    {
        if (!LandingColliderScopes.TryGetValue(scopeId, out HashSet<WorldCard> scope))
            return;

        foreach (WorldCard card in scope)
        {
            if (card != null)
                ReleaseLandingColliderRef(card);
        }

        LandingColliderScopes.Remove(scopeId);
    }

    static void CollectFlatLandingCandidates(Vector3 worldPos, float radius, List<WorldCard> results)
    {
        if (GroundCards.Count >= BulkFlatStackThreshold)
        {
            CollectFlatLandingCandidatesSpatial(worldPos, radius, results);
            return;
        }

        float radiusSq = radius * radius;
        for (int i = 0; i < GroundCards.Count; i++)
        {
            WorldCard card = GroundCards[i];
            if (card == null || card.IsInHand || card.IsPhysicsSimulating)
                continue;

            Vector3 delta = card.transform.position - worldPos;
            delta.y *= 0.35f;
            if (delta.sqrMagnitude > radiusSq)
                continue;

            results.Add(card);
        }
    }

    static void CollectFlatLandingCandidatesSpatial(Vector3 worldPos, float radius, List<WorldCard> results)
    {
        if (SpatialBuckets.Count == 0)
            RebuildSpatialBuckets();

        float cellSize = SpatialCellSize;
        float radiusSq = radius * radius;
        int cx = Mathf.FloorToInt(worldPos.x / cellSize);
        int cz = Mathf.FloorToInt(worldPos.z / cellSize);
        int cellRadius = Mathf.CeilToInt(radius / cellSize);

        for (int dz = -cellRadius; dz <= cellRadius; dz++)
        {
            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                long key = PackCell(cx + dx, cz + dz);
                if (!SpatialBuckets.TryGetValue(key, out List<WorldCard> bucket))
                    continue;

                for (int i = 0; i < bucket.Count; i++)
                {
                    WorldCard card = bucket[i];
                    if (card == null || card.IsInHand || card.IsPhysicsSimulating)
                        continue;

                    Vector3 delta = card.transform.position - worldPos;
                    delta.y *= 0.35f;
                    if (delta.sqrMagnitude > radiusSq)
                        continue;

                    results.Add(card);
                }
            }
        }
    }

    static void AcquireLandingColliderRef(WorldCard card)
    {
        if (card == null)
            return;

        if (!LandingColliderRefCounts.TryGetValue(card, out int count) || count <= 0)
        {
            card.EnableLandingCollider();
            LandingColliderRefCounts[card] = 1;
            return;
        }

        LandingColliderRefCounts[card] = count + 1;
    }

    static void ReleaseLandingColliderRef(WorldCard card)
    {
        if (card == null || !LandingColliderRefCounts.TryGetValue(card, out int count))
            return;

        count--;
        if (count <= 0)
        {
            LandingColliderRefCounts.Remove(card);
            card.RestoreGroundCollider();
            return;
        }

        LandingColliderRefCounts[card] = count;
    }

    /// <summary>Cell size for neighbor queries — sized to card footprint, not pile radius.</summary>
    static float SpatialCellSize
    {
        get
        {
            float footprint = Mathf.Max(CardDimensions.Width, CardDimensions.Height)
                * CardDimensions.GroundCardScale;
            return Mathf.Max(0.12f, footprint * 0.85f);
        }
    }

    static long CellKey(Vector3 worldPos, float cellSize)
    {
        int x = Mathf.FloorToInt(worldPos.x / cellSize);
        int z = Mathf.FloorToInt(worldPos.z / cellSize);
        return ((long)x << 32) ^ (uint)z;
    }

    /// <summary>
    /// Place a dropped card on the pile. Existing cards keep their height — only this card moves.
    /// </summary>
    /// <param name="maxDownwardShift">
    /// Largest downward correction the stack height may apply, in world units. Negative = unlimited.
    /// Settling throws pass a small budget so a card resting on a cabinet plinth or leaning against
    /// one is never yanked down to the floor-relative stack height and left inside the geometry.
    /// </param>
    public static void ApplyStackHeight(WorldCard card, bool placeOnTop = false, float maxDownwardShift = -1f)
    {
        if (card == null || card.IsInHand)
            return;

        Track(card);
        if (placeOnTop)
        {
            PlaceOnTopOfOverlaps(card, maxDownwardShift);
            return;
        }

        RefreshCluster(card);
    }

    static void PlaceOnTopOfOverlaps(WorldCard card, float maxDownwardShift)
    {
        ScanRestingOverlaps(
            card.transform,
            GetStackReferenceY(card),
            card,
            null,
            out int maxLayer,
            out float ceilingY);

        card.SetGroundStackLayer(maxLayer + 1);

        Vector3 position = card.transform.position;
        float stackedY = GetDrawWorldY(card);
        if (ShouldTakeStackedY(position.y, stackedY, maxDownwardShift, ceilingY + card.GroundRestLift))
        {
            position.y = stackedY;
            card.SetGroundRestPosition(position);
        }

        CardFactory.LiftAboveFloor(card.transform, card.GetComponent<Rigidbody>());
        InsertIntoSpatialBucket(card);
    }

    /// <summary>
    /// Looks at every settled card and pack whose footprint the item covers and reports the highest
    /// layer among those it is resting on, plus the height of the lowest one resting on top of it.
    /// </summary>
    /// <param name="restY">Item's own height, in the shared reference space of <see cref="GetStackReferenceY(WorldCard)"/>.</param>
    /// <param name="maxLayer">-1 when the item covers nothing, so the caller lands on layer 0.</param>
    /// <param name="ceilingY">
    /// Reference height of the lowest item lying on this one, or infinity when nothing does.
    /// </param>
    static void ScanRestingOverlaps(
        Transform itemTransform,
        float restY,
        WorldCard selfCard,
        WorldBoosterPack selfPack,
        out int maxLayer,
        out float ceilingY)
    {
        maxLayer = -1;
        ceilingY = float.PositiveInfinity;

        Bounds footprint = GetHorizontalBounds(itemTransform);
        EnsureSpatialBucketsBuilt();
        CollectNeighborhood(itemTransform.position, CellScratch);

        for (int i = 0; i < CellScratch.Count; i++)
        {
            WorldCard other = CellScratch[i];
            // Frozen cards (settled, kinematic body kept) still occupy a layer and are still a solid
            // surface, so they must count here — skipping them handed the incoming card their layer
            // and dropped it underneath the card it visibly landed on.
            if (other == null || other == selfCard || other.IsInHand || other.IsPhysicsSimulating)
                continue;
            if (!footprint.Intersects(GetHorizontalBounds(other.transform)))
                continue;

            AccumulateStackNeighbor(GetStackReferenceY(other), other.GroundStackLayer, restY, ref maxLayer, ref ceilingY);
        }

        for (int i = 0; i < GroundPacks.Count; i++)
        {
            WorldBoosterPack pack = GroundPacks[i];
            if (pack == null || pack == selfPack || pack.IsInHand || pack.IsPhysicsSimulating)
                continue;
            if (!footprint.Intersects(GetHorizontalBounds(pack.transform)))
                continue;

            AccumulateStackNeighbor(GetStackReferenceY(pack), pack.GroundStackLayer, restY, ref maxLayer, ref ceilingY);
        }
    }

    static void AccumulateStackNeighbor(
        float otherY,
        int otherLayer,
        float restY,
        ref int maxLayer,
        ref float ceilingY)
    {
        if (RestsAbove(otherY, restY))
        {
            ceilingY = Mathf.Min(ceilingY, otherY);
            return;
        }

        if (otherLayer > maxLayer)
            maxLayer = otherLayer;
    }

    /// <summary>
    /// Height an item is measured at when comparing stack positions. A pack's root sits a body
    /// half-thickness higher than a card's on the same surface, so its lift is taken back out.
    /// </summary>
    static float GetStackReferenceY(WorldCard card) => card.transform.position.y - card.GroundRestLift;

    static float GetStackReferenceY(WorldBoosterPack pack) => pack.transform.position.y - pack.GroundRestLift;

    /// <summary>Half a layer of tolerance so items sharing one surface never read as stacked.</summary>
    static bool RestsAbove(float otherY, float selfY) => otherY > selfY + StackStep * 0.5f;

    /// <param name="ceilingY">
    /// Root height of the lowest settled item resting on top of this one, in this item's own root
    /// space (infinity when nothing rests on it).
    /// </param>
    static bool ShouldTakeStackedY(float currentY, float stackedY, float maxDownwardShift, float ceilingY)
    {
        float minY = CardFactory.GroundHeightOffset();
        // A card that tunneled under the floor (or under the pile) must be allowed to climb back out.
        // The ceiling guard below used to keep it frozen underground because the pile read as a roof.
        if (currentY < minY && stackedY >= minY)
            return true;

        // Whoever settles first claims the lowest free layer, and back-to-back throws at one spot settle
        // out of order: the card underneath is regularly the last to freeze. Climbing to its computed
        // layer then drove it straight up into the card already resting on it, and both froze intersecting.
        if (currentY >= minY && stackedY > currentY && stackedY > ceilingY - StackStep * 0.5f)
            return false;

        return maxDownwardShift < 0f || stackedY >= currentY - maxDownwardShift;
    }

    /// <summary>
    /// Builds the spatial hash once and only re-touches it via <see cref="InsertIntoSpatialBucket"/> for
    /// individually-settled items afterward — a full rebuild scanning every tracked ground card is too
    /// costly to run on every single thrown card/pack settle (rapid Q-throws would otherwise hitch).
    /// </summary>
    static void EnsureSpatialBucketsBuilt()
    {
        if (SpatialBuckets.Count == 0)
            RebuildSpatialBuckets();
    }

    static void InsertIntoSpatialBucket(WorldCard card)
    {
        if (card == null || SpatialBuckets.Count == 0)
            return;

        long key = CellKey(card.transform.position, SpatialCellSize);
        if (!SpatialBuckets.TryGetValue(key, out List<WorldCard> bucket))
        {
            bucket = new List<WorldCard>(8);
            SpatialBuckets[key] = bucket;
        }

        if (!bucket.Contains(card))
            bucket.Add(card);
    }

    public static void RefreshCluster(WorldCard seed, WorldCard forceOnTop = null)
    {
        if (seed == null || seed.IsInHand)
            return;

        BuildLocalPile(seed, ClusterScratch);
        if (ClusterScratch.Count == 0)
            return;

        ApplyPileLayers(ClusterScratch, forceOnTop);
    }

    static void CollectNeighborhood(Vector3 worldPos, List<WorldCard> results)
    {
        results.Clear();
        float cellSize = SpatialCellSize;
        int cx = Mathf.FloorToInt(worldPos.x / cellSize);
        int cz = Mathf.FloorToInt(worldPos.z / cellSize);

        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                long key = PackCell(cx + dx, cz + dz);
                if (!SpatialBuckets.TryGetValue(key, out List<WorldCard> bucket))
                    continue;

                for (int i = 0; i < bucket.Count; i++)
                {
                    WorldCard card = bucket[i];
                    if (card != null && !card.IsInHand)
                        results.Add(card);
                }
            }
        }
    }

    static long PackCell(int x, int z) => ((long)x << 32) ^ (uint)z;

    static int CompareGroundOrder(WorldCard a, WorldCard b)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        int layerDelta = a.GroundStackLayer - b.GroundStackLayer;
        if (layerDelta != 0)
            return layerDelta;

        return WorldCardDrawOrder.CompareStableInstanceId(a, b);
    }

    /// <summary>
    /// Flood-fill overlapping cards in neighboring cells only — not the whole floor.
    /// </summary>
    static void BuildLocalPile(WorldCard seed, List<WorldCard> results)
    {
        results.Clear();
        if (seed == null || seed.IsInHand)
            return;

        RebuildSpatialBuckets();
        CollectNeighborhood(seed.transform.position, CellScratch);
        for (int i = CellScratch.Count - 1; i >= 0; i--)
        {
            WorldCard card = CellScratch[i];
            if (card == null || card.IsInHand || card.IsPhysicsSimulating)
                CellScratch.RemoveAt(i);
        }

        OpenScratch.Clear();
        OpenScratch.Add(seed);

        while (OpenScratch.Count > 0)
        {
            int last = OpenScratch.Count - 1;
            WorldCard current = OpenScratch[last];
            OpenScratch.RemoveAt(last);

            if (current == null || current.IsInHand || results.Contains(current))
                continue;

            results.Add(current);

            for (int i = 0; i < CellScratch.Count; i++)
            {
                WorldCard other = CellScratch[i];
                if (other == null || other.IsInHand || results.Contains(other))
                    continue;
                if (!OverlapsOnGround(current, other))
                    continue;

                OpenScratch.Add(other);
            }
        }

        if (!results.Contains(seed))
            results.Add(seed);
    }

    public static bool OverlapsOnGround(WorldCard a, WorldCard b)
    {
        if (a == null || b == null)
            return false;

        return GetHorizontalBounds(a.transform).Intersects(GetHorizontalBounds(b.transform));
    }

    /// <summary>
    /// Yaw-aware ground footprint of a card or pack — both are the same flat rectangle. The Y size is
    /// a deliberately thin slab so items a couple of layers apart do not read as sharing a surface.
    /// </summary>
    static Bounds GetHorizontalBounds(Transform itemTransform)
    {
        float scale = Mathf.Max(itemTransform.lossyScale.x, CardDimensions.GroundCardScale);
        float width = CardDimensions.Width * scale;
        float height = CardDimensions.Height * scale;
        float yaw = itemTransform.eulerAngles.y * Mathf.Deg2Rad;
        float cos = Mathf.Abs(Mathf.Cos(yaw));
        float sin = Mathf.Abs(Mathf.Sin(yaw));
        float extentX = width * cos + height * sin;
        float extentZ = width * sin + height * cos;
        return new Bounds(itemTransform.position, new Vector3(extentX, FootprintSlabHeight, extentZ));
    }

    static readonly List<WorldBoosterPack> GroundPacks = new List<WorldBoosterPack>(32);
    static readonly HashSet<WorldBoosterPack> GroundPackSet = new HashSet<WorldBoosterPack>();
    static readonly List<WorldBoosterPack> PhysicsPacks = new List<WorldBoosterPack>(16);
    static readonly HashSet<WorldBoosterPack> PhysicsPackSet = new HashSet<WorldBoosterPack>();
    static readonly List<WorldCard> PhysicsCards = new List<WorldCard>(64);
    static readonly HashSet<WorldCard> PhysicsCardSet = new HashSet<WorldCard>();

    public static int TrackedPackCount => GroundPacks.Count;

    public static int PhysicsPackCount => PhysicsPacks.Count;

    public static void TrackPack(WorldBoosterPack pack)
    {
        if (pack == null || !GroundPackSet.Add(pack))
            return;

        GroundPacks.Add(pack);
    }

    public static void UntrackPack(WorldBoosterPack pack)
    {
        if (pack == null || !GroundPackSet.Remove(pack))
            return;

        for (int i = GroundPacks.Count - 1; i >= 0; i--)
        {
            if (GroundPacks[i] == pack)
            {
                GroundPacks.RemoveAt(i);
                break;
            }
        }
    }

    public static void ForEachTrackedPack(System.Action<WorldBoosterPack> action)
    {
        if (action == null)
            return;

        for (int i = 0; i < GroundPacks.Count; i++)
            action(GroundPacks[i]);
    }

    public static void TrackPhysicsPack(WorldBoosterPack pack)
    {
        if (pack == null || !PhysicsPackSet.Add(pack))
            return;

        PhysicsPacks.Add(pack);
    }

    public static void UntrackPhysicsPack(WorldBoosterPack pack)
    {
        if (pack == null || !PhysicsPackSet.Remove(pack))
            return;

        for (int i = PhysicsPacks.Count - 1; i >= 0; i--)
        {
            if (PhysicsPacks[i] == pack)
            {
                PhysicsPacks.RemoveAt(i);
                break;
            }
        }
    }

    public static void TrackPhysicsCard(WorldCard card)
    {
        if (card == null || !PhysicsCardSet.Add(card))
            return;

        PhysicsCards.Add(card);
    }

    public static void UntrackPhysicsCard(WorldCard card)
    {
        if (card == null || !PhysicsCardSet.Remove(card))
            return;

        for (int i = PhysicsCards.Count - 1; i >= 0; i--)
        {
            if (PhysicsCards[i] == card)
            {
                PhysicsCards.RemoveAt(i);
                break;
            }
        }
    }

    public static bool IsTrackedPhysicsCard(WorldCard card) =>
        card != null && PhysicsCardSet.Contains(card);

    public static bool IsTrackedPhysicsPack(WorldBoosterPack pack) =>
        pack != null && PhysicsPackSet.Contains(pack);

    public static bool IsTrackedPhysicsTransform(Transform itemTransform)
    {
        if (itemTransform == null)
            return false;

        WorldCard card = itemTransform.GetComponent<WorldCard>();
        if (card != null)
            return PhysicsCardSet.Contains(card);

        WorldBoosterPack pack = itemTransform.GetComponent<WorldBoosterPack>();
        return pack != null && PhysicsPackSet.Contains(pack);
    }

    public static void ForEachPhysicsCard(System.Action<WorldCard> action)
    {
        if (action == null)
            return;

        for (int i = 0; i < PhysicsCards.Count; i++)
            action(PhysicsCards[i]);
    }

    public static void ForEachPhysicsPack(System.Action<WorldBoosterPack> action)
    {
        if (action == null)
            return;

        for (int i = 0; i < PhysicsPacks.Count; i++)
            action(PhysicsPacks[i]);
    }

    public static float GetDrawWorldY(WorldBoosterPack pack)
    {
        if (pack == null)
            return CardFactory.GroundHeightOffset();

        // A pack is a thick body, so its root rests higher than a flat card's on the same surface.
        float y = GetStackedWorldY(pack.GroundStackLayer) + pack.GroundRestLift;
        if (pack.GroundStackLayer == 0)
            y += GetUniqueDepthBiasForPack(pack);

        return y;
    }

    static float GetUniqueDepthBiasForPack(WorldBoosterPack pack)
    {
        int id = Mathf.Abs(pack.GetInstanceID());
        return (id % UniqueDepthBiasSteps) * (UniqueDepthBiasRange / UniqueDepthBiasSteps);
    }

    public static void ApplyStackHeight(WorldBoosterPack pack, bool placeOnTop = false, float maxDownwardShift = -1f)
    {
        if (pack == null || pack.IsInHand)
            return;

        TrackPack(pack);
        if (placeOnTop)
        {
            PlacePackOnTopOfOverlaps(pack, maxDownwardShift);
            return;
        }

        // Scatter / ground spawn: keep packs on the floor (layer 0), never float on card piles.
        pack.SetGroundStackLayer(0);
        Vector3 position = pack.transform.position;
        position.y = GetDrawWorldY(pack);
        pack.SetGroundRestPosition(position);
    }

    static void PlacePackOnTopOfOverlaps(WorldBoosterPack pack, float maxDownwardShift)
    {
        ScanRestingOverlaps(
            pack.transform,
            GetStackReferenceY(pack),
            null,
            pack,
            out int maxLayer,
            out float ceilingY);

        pack.SetGroundStackLayer(maxLayer + 1);

        Vector3 position = pack.transform.position;
        float stackedY = GetDrawWorldY(pack);
        // ceilingY was gathered in reference space; the pack's own transform sits its rest lift above that.
        if (ShouldTakeStackedY(position.y, stackedY, maxDownwardShift, ceilingY + pack.GroundRestLift))
        {
            position.y = stackedY;
            pack.SetGroundRestPosition(position);
        }

        CardFactory.LiftAboveFloor(pack.transform, pack.GetComponent<Rigidbody>());
    }

    public static bool OverlapsOnGround(WorldBoosterPack pack, WorldCard card)
    {
        if (pack == null || card == null)
            return false;

        return GetHorizontalBounds(pack.transform).Intersects(GetHorizontalBounds(card.transform));
    }

    public static bool OverlapsOnGround(WorldBoosterPack a, WorldBoosterPack b)
    {
        if (a == null || b == null)
            return false;

        return GetHorizontalBounds(a.transform).Intersects(GetHorizontalBounds(b.transform));
    }
}
