using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static CardGroundStack;
using Stopwatch = System.Diagnostics.Stopwatch;

/// <summary>Isolated old/new CPU comparisons. Never enters Play Mode or writes a player save.</summary>
public static class CardPilePerformanceValidation
{
    const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    const int BulkFlatStackThreshold = 256;
    static readonly List<WorldCard> CellScratch = new();
    static readonly List<WorldCard> OpenScratch = new();
    static readonly HashSet<WorldCard> RayCandidateSeen = new();
    static readonly List<WorldCard> GroundCards = (List<WorldCard>)typeof(CardGroundStack).GetField("GroundCards", Flags).GetValue(null);
    static readonly Dictionary<long, List<WorldCard>> SpatialBuckets = (Dictionary<long, List<WorldCard>>)typeof(CardGroundStack).GetField("SpatialBuckets", Flags).GetValue(null);
    static readonly Action RebuildSpatialBuckets = Bind<Action>("RebuildSpatialBuckets");
    static readonly Action<Vector3, List<WorldCard>> CollectNeighborhood = Bind<Action<Vector3, List<WorldCard>>>("CollectNeighborhood");
    static readonly Action<WorldCard, List<WorldCard>, bool> BuildLocalPile = Bind<Action<WorldCard, List<WorldCard>, bool>>("BuildLocalPile");
    static readonly Comparison<WorldCard> CompareGroundOrder = Bind<Comparison<WorldCard>>("CompareGroundOrder");
    static float SpatialCellSize => (float)typeof(CardGroundStack).GetProperty("SpatialCellSize", Flags).GetValue(null);
    static long PackCell(int x, int z) => ((long)x << 32) ^ (uint)z;
    static T Bind<T>(string name) where T : Delegate => (T)typeof(CardGroundStack).GetMethod(name, Flags).CreateDelegate(typeof(T));

