using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = System.Random;

/// <summary>Editor-only set dressing. Repositions existing items; never creates cards or pack contents.</summary>
public static class VideoCardMoundBuilder
{
    const int Seed = 20260926;
    const float Cell = 0.5f;
    const float Clearance = 0.0012f;
    const float FloorY = 0.0665f;
    public static readonly Vector3 Center = new Vector3(2.07f, FloorY, -1.7f);

    struct Body
    {
        public Vector3 center, half, right, up, forward;
        public float minX, maxX, minZ, maxZ;
        public float Radius(Vector3 axis) => Mathf.Abs(Vector3.Dot(right, axis)) * half.x
            + Mathf.Abs(Vector3.Dot(up, axis)) * half.y + Mathf.Abs(Vector3.Dot(forward, axis)) * half.z;
        public void Bounds()
        {
            float x = Radius(Vector3.right), z = Radius(Vector3.forward);
            minX = center.x - x; maxX = center.x + x;
            minZ = center.z - z; maxZ = center.z + z;
        }
    }

    [MenuItem("TCG Card Chaos/Video/Rebuild Mixed Card Mountain")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Build the video mound outside Play Mode.");
        if (SceneManager.GetActiveScene().path != "Assets/Scenes/MainScene.unity")
            throw new InvalidOperationException("Open MainScene before building the video mound.");
        PhysicsLevelLayout layout = PhysicsLevelLayout.FindExisting();
        if (layout == null || layout.MainLevelRoot == null)
            throw new InvalidOperationException("The authored card layout is missing.");
        Transform root = layout.MainLevelRoot.Find(PhysicsLevelLayout.MixAllName);
        if (root == null) throw new InvalidOperationException("Mix_All is missing.");

        var items = new List<Transform>();
        foreach (WorldCard card in UnityEngine.Object.FindObjectsByType<WorldCard>(FindObjectsSortMode.None))
            if (!card.IsInHand && !card.IsFlyingToShelf) items.Add(card.transform);
        foreach (WorldBoosterPack pack in UnityEngine.Object.FindObjectsByType<WorldBoosterPack>(FindObjectsSortMode.None))
            if (!pack.IsInHand) items.Add(pack.transform);
        items.Sort((a, b) => string.CompareOrdinal(PersistentId.Resolve(a), PersistentId.Resolve(b)));
        var random = new Random(Seed);
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            Transform temp = items[i]; items[i] = items[j]; items[j] = temp;
        }
        // Validate before changing any scene object.
        foreach (Transform item in items)
            if (item.GetComponent<BoxCollider>() == null)
                throw new InvalidOperationException("Missing item bounds: " + item.name);

