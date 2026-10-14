using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = System.Random;

/// <summary>One-off editor authoring for the cpi trailer branch. Never moves source cards.</summary>
public static class CpiSceneDressingBuilder
{
    const string CopyPrefix = "CPI_Copy_";
    const string MoundName = "Mix_Cpi_TrailerCopies";
    const float TargetHeight = 4.26f; // video_tcg_3 mound was 2.13 m tall.
    const int Seed = 20260926;
    const float Cell = 0.5f;
    const float Clearance = 0.0012f;
    const float FloorY = 0.0665f;
    static readonly Vector3 Center = new Vector3(2.07f, FloorY, -1.7f);

    [Serializable] public class ItemSnapshot
    {
        public string id, parent, definition;
        public Vector3 position, scale;
        public Quaternion rotation;
    }
    [Serializable] public class Snapshot { public ItemSnapshot[] items; }
    const string SnapshotPath = "Temp/CpiOriginalItems.json";

    static WorldCard[] Cards() => UnityEngine.Object.FindObjectsByType<WorldCard>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Where(c => c.gameObject.scene == SceneManager.GetActiveScene()).ToArray();
    static bool IsCopy(Component item) => item.name.StartsWith(CopyPrefix, StringComparison.Ordinal);
    static string PathOf(Transform t) => t == null ? "" : PathOf(t.parent) + "/" + t.name;
    static bool Ground(WorldCard c) => c.GetComponentInParent<CardShelfSlot>() == null && c.GetComponentInParent<PsaCabinetSlot>() == null;
    static void RequireScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != "Assets/Scenes/MainScene.unity")
            throw new InvalidOperationException("Open MainScene outside Play Mode.");
    }

    static ItemSnapshot Capture(Component item)
    {
        WorldCard card = item as WorldCard;
        return new ItemSnapshot {
            id = PersistentId.Resolve(item), parent = PathOf(item.transform.parent),
            position = item.transform.position, rotation = item.transform.rotation, scale = item.transform.localScale,
            definition = card == null ? "pack" : card.UsesPsaSlab ? $"TCG:{card.PsaSlotNumber}:{card.PsaVariantIndex}:{card.PsaSet}" : card.Definition?.DefinitionId
        };
    }

    public static Snapshot CaptureOriginals()
    {
        return new Snapshot { items = Cards().Where(c => !IsCopy(c)).Cast<Component>()
            .Concat(UnityEngine.Object.FindObjectsByType<WorldBoosterPack>(FindObjectsSortMode.None))
            .Select(Capture).OrderBy(s => s.id, StringComparer.Ordinal).ToArray() };
    }

    public static void Inspect()
    {
        RequireScene();
        var cards = Cards();
        var shelves = UnityEngine.Object.FindObjectsByType<CardShelf>(FindObjectsSortMode.None);
        var psa = UnityEngine.Object.FindObjectsByType<PsaCabinet>(FindObjectsSortMode.None);
        var text = $"Original cards={cards.Count(c => !IsCopy(c))}, copies={cards.Count(IsCopy)}, normal cabinets={shelves.Length}, TCG cabinets={psa.Length}\n";
        foreach (var s in shelves.OrderBy(s => PathOf(s.transform)))
            text += $"{PathOf(s.transform)} category={s.CategoryId} slots={s.CountPlaceableSlots()} occupied={s.CountOccupiedSlots()} pool={cards.Count(c => !IsCopy(c) && c.Definition != null && s.AcceptsDefinition(c.Definition))}\n";
        foreach (var s in psa) text += $"TCG {PathOf(s.transform)} slots={s.CountPlaceableSlots()} occupied={s.CountOccupiedSlots()}\n";
        File.WriteAllText("Temp/CpiDressingInspection.txt", text);
        if (!File.Exists(SnapshotPath)) File.WriteAllText(SnapshotPath, JsonUtility.ToJson(CaptureOriginals(), true));
        Debug.Log("[CPI Dressing] Inspection written.");
    }

    static WorldCard Clone(WorldCard source, string suffix)
    {
        WorldCard copy = UnityEngine.Object.Instantiate(source);
        copy.name = CopyPrefix + suffix + "_" + source.name;
        foreach (PersistentId identity in copy.GetComponentsInChildren<PersistentId>(true)) identity.AssignNew();
        PersistentId.GetOrCreate(copy.gameObject).AssignNew();
        copy.StripEditorRigidbody();
        PhysicsLevelItem baked = copy.GetComponent<PhysicsLevelItem>();
        if (baked == null) baked = copy.gameObject.AddComponent<PhysicsLevelItem>();
        baked.Configure(PhysicsLevelItem.AreaKind.Main, 0, true);
        return copy;
    }

    [MenuItem("Tools/TCG Trailer/Fill Cabinets 60% With Copies")]
    public static void FillCabinets()
    {
        RequireScene();
        if (!File.Exists(SnapshotPath)) File.WriteAllText(SnapshotPath, JsonUtility.ToJson(CaptureOriginals(), true));
        var pool = Cards().Where(c => !IsCopy(c)).OrderBy(c => c.Definition?.DefinitionId).ThenBy(c => PersistentId.Resolve(c)).ToArray();
        var created = new List<WorldCard>();
        try
        {
            foreach (CardShelf shelf in UnityEngine.Object.FindObjectsByType<CardShelf>(FindObjectsSortMode.None).OrderBy(s => PathOf(s.transform)))
            {
                shelf.RefreshSlotCache();
                var slots = shelf.GetComponentsInChildren<CardShelfSlot>().Where(s => s.GetComponentInParent<CardShelf>() == shelf)
                    .OrderBy(s => s.SlotNumber).ThenBy(s => s.RowIndex).ToArray();
                int target = Mathf.RoundToInt(shelf.CountPlaceableSlots() * 0.6f);
                var sources = pool.Where(c => !c.UsesPsaSlab && shelf.AcceptsDefinition(c.Definition)).ToArray();
                foreach (var slot in slots)
                {
                    if (shelf.CountOccupiedSlots() >= target) break;
                    if (!slot.IsEmpty) continue;
                    var source = sources.FirstOrDefault(c => shelf.IsCorrectPlacement(c, slot));
                    if (source == null) continue;
                    var copy = Clone(source, "Shelf");
                    created.Add(copy);
                    copy.ApplySolidEditorCollider();
                    slot.RestoreOccupiedCard(copy, shelf.SurfacePadding, true, playPlacementFeedback: false);
                    EditorUtility.SetDirty(slot);
                }
                if (shelf.CountOccupiedSlots() != target || shelf.CountCorrectlyPlacedCards() != target)
                    throw new InvalidOperationException($"Cannot correctly fill {shelf.name}: {shelf.CountOccupiedSlots()}/{target}");
            }
            int variant = 0;
            foreach (PsaCabinet shelf in UnityEngine.Object.FindObjectsByType<PsaCabinet>(FindObjectsSortMode.None).OrderBy(s => PathOf(s.transform)))
            {
                int target = Mathf.RoundToInt(shelf.CountPlaceableSlots() * 0.6f);
                foreach (var slot in shelf.Slots.Where(s => s != null && s.gameObject.activeInHierarchy))
                {
                    if (shelf.CountOccupiedSlots() >= target) break;
                    if (!slot.IsEmpty) continue;
                    var sources = pool.Where(c => c.UsesPsaSlab && slot.IsCorrectPlacement(c)).ToArray();
                    if (sources.Length == 0) throw new InvalidOperationException("Missing TCG source: " + slot.SlotNumber);
                    var copy = Clone(sources[variant++ % sources.Length], "TCG");
                    created.Add(copy);
                    slot.RestoreOccupiedCard(copy, playPlacementFeedback: false);
                    EditorUtility.SetDirty(slot);
                }
                if (shelf.CountOccupiedSlots() != target || shelf.CountCorrectlyPlacedCards() != target)
                    throw new InvalidOperationException("Incorrect TCG occupancy: " + shelf.name);
            }
            ValidateOriginals();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Inspect();
            Debug.Log($"[CPI Dressing] Filled every cabinet to 60%; added {created.Count} copies. Original items unchanged.");
        }
        catch
        {
            foreach (var card in created)
            {
                card.GetComponentInParent<CardShelfSlot>()?.ClearIfMatches(card);
                card.GetComponentInParent<PsaCabinetSlot>()?.ClearOccupant();
                UnityEngine.Object.DestroyImmediate(card.gameObject);
            }
            throw;
        }
    }

    public static void ValidateOriginals(Snapshot baseline = null)
    {
        if (baseline == null) baseline = JsonUtility.FromJson<Snapshot>(File.ReadAllText(SnapshotPath));
        Snapshot current = CaptureOriginals();
        if (baseline.items.Length != current.items.Length) throw new InvalidOperationException("Original item count changed.");
        for (int i = 0; i < baseline.items.Length; i++)
        {
            var a = baseline.items[i]; var b = current.items[i];
            if (a.id != b.id || a.parent != b.parent || a.definition != b.definition ||
                Vector3.Distance(a.position, b.position) > 0.00001f || Quaternion.Angle(a.rotation, b.rotation) > 0.01f ||
                Vector3.Distance(a.scale, b.scale) > 0.00001f)
                throw new InvalidOperationException("Original item changed: " + a.id + "\nBefore: " + JsonUtility.ToJson(a) + "\nAfter: " + JsonUtility.ToJson(b));
        }
        var ids = UnityEngine.Object.FindObjectsByType<PersistentId>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(p => p.gameObject.scene == SceneManager.GetActiveScene() && p.HasValue).Select(p => p.Value).ToArray();
        if (ids.Distinct().Count() != ids.Length) throw new InvalidOperationException("Duplicate persistent IDs.");
    }

    public static void InstallToggle()
    {
        RequireScene();
        var layout = PhysicsLevelLayout.FindExisting();
        Transform root = layout.MainLevelRoot.Find(MoundName);
        Transform host = layout.MainLevelRoot.Find("Mix_Cpi_Trailer_Setup");
        if (host == null)
        {
            host = new GameObject("Mix_Cpi_Trailer_Setup").transform;
            host.SetParent(layout.MainLevelRoot, false);
        }
        if (root == null) root = host.Find(MoundName);
        if (root == null) throw new InvalidOperationException("Build the mound first.");
        root.SetParent(host, true);
        var toggle = host.GetComponent<CpiTrailerMoundToggle>();
        if (toggle == null) toggle = host.gameObject.AddComponent<CpiTrailerMoundToggle>();
        var serialized = new SerializedObject(toggle);
        serialized.FindProperty("moundRoot").objectReferenceValue = root.gameObject;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        root.gameObject.SetActive(false);
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Trailer")) AssetDatabase.CreateFolder("Assets/Prefabs", "Trailer");
        PersistMoundMaterials(root);
        PrefabUtility.SaveAsPrefabAssetAndConnect(host.gameObject, "Assets/Prefabs/Trailer/CpiCardMound.prefab", InteractionMode.AutomatedAction);
        ValidateOriginals();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    static void PersistMoundMaterials(Transform root)
    {
        const string path = "Assets/Prefabs/Trailer/CpiMoundMaterials.asset";
        var saved = new Dictionary<Material, Material>();
        bool exists = AssetDatabase.LoadMainAssetAtPath(path) != null;
        var reusable = exists ? AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>()
            .GroupBy(m => m.name).ToDictionary(g => g.Key, g => g.ToArray()) : new Dictionary<string, Material[]>();
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (source == null || AssetDatabase.Contains(source)) continue;
                if (!saved.TryGetValue(source, out var material))
                {
                    if (reusable.TryGetValue(source.name, out var matches))
                        material = matches.FirstOrDefault(m => m.shader == source.shader && m.mainTexture == source.mainTexture);
                    if (material == null)
                    {
                        material = new Material(source) { name = source.name, hideFlags = HideFlags.None };
                        if (exists) AssetDatabase.AddObjectToAsset(material, path);
                        else { AssetDatabase.CreateAsset(material, path); exists = true; }
                    }
                    saved.Add(source, material);
                }
                materials[i] = material;
            }
            renderer.sharedMaterials = materials;
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/TCG Trailer/Build Double Height Card Mound With Copies")]
    public static void BuildMound()
    {
        BuildMoundGeometry();
        InstallToggle();
    }

    public static void RebuildMound()
    {
        RequireScene();
        var parent = PhysicsLevelLayout.FindExisting().MainLevelRoot;
        var previous = parent.Find("Mix_Cpi_Trailer_Setup");
        if (previous == null) throw new InvalidOperationException("No previous CPI mound.");
        previous.name = "CPI_Previous_Mound";
        try
        {
            BuildMoundGeometry();
        }
        catch { previous.name = "Mix_Cpi_Trailer_Setup"; throw; }
        UnityEngine.Object.DestroyImmediate(previous.gameObject);
        InstallToggle();
    }

    static void BuildMoundGeometry()
    {
        RequireScene();
        var layout = PhysicsLevelLayout.FindExisting();
        if (layout == null || layout.MainLevelRoot == null) throw new InvalidOperationException("Missing Main_Level.");
        if (layout.MainLevelRoot.Find(MoundName) != null || layout.MainLevelRoot.Find("Mix_Cpi_Trailer_Setup/" + MoundName) != null)
            throw new InvalidOperationException("The CPI mound already exists.");
        if (!File.Exists(SnapshotPath)) File.WriteAllText(SnapshotPath, JsonUtility.ToJson(CaptureOriginals(), true));
        // Same swept-box settling as video_tcg_3; shrink the deposition radius as the
        // mound grows to build a broad foot and a tapered peak, not a vertical column.
        var sources = Cards().Where(c => !IsCopy(c) && Ground(c) && !c.UsesPsaSlab && c.Definition != null)
            .OrderBy(c => PersistentId.Resolve(c)).ToArray();
        if (sources.Length == 0) throw new InvalidOperationException("No source cards.");
        var random = new Random(Seed);
        for (int i = sources.Length - 1; i > 0; i--) { int j = random.Next(i + 1); var v = sources[i]; sources[i] = sources[j]; sources[j] = v; }
        var bodies = new List<Body>();
        var buckets = new Dictionary<Vector2Int, List<int>>();
        var candidates = new HashSet<int>();
        // Rest on the existing floor cards rather than intersecting or moving them.
        foreach (var c in Cards().Where(c => !IsCopy(c) && Ground(c)))
        {
            var box = c.GetComponent<BoxCollider>();
            if (box == null || Vector2.Distance(new Vector2(c.transform.position.x, c.transform.position.z), new Vector2(Center.x, Center.z)) > 5f) continue;
            var body = MakeBody(c.transform, box);
            AddBody(body, bodies, buckets);
        }
        var root = new GameObject(MoundName).transform;
        root.SetParent(layout.MainLevelRoot, false);
        float highest = FloorY;
        int count = 0;
        try
        {
            while (highest < FloorY + TargetHeight && count < 14000)
            {
                var source = sources[count % sources.Length];
                var box = source.GetComponent<BoxCollider>();
                Vector3 scale = source.transform.lossyScale;
                Vector3 half = Vector3.Scale(box.size * 0.5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                float progress = Mathf.Clamp01((highest - FloorY) / TargetHeight);
                float spread = Mathf.Lerp(1.28f, 0.18f, progress);
                float radius = random.NextDouble() < 0.95
                    ? Mathf.Min(Mathf.Lerp(3.5f, 0.35f, progress), spread * Mathf.Sqrt(-2f * Mathf.Log(Mathf.Max(0.00001f, (float)random.NextDouble()))))
                    : Mathf.Sqrt(Range(random, 0f, 1f)) * 3.5f;
                float angle = Range(random, 0f, Mathf.PI * 2f);
                float x = Mathf.Cos(angle) * radius, z = Mathf.Sin(angle) * radius * 1.08f;
                float slope = Mathf.Sin(Mathf.Clamp01(radius / 2.8f) * Mathf.PI) * 0.45f;
                Vector3 normal = new Vector3(x / Mathf.Max(radius, 0.01f) * slope, 1f, z / Mathf.Max(radius, 0.01f) * slope).normalized;
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal)
                    * Quaternion.Euler(Range(random, -8f, 8f), Range(random, 0f, 360f), Range(random, -8f, 8f));
                if (random.NextDouble() < 0.07) rotation *= Quaternion.AngleAxis(180f, Vector3.forward);
                var body = new Body { center = new Vector3(Center.x + x, 0f, Center.z + z), half = half,
                    right = rotation * Vector3.right, up = rotation * Vector3.up, forward = rotation * Vector3.forward };
                body.Bounds();
                candidates.Clear();
                float reach = half.magnitude;
                for (int cx = Bin(body.center.x - reach); cx <= Bin(body.center.x + reach); cx++)
                    for (int cz = Bin(body.center.z - reach); cz <= Bin(body.center.z + reach); cz++)
                        if (buckets.TryGetValue(new Vector2Int(cx, cz), out var indices)) foreach (int index in indices) candidates.Add(index);
                Settle(ref body, ref rotation, candidates, bodies);
                var copy = Clone(source, "Mound");
                copy.transform.SetParent(root, true);
                copy.transform.SetPositionAndRotation(body.center - rotation * Vector3.Scale(box.center, scale), rotation);
                AddBody(body, bodies, buckets);
                highest = Mathf.Max(highest, body.center.y + body.Radius(Vector3.up));
                count++;
                if (count % 100 == 0)
                {
                    File.WriteAllText("Temp/CpiMoundProgress.txt", $"{count} copies, height {highest - FloorY:F3} m");
                    if (EditorUtility.DisplayCancelableProgressBar("CPI copied card mound", $"{count} copies — {highest - FloorY:F2} / {TargetHeight:F2} m", (highest - FloorY) / TargetHeight))
                        throw new OperationCanceledException();
                }
            }
            if (highest < FloorY + TargetHeight) throw new InvalidOperationException("Mound target height not reached.");
            ValidateOriginals();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            File.WriteAllText("Temp/CpiMoundResult.txt", $"{count} copies, height {highest - FloorY:F3} m, originals unchanged.");
            Debug.Log($"[CPI Dressing] Mound: {count} copied cards, height {highest - FloorY:F3} m. Original items unchanged.");
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.LookAt(Center + Vector3.up * 1.9f, Quaternion.LookRotation(new Vector3(-0.3f, -0.2f, 1f)), 6f);
        }
        catch { UnityEngine.Object.DestroyImmediate(root.gameObject); throw; }
        finally { EditorUtility.ClearProgressBar(); }
    }

    static Body MakeBody(Transform t, BoxCollider box)
    {
        Vector3 scale = t.lossyScale;
        var body = new Body { center = t.TransformPoint(box.center),
            half = Vector3.Scale(box.size * 0.5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))),
            right = t.right, up = t.up, forward = t.forward };
        body.Bounds(); return body;
    }
    static void AddBody(Body body, List<Body> bodies, Dictionary<Vector2Int, List<int>> buckets)
    {
        int id = bodies.Count; bodies.Add(body);
        for (int x = Bin(body.minX); x <= Bin(body.maxX); x++)
            for (int z = Bin(body.minZ); z <= Bin(body.maxZ); z++)
            {
                var key = new Vector2Int(x, z);
                if (!buckets.TryGetValue(key, out var indices)) buckets.Add(key, indices = new List<int>());
                indices.Add(id);
            }
    }

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
