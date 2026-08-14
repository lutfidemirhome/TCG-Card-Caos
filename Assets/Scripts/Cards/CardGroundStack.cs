using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps overlapping flat world cards at distinct heights (GPU instancing stays enabled).
/// Every ground card also gets a unique micro depth bias so same-layer cards never z-fight.
/// </summary>
public static class CardGroundStack
{
    const float StackGap = 0.012f;
    const float HeightEpsilon = 0.0002f;
    const float UniqueDepthBiasRange = 0.0025f;
    const int UniqueDepthBiasSteps = 4096;
    const int BulkFlatStackThreshold = 256;

    static readonly List<WorldCard> GroundCards = new List<WorldCard>(512);
    static readonly HashSet<WorldCard> GroundCardSet = new HashSet<WorldCard>();
    static readonly List<WorldCard> ClusterScratch = new List<WorldCard>(16);
    static readonly List<WorldCard> OpenScratch = new List<WorldCard>(16);
    static readonly List<WorldCard> CellScratch = new List<WorldCard>(32);
    static readonly Dictionary<long, List<WorldCard>> SpatialBuckets = new Dictionary<long, List<WorldCard>>(512);

    public static float StackStep =>
        CardDimensions.Thickness * CardDimensions.WorldCardScale + StackGap;

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

        return GetStackedWorldY(card.GroundStackLayer) + GetUniqueDepthBias(card);
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
        bool bulk = GroundCards.Count >= BulkFlatStackThreshold;

        if (!bulk)
        {
            var affected = new List<WorldCard>(4);
            for (int i = 0; i < GroundCards.Count; i++)
            {
                WorldCard other = GroundCards[i];
                if (other != null && other != card && OverlapsOnGround(card, other))
                    affected.Add(other);
            }

            RemoveFromList(card);
            for (int i = 0; i < affected.Count; i++)
                RefreshCluster(affected[i]);
            return;
        }

        RemoveFromList(card);
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

    public static void ForEachTracked(System.Action<WorldCard> visit)
    {
        if (visit == null)
            return;

        for (int i = 0; i < GroundCards.Count; i++)
        {
            WorldCard card = GroundCards[i];
            if (card != null)
                visit(card);
        }
    }

    public static int TrackedCount => GroundCards.Count;

    public static WorldCard GetTracked(int index) => GroundCards[index];

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
        if (GroundCards.Count >= BulkFlatStackThreshold)
        {
            RebuildCellPiles();
            return;
        }

        RebuildClustered();
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

            long key = CellKey(card.transform.position, cellSize);
            if (!SpatialBuckets.TryGetValue(key, out List<WorldCard> bucket))
            {
                bucket = new List<WorldCard>(8);
                SpatialBuckets[key] = bucket;
            }