    static double Measure(Action action, int iterations)
    {
        action(); // Warm caches, JIT and reusable buffers before measuring.
        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < iterations; i++) action();
        return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency / iterations;
    }

    [MenuItem("TCG Card Chaos/Validation/Card Piles (Isolated CPU Benchmark)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PlayerCardHand.Instance != null
            || GroundCards.Count != 0 || PhysicsCardCount != 0 || TrackedPackCount != 0)
            throw new InvalidOperationException("Run outside Play Mode with no live ground-card registry.");
        var report = new StringBuilder("Card pile CPU benchmark (Editor; not whole-game FPS).\n");
        var scene = EditorSceneManager.NewPreviewScene();
        ulong revision = MembershipRevision;
        int checks = 0;
        try
        {
            void Check(bool value, string label)
            {
                if (!value) throw new InvalidOperationException(label);
                checks++;
            }
            var root = new GameObject("Isolated pile benchmark");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            var cards = new List<WorldCard>();
            var poses = new List<Vector3>();
            var rotations = new List<Quaternion>();
            for (int i = 0; i < 1536; i++)
            {
                var go = new GameObject("Benchmark card " + i);
                go.transform.SetParent(root.transform, false);
                go.transform.localScale = Vector3.one * CardDimensions.GroundCardScale;
                cards.Add(go.AddComponent<WorldCard>());
            }
            void Layout(int count, bool layered, bool dispersed = false)
            {
                ClearAll();
                var random = new System.Random(7821);
                for (int i = 0; i < cards.Count; i++)
                {
                    var t = cards[i].transform;
                    float x = (float)random.NextDouble();
                    float z = (float)random.NextDouble();
                    t.position = i < count
                        ? new Vector3((x - .5f) * (dispersed ? 1.6f : .08f), layered ? i * .004f : 0f, (z - .5f) * (dispersed ? 1.6f : .08f))
                        : new Vector3(10f + (i % 48) * .4f, 0f, 10f + (i / 48) * .4f);
                    t.rotation = Quaternion.Euler(0f, x * 360f, 0f);
                    Track(cards[i]);
                }
                RebuildSpatialBuckets();
            }
            var legacy = new List<WorldCard>();
            var current = new List<WorldCard>();
            var down = new Ray(new Vector3(0f, 2f, 0f), Vector3.down);
            foreach (int count in new[] { 64, 128, 256, 512 })
            {
                Layout(count, false);
                double oldRay = Measure(() => LegacyCollectRayCandidates(down, 4.5f, legacy), 30);
                double newRay = Measure(() => CollectRayCandidates(down, 4.5f, current), 30);
                Check(legacy.Count == current.Count, "Candidate count is unchanged");
                for (int i = 0; i < legacy.Count; i++) Check(legacy[i] == current[i], "Candidate first-visit order is unchanged");
                double oldPile = Measure(() => LegacyBuildLocalPile(cards[0], legacy, true), 1);
                double newPile = Measure(() => BuildLocalPile(cards[0], current, true), 10);
                legacy.Sort(CompareGroundOrder); current.Sort(CompareGroundOrder);
                Check(legacy.Count == current.Count, "Dense pile membership unchanged");
                for (int i = 0; i < legacy.Count; i++) Check(legacy[i] == current[i], "Dense pile layer order unchanged");
                report.AppendLine($"{count} piled + {cards.Count - count} scattered: aim {oldRay:F4} -> {newRay:F4} ms; cluster incl. spatial rebuild {oldPile:F4} -> {newPile:F4} ms");
            }
            // Multiple separated groups, tall piles, angled rays and cell boundaries.
            foreach (bool layered in new[] { false, true })
            foreach (bool dispersed in new[] { false, true })
            {
                Layout(256, layered, dispersed);
                var random = new System.Random(339);
                for (int i = 0; i < 80; i++)
                {
                    var origin = new Vector3((float)random.NextDouble() * 4f - 2f, 2f, (float)random.NextDouble() * 4f - 2f);
                    var target = new Vector3((float)random.NextDouble() - .5f, (float)random.NextDouble() * .5f, (float)random.NextDouble() - .5f);
                    var ray = new Ray(origin, (target - origin).normalized);
                    LegacyCollectRayCandidates(ray, 4.5f, legacy); CollectRayCandidates(ray, 4.5f, current);
                    Check(legacy.Count == current.Count, "Angled ray candidate count");
                    for (int j = 0; j < legacy.Count; j++) Check(legacy[j] == current[j], "Angled ray candidate order");
                }
                for (int i = 0; i < 256; i += 17)
                {
                    LegacyBuildLocalPile(cards[i], legacy, false); BuildLocalPile(cards[i], current, false);
                    legacy.Sort(CompareGroundOrder); current.Sort(CompareGroundOrder);
                    Check(legacy.Count == current.Count, "Layered/disconnected cluster count");
                    for (int j = 0; j < legacy.Count; j++) Check(legacy[j] == current[j], "Layered/disconnected cluster ordering");
                }
            }
            Layout(512, true);
            foreach (var card in cards) { poses.Add(card.transform.position); rotations.Add(card.transform.rotation); }
            // Registry changes used by pickup, shelf placement and newly settled throws.
            var removed = cards[0];
            Untrack(removed);
            CollectRayCandidates(down, 4.5f, current);
            Check(!current.Contains(removed), "Picked-up card leaves aim candidates");
            removed.transform.position = new Vector3(-3f, 1f, -3f);
            TrackPhysicsPose(removed);
            CollectRayCandidates(new Ray(new Vector3(-3f, 3f, -3f), Vector3.down), 4.5f, current);
            Check(current.Contains(removed), "Settled throw is queryable in its new cell");
            Check(removed.transform.position == new Vector3(-3f, 1f, -3f), "Settled throw pose stays unchanged");
            Untrack(removed);
            for (int i = 1; i < cards.Count; i++)
            {
                Check(cards[i].transform.position == poses[i], "Queries/pickup relayout preserve neighbour positions");
                Check(cards[i].transform.rotation == rotations[i], "Queries/pickup relayout preserve front/back orientation");
            }
            // No per-query allocations once reusable scratch buffers have warmed up.
            for (int i = 0; i < 10; i++) { CollectRayCandidates(down, 4.5f, current); BuildLocalPile(cards[1], current, false); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 30; i++) { CollectRayCandidates(down, 4.5f, current); BuildLocalPile(cards[1], current, false); }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(bytes == 0, "Warmed aim/cluster work allocates zero managed bytes (got " + bytes + ")");
            report.AppendLine("PASS: " + checks + " equivalence/registry/pose checks; steady-state managed allocation: " + bytes + " bytes.");
        }
        catch (Exception exception)
        {
            report.AppendLine("FAIL: " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            ClearAll();
            EditorSceneManager.ClosePreviewScene(scene);
            typeof(CardGroundStack).GetProperty("MembershipRevision", Flags).SetValue(null, revision);
            File.WriteAllText("Temp/card-pile-performance-validation.txt", report.ToString());
            Debug.Log(report.ToString());
        }
    }

    // Frozen pre-optimization algorithms: compare the real old/new output on identical objects.
    static void LegacyCollectRayCandidates(Ray ray, float maxDistance, List<WorldCard> results)
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

    static void LegacyBuildLocalPile(WorldCard seed, List<WorldCard> results, bool rebuildSpatialBuckets = true)
    {
        results.Clear();
        if (seed == null || seed.IsInHand)
            return;

        if (rebuildSpatialBuckets)
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
}
