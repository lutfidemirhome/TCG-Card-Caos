using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps overlapping flat world cards at distinct heights (GPU instancing stays enabled).
/// Only the top card in a pile keeps its collider for interaction.
/// </summary>
public static class CardGroundStack
{
    const float StackGap = 0.008f;
    const float HeightEpsilon = 0.0002f;

    static readonly List<WorldCard> GroundCards = new List<WorldCard>(512);
    static readonly List<WorldCard> ClusterScratch = new List<WorldCard>(16);
    static readonly List<WorldCard> OpenScratch = new List<WorldCard>(16);

    public static float StackStep =>
        CardDimensions.Thickness * CardDimensions.WorldCardScale + StackGap;

    public static float GetStackedWorldY(int layer)
    {
        return CardFactory.GroundHeightOffset() + Mathf.Max(0, layer) * StackStep;
    }

    public static void Track(WorldCard card)
    {
        if (card == null || GroundCards.Contains(card))
            return;

        GroundCards.Add(card);
    }

    public static void Untrack(WorldCard card)
    {
        if (card == null)
            return;

        var affected = new List<WorldCard>(4);
        for (int i = 0; i < GroundCards.Count; i++)
        {
            WorldCard other = GroundCards[i];
            if (other != null && other != card && OverlapsOnGround(card, other))
                affected.Add(other);
        }

        GroundCards.Remove(card);

        for (int i = 0; i < affected.Count; i++)
            RefreshCluster(affected[i]);
    }

    public static void ApplyStackHeight(WorldCard card)
    {
        if (card == null || card.IsInHand)
            return;

        Track(card);
        RefreshCluster(card);
    }

    public static void RefreshCluster(WorldCard seed)
    {
        if (seed == null || seed.IsInHand)
            return;

        BuildCluster(seed, ClusterScratch);
        if (ClusterScratch.Count == 0)
            return;

        ClusterScratch.Sort(CompareGroundOrder);

        for (int i = 0; i < ClusterScratch.Count; i++)
        {
            WorldCard card = ClusterScratch[i];
            if (card == null || card.IsInHand)
                continue;

            card.SetGroundStackLayer(i);

            Vector3 position = card.transform.position;
            position.y = GetStackedWorldY(i);
            card.transform.position = position;
        }
    }

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

        return a.GetInstanceID().CompareTo(b.GetInstanceID());
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