        var bodies = new List<Body>(items.Count);
        var buckets = new Dictionary<Vector2Int, List<int>>();
        var candidates = new HashSet<int>();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Arrange video card mountain");
        float highest = FloorY;
        int packs = 0;
        try
        {
            foreach (Transform item in items)
            {
                BoxCollider box = item.GetComponent<BoxCollider>();
                Vector3 scale = item.lossyScale;
                Vector3 half = Vector3.Scale(box.size * 0.5f,
                    new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                // Dense centre, an irregular skirt, and no regular stacks or category groups.
                float x, z;
                if (random.NextDouble() < 0.82)
                {
                    float radius = Mathf.Min(3.1f, 0.85f * Mathf.Sqrt(-2f * Mathf.Log(Mathf.Max(0.00001f, (float)random.NextDouble()))));
                    float angle = Range(random, 0f, Mathf.PI * 2f);
                    x = Mathf.Cos(angle) * radius; z = Mathf.Sin(angle) * radius * 1.08f;
                }
                else
                {
                    float radius = Mathf.Sqrt(Range(random, 0f, 1f)) * 3.5f;
                    float angle = Range(random, 0f, Mathf.PI * 2f);
                    x = Mathf.Cos(angle) * radius; z = Mathf.Sin(angle) * radius * 1.08f;
                }
                float r = new Vector2(x, z).magnitude;
                float slope = Mathf.Sin(Mathf.Clamp01(r / 2.8f) * Mathf.PI) * 0.45f;
                Vector3 normal = new Vector3(x / Mathf.Max(r, 0.01f) * slope, 1f, z / Mathf.Max(r, 0.01f) * slope).normalized;
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal)
                    * Quaternion.Euler(Range(random, -8f, 8f), Range(random, 0f, 360f), Range(random, -8f, 8f));
                if (item.GetComponent<WorldCard>() != null && random.NextDouble() < 0.07)
                    rotation *= Quaternion.AngleAxis(180f, Vector3.forward);
                var body = new Body {
                    center = new Vector3(Center.x + x, 0f, Center.z + z), half = half,
                    right = rotation * Vector3.right, up = rotation * Vector3.up, forward = rotation * Vector3.forward
                };
                body.Bounds();
                candidates.Clear();
                // Include the whole possible rotation footprint, not just the initial box.
                float reach = half.magnitude;
                for (int cx = Bin(body.center.x - reach); cx <= Bin(body.center.x + reach); cx++)
                    for (int cz = Bin(body.center.z - reach); cz <= Bin(body.center.z + reach); cz++)
                        if (buckets.TryGetValue(new Vector2Int(cx, cz), out List<int> indices))
                            foreach (int index in indices) candidates.Add(index);
                Settle(ref body, ref rotation, candidates, bodies);
                float y = body.center.y;
                // Calculate a root pose from the actual box centre, preserving authored item scale.
                Vector3 position = body.center - rotation * Vector3.Scale(box.center, scale);
                Undo.RecordObject(item, "Move video item");
                WorldCard card = item.GetComponent<WorldCard>();
                CardShelfSlot shelfSlot = item.GetComponentInParent<CardShelfSlot>();
                if (card != null && shelfSlot != null) shelfSlot.ClearIfMatches(card);
                if (item.parent != root) Undo.SetTransformParent(item, root, "Move item to video pile");
                item.SetPositionAndRotation(position, rotation);
                Rigidbody rb = item.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Undo.RecordObject(rb, "Keep authored pile stationary");
                    rb.isKinematic = true; rb.position = position; rb.rotation = rotation;
                }
                PhysicsLevelItem authored = item.GetComponent<PhysicsLevelItem>();
                if (authored != null) { Undo.RecordObject(authored, "Bake video pile"); authored.MarkBaked(); }
                if (item.GetComponent<WorldBoosterPack>() != null) packs++;
                EditorUtility.SetDirty(item);
                int id = bodies.Count;
                bodies.Add(body);
                for (int cx = Bin(body.minX); cx <= Bin(body.maxX); cx++)
                    for (int cz = Bin(body.minZ); cz <= Bin(body.maxZ); cz++)
                    {
                        var key = new Vector2Int(cx, cz);
                        if (!buckets.TryGetValue(key, out List<int> indices)) buckets.Add(key, indices = new List<int>());
                        indices.Add(id);
                    }
                highest = Mathf.Max(highest, y + body.Radius(Vector3.up));
            }
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[Video Mound] cards={items.Count - packs}, packs={packs}, height={highest - FloorY:F2}m. Existing identities, artwork and pack contents preserved.");
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(Center + Vector3.up * 0.75f, Quaternion.LookRotation(new Vector3(-0.3f, -0.3f, 1f)), 5f);
        }
        finally { Undo.CollapseUndoOperations(undoGroup); }
    }

    static int Bin(float value) => Mathf.FloorToInt(value / Cell);
    static float Range(Random random, float min, float max) => min + (max - min) * (float)random.NextDouble();

    // Minimise centre-of-mass height while maintaining separation. A box touching only one
    // corner can still fall by rotating; a vertical sweep alone freezes that unsupported pose.
    // All trials use the real collider and remain editor-only. Existing placed items never move.
    static void Settle(ref Body body, ref Quaternion rotation, HashSet<int> candidates, List<Body> bodies)
    {
        body.center.y = RestHeight(body, candidates, bodies);
        for (float step = 12f; step >= 0.35f; step *= 0.5f)
        {
            for (int iteration = 0; iteration < 16; iteration++)
            {
                Body best = body;
                Quaternion bestRotation = rotation;
                for (int direction = 0; direction < 8; direction++)
                {
                    float angle = direction * Mathf.PI * 0.25f;
                    Quaternion trialRotation = Quaternion.AngleAxis(step,
                        new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle))) * rotation;
                    Body trial = body;
                    trial.right = trialRotation * Vector3.right;
                    trial.up = trialRotation * Vector3.up;
                    trial.forward = trialRotation * Vector3.forward;
                    // Cards stay laid over the mound rather than balancing upright on an edge.
                    if (Mathf.Abs(trial.up.y) < 0.57f) continue;
                    trial.Bounds();
                    trial.center.y = RestHeight(trial, candidates, bodies);
                    if (trial.center.y < best.center.y - 0.000002f)
                    { best = trial; bestRotation = trialRotation; }
                }
                if (best.center.y >= body.center.y - 0.000002f) break;
                body = best; rotation = bestRotation;
            }
        }
    }

    static float RestHeight(Body body, HashSet<int> candidates, List<Body> bodies)
    {
        float y = FloorY + body.Radius(Vector3.up) + Clearance;
        foreach (int index in candidates)
            if (ContactHeight(body, bodies[index], out float contact)) y = Mathf.Max(y, contact + Clearance);
        return y;
    }

    // Exact swept-box contact along world Y (15 separating axes). Unlike noisy height offsets,
    // each new tilted item stops on an existing box, without intersecting or moving old items.
    static bool ContactHeight(Body a, Body b, out float height)
    {
        height = 0f;
        if (a.maxX < b.minX || a.minX > b.maxX || a.maxZ < b.minZ || a.minZ > b.maxZ) return false;
        float low = float.NegativeInfinity, high = float.PositiveInfinity;
        if (!Axis(a, b, a.right, ref low, ref high) || !Axis(a, b, a.up, ref low, ref high)
            || !Axis(a, b, a.forward, ref low, ref high) || !Axis(a, b, b.right, ref low, ref high)
            || !Axis(a, b, b.up, ref low, ref high) || !Axis(a, b, b.forward, ref low, ref high)
            || !Axis(a, b, Vector3.Cross(a.right, b.right), ref low, ref high)
            || !Axis(a, b, Vector3.Cross(a.right, b.up), ref low, ref high)
            || !Axis(a, b, Vector3.Cross(a.right, b.forward), ref low, ref high)
            || !Axis(a, b, Vector3.Cross(a.up, b.right), ref low, ref high)
            || !Axis(a, b, Vector3.Cross(a.up, b.up), ref low, ref high)
            || !Axis(a, b, Vector3.Cross(a.up, b.forward), ref low, ref high)
            || !Axis(a, b, Vector3.Cross(a.forward, b.right), ref low, ref high)
            || !Axis(a, b, Vector3.Cross(a.forward, b.up), ref low, ref high)
            || !Axis(a, b, Vector3.Cross(a.forward, b.forward), ref low, ref high)) return false;
        height = high;
        return !float.IsInfinity(high) && low <= high;
    }

    static bool Axis(Body a, Body b, Vector3 axis, ref float low, ref float high)
    {
        if (axis.sqrMagnitude < 0.00000001f) return true;
        float radius = a.Radius(axis) + b.Radius(axis);
        Vector3 delta = a.center - b.center;
        float horizontal = delta.x * axis.x + delta.z * axis.z - b.center.y * axis.y;
        if (Mathf.Abs(axis.y) < 0.000001f) return Mathf.Abs(horizontal) <= radius;
        float from = (-radius - horizontal) / axis.y, to = (radius - horizontal) / axis.y;
        if (from > to) { float swap = from; from = to; to = swap; }
        low = Mathf.Max(low, from); high = Mathf.Min(high, to);
        return low <= high;
    }
}