            bucket.Add(card);
        }
    }

    static void RefreshCellAt(Vector3 worldPos)
    {
        float cellSize = SpatialCellSize;
        long key = CellKey(worldPos, cellSize);
        CellScratch.Clear();

        for (int i = 0; i < GroundCards.Count; i++)
        {
            WorldCard card = GroundCards[i];
            if (card == null || card.IsInHand)
                continue;
            if (CellKey(card.transform.position, cellSize) != key)
                continue;

            CellScratch.Add(card);
        }

        ApplyPileLayers(CellScratch);
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
            if (card == null || card.IsInHand)
                continue;

            card.SetGroundStackLayer(layer);
            Vector3 position = card.transform.position;
            position.y = GetDrawWorldY(card);
            card.transform.position = position;
        }
    }

    /// <summary>Cell size for neighbor queries — sized to card footprint, not pile radius.</summary>
    static float SpatialCellSize
    {
        get
        {
            float footprint = Mathf.Max(CardDimensions.Width, CardDimensions.Height)
                * CardDimensions.WorldCardScale;
            return Mathf.Max(0.12f, footprint * 0.85f);
        }
    }

    static long CellKey(Vector3 worldPos, float cellSize)
    {
        int x = Mathf.FloorToInt(worldPos.x / cellSize);
        int z = Mathf.FloorToInt(worldPos.z / cellSize);
        return ((long)x << 32) ^ (uint)z;
    }

    static void RebuildClustered()
    {
        var processed = new HashSet<WorldCard>();
        for (int i = 0; i < GroundCards.Count; i++)
        {
            WorldCard seed = GroundCards[i];
            if (seed == null || seed.IsInHand || processed.Contains(seed))
                continue;

            BuildCluster(seed, ClusterScratch);
            ApplyPileLayers(ClusterScratch);
            for (int c = 0; c < ClusterScratch.Count; c++)
            {
                if (ClusterScratch[c] != null)
                    processed.Add(ClusterScratch[c]);
            }
        }
    }

    /// <summary>
    /// Restack overlapping ground cards under <paramref name="card"/>.
    /// When <paramref name="placeOnTop"/> is true (thrown/dropped card), that card becomes the top layer.
    /// </summary>
    public static void ApplyStackHeight(WorldCard card, bool placeOnTop = false)
    {
        if (card == null || card.IsInHand)
            return;

        Track(card);
        if (GroundCards.Count >= BulkFlatStackThreshold)
        {
            RefreshDirectOverlaps(card, placeOnTop ? card : null);
            return;
        }

        RefreshCluster(card, placeOnTop ? card : null);
    }

    public static void RefreshCluster(WorldCard seed, WorldCard forceOnTop = null)
    {
        if (GroundCards.Count >= BulkFlatStackThreshold)
        {
            if (seed != null)
                RefreshDirectOverlaps(seed, forceOnTop);
            return;
        }

        if (seed == null || seed.IsInHand)
            return;

        BuildCluster(seed, ClusterScratch);
        if (ClusterScratch.Count == 0)
            return;

        ApplyPileLayers(ClusterScratch, forceOnTop);
    }

    /// <summary>
    /// Restack only the seed and cards that actually overlap it (same feel as morning clusters, O(neighbors)).
    /// </summary>
    static void RefreshDirectOverlaps(WorldCard seed, WorldCard forceOnTop = null)
    {
        if (seed == null || seed.IsInHand)
            return;

        RebuildSpatialBuckets();
        CollectNeighborhood(seed.transform.position, CellScratch);
        ClusterScratch.Clear();
        ClusterScratch.Add(seed);

        for (int i = 0; i < CellScratch.Count; i++)
        {
            WorldCard other = CellScratch[i];
            if (other == null || other == seed || other.IsInHand)
                continue;
            if (other.GetComponent<Rigidbody>() != null)
                continue;
            if (!OverlapsOnGround(seed, other))
                continue;

            ClusterScratch.Add(other);
        }

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

        float deltaY = a.transform.position.y - b.transform.position.y;
        if (deltaY < -HeightEpsilon)
            return -1;
        if (deltaY > HeightEpsilon)
            return 1;

        return WorldCardDrawOrder.CompareStableInstanceId(a, b);
    }

    static void BuildCluster(WorldCard seed, List<WorldCard> results)
    {
        results.Clear();
        if (seed == null || seed.IsInHand)
            return;

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

            for (int i = 0; i < GroundCards.Count; i++)
            {
                WorldCard other = GroundCards[i];
                if (other == null || other.IsInHand || results.Contains(other))
                    continue;
                if (other.GetComponent<Rigidbody>() != null)
                    continue;
                if (!OverlapsOnGround(current, other))
                    continue;

                OpenScratch.Add(other);
            }
        }

        if (!results.Contains(seed))
            results.Add(seed);
    }

    static bool OverlapsOnGround(WorldCard a, WorldCard b)
    {
        Bounds aBounds = GetHorizontalBounds(a);
        Bounds bBounds = GetHorizontalBounds(b);
        aBounds.extents = new Vector3(aBounds.extents.x, 10f, aBounds.extents.z);
        bBounds.extents = new Vector3(bBounds.extents.x, 10f, bBounds.extents.z);
        return aBounds.Intersects(bBounds);
    }

    static Bounds GetHorizontalBounds(WorldCard card)
    {
        float scale = Mathf.Max(card.transform.lossyScale.x, CardDimensions.WorldCardScale);
        float width = CardDimensions.Width * scale;
        float height = CardDimensions.Height * scale;
        float yaw = card.transform.eulerAngles.y * Mathf.Deg2Rad;
        float cos = Mathf.Abs(Mathf.Cos(yaw));
        float sin = Mathf.Abs(Mathf.Sin(yaw));
        float extentX = width * cos + height * sin;
        float extentZ = width * sin + height * cos;
        return new Bounds(card.transform.position, new Vector3(extentX, 0.01f, extentZ));
    }
}
