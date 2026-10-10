using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Authors movable placeholders once; never spawns objects or searches the shop during gameplay.</summary>
public static class MinorSkillWorldBuilder
{
    const string Folder = "Assets/Prefabs/MinorSkills";
    const string GroupName = "Minor Skill Placeholders";
    const float ChestScale = .75f;
    // 0.69m source key becomes 0.262m, about 14% longer than a ground card.
    const float KeyScale = .38f;
    static readonly Color[] PairColors = {
        new Color32(221, 164, 54, 255), new Color32(77, 172, 133, 255),
        new Color32(193, 92, 92, 255), new Color32(145, 118, 196, 255)
    };

    [MenuItem("TCG Card Chaos/Minor Skills/Create Missing World Placeholders")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before creating minor-skill placeholders.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "MainScene")
            throw new InvalidOperationException("Open MainScene before creating minor-skill placeholders.");
        EnsureFolder("Assets/Prefabs", "MinorSkills");
        EnsureFolder(Folder, "Materials");
        EnsureFolder(Folder, "Meshes");
        var report = new StringBuilder("Minor skill world placeholders\n");
        var roots = scene.GetRootGameObjects();
        GameObject group = Array.Find(roots, root => root.name == GroupName);
        if (group == null)
        {
            group = new GameObject(GroupName);
            Undo.RegisterCreatedObjectUndo(group, "Create minor-skill placeholders");
            SceneManager.MoveGameObjectToScene(group, scene);
        }
        Transform keys = EnsureGroup(group.transform, "Keys - move each key separately");
        Transform chests = EnsureGroup(group.transform, "Chests - move each chest separately");
        Material wood = MaterialAsset("ChestWood", new Color32(111, 76, 46, 255));
        Material trim = MaterialAsset("ChestTrim", new Color32(53, 46, 34, 255));
        Physics.SyncTransforms();
        int created = 0;
        for (int index = 0; index < 4; index++)
        {
            Material color = MaterialAsset("Pair" + (index + 1), PairColors[index]);
            GameObject chestPrefab = EnsureChestPrefab(index, wood, trim, color);
            GameObject keyPrefab = EnsureKeyPrefab(index, trim, color);
            string chestName = "Minor Chest " + (index + 1);
            string keyName = "Minor Key " + (index + 1);
            Transform chest = chests.Find(chestName);
            if (chest == null)
            {
                Vector3 desired = new Vector3(index * 1.55f - .2f, .06f, -4.65f);
                // Every chest has the same size. Find another position instead of shrinking it.
                bool placed = TryFindClearPosition(desired, new Vector3(.90f, .88f, .66f) * ChestScale,
                    .45f * ChestScale, false, out Vector3 position);
                if (!placed) throw new InvalidOperationException("No clear floor gap for " + chestName
                    + ". No existing card was moved. See Temp/minor-skill-placement-blockers.txt.");
                chest = Instantiate(chestPrefab, chests, chestName, position);
                chest.localScale = Vector3.one * ChestScale;
                PrefabUtility.RecordPrefabInstancePropertyModifications(chest);
                created++;
                Physics.SyncTransforms();
            }
            Transform key = keys.Find(keyName);
            if (key == null)
            {
                Vector3 desired = chest.position + new Vector3(0, 0, -1.1f);
                Vector3 position = FindClearPosition(desired, new Vector3(.78f, .18f, .35f) * KeyScale, .08f * KeyScale, true);
                key = Instantiate(keyPrefab, keys, keyName, position);
                created++;
                Physics.SyncTransforms();
            }
            report.AppendLine("Pair " + (index + 1) + ": key " + key.position.ToString("F3")
                + "; chest " + chest.position.ToString("F3") + "; chest scale " + chest.localScale.x.ToString("F2"));
        }
        AssetDatabase.SaveAssets();
        if (created > 0) EditorSceneManager.MarkSceneDirty(scene);
        Directory.CreateDirectory("Temp");
        report.AppendLine("Created " + created + " objects. Existing placements/prefabs were preserved.");
        report.AppendLine("Visual children may be replaced without changing IDs or interaction roots.");
        File.WriteAllText("Temp/minor-skill-world-build.txt", report.ToString());
        Debug.Log(report.ToString());
    }

    [MenuItem("TCG Card Chaos/Minor Skills/Validate World Placeholders")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Validate the authored scene outside Play mode.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "MainScene") throw new InvalidOperationException("Open MainScene first.");
        var keys = new List<WorldMinorSkillKey>();
        var chests = new List<WorldMinorSkillChest>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            keys.AddRange(root.GetComponentsInChildren<WorldMinorSkillKey>(true));
            chests.AddRange(root.GetComponentsInChildren<WorldMinorSkillChest>(true));
        }
        if (keys.Count != 4 || chests.Count != 4)
            throw new InvalidOperationException("Expected four keys and four chests; found " + keys.Count + "/" + chests.Count + ".");
        var report = new StringBuilder("Authored minor-skill scene validation\n");
        for (int i = 0; i < 4; i++)
        {
            if (keys.FindAll(key => key.SkillIndex == i).Count != 1 || chests.FindAll(chest => chest.SkillIndex == i).Count != 1)
                throw new InvalidOperationException("Missing or duplicate minor-skill pair " + (i + 1) + ".");
            WorldMinorSkillKey key = keys.Find(item => item.SkillIndex == i);
            WorldMinorSkillChest chest = chests.Find(item => item.SkillIndex == i);
            foreach (Component item in new Component[] { key, chest })
            {
                BoxCollider collider = item.GetComponent<BoxCollider>();
                if (collider == null || !collider.enabled || collider.isTrigger || item.GetComponent<InteractionBlocker>() == null)
                    throw new InvalidOperationException(item.name + " needs its solid interaction collider and blocker.");
                if (item.GetComponentInChildren<Rigidbody>(true) != null || item.GetComponentInChildren<Animator>(true) != null)
                    throw new InvalidOperationException(item.name + " unexpectedly has simulation/Animator overhead.");
                if (item.transform.Find("Visual") == null)
                    throw new InvalidOperationException(item.name + " is missing its replaceable Visual child.");
                if (PrefabUtility.GetCorrespondingObjectFromSource(item.gameObject) == null)
                    throw new InvalidOperationException(item.name + " should remain an authored prefab instance.");
            }
            if (chest.ModelView == null && (chest.Lid == null || Quaternion.Angle(chest.Lid.localRotation, Quaternion.identity) > .01f))
                throw new InvalidOperationException(chest.name + " lid must be assigned and authored closed.");
            var chestData = new SerializedObject(chest);
            float angle = chestData.FindProperty("openAngle").floatValue;
            if (angle <= 0 || angle > 120)
                throw new InvalidOperationException(chest.name + " lid must rotate upward around its rear hinge.");
            report.AppendLine("Pair " + (i + 1) + ": unique IDs, replaceable Visual, solid blockers, closed rear-hinged lid; key "
                + key.transform.position.ToString("F3") + ", chest " + chest.transform.position.ToString("F3"));
        }
        report.AppendLine("PASS: four authored pairs; no per-object Rigidbody or Animator; no scene/save mutation.");
        Directory.CreateDirectory("Temp");
        File.WriteAllText("Temp/minor-skill-world-validation.txt", report.ToString());
        Debug.Log(report.ToString());
    }

    static Transform Instantiate(GameObject prefab, Transform parent, string name, Vector3 position)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(instance, "Place " + name);
        instance.name = name;
        instance.transform.position = position;
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
        return instance.transform;
    }

    static GameObject EnsureKeyPrefab(int index, Material trim, Material color)
    {
        string path = Folder + "/MinorKey" + (index + 1) + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        var root = new GameObject("Minor Key " + (index + 1));
        root.transform.localScale = Vector3.one * KeyScale;
        try
        {
            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var shape = new Boxes();
            // A flat rectangular bow, shaft and two teeth, all made from cubes.
            shape.Add(new Vector3(-.22f, .055f, -.105f), new Vector3(.24f, .065f, .045f));
            shape.Add(new Vector3(-.22f, .055f, .105f), new Vector3(.24f, .065f, .045f));
            shape.Add(new Vector3(-.32f, .055f, 0), new Vector3(.045f, .065f, .21f));
            shape.Add(new Vector3(-.12f, .055f, 0), new Vector3(.045f, .065f, .21f));
            shape.Add(new Vector3(.105f, .055f, 0), new Vector3(.45f, .065f, .065f));
            shape.Add(new Vector3(.22f, .055f, -.065f), new Vector3(.055f, .065f, .115f));
            shape.Add(new Vector3(.32f, .055f, -.065f), new Vector3(.055f, .065f, .115f));
            MeshObject(visual, "KeyShape", MeshAsset("KeyShape", shape), color);
            var tally = new Boxes();
            for (int n = 0; n <= index; n++)
                tally.Add(new Vector3(-.28f + n * .04f, .089f, .105f), new Vector3(.014f, .004f, .031f));
            MeshObject(visual, "PairMarks", MeshAsset("KeyMarks" + (index + 1), tally), trim);
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(.005f, .075f, 0);
            collider.size = new Vector3(.73f, .15f, .30f);
            root.AddComponent<InteractionBlocker>();
            root.AddComponent<WorldMinorSkillKey>().Configure(index, visual);
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }

    static GameObject EnsureChestPrefab(int index, Material wood, Material trim, Material color)
    {
        string path = Folder + "/MinorChest" + (index + 1) + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        var root = new GameObject("Minor Chest " + (index + 1));
        root.transform.localScale = Vector3.one * ChestScale;
        try
        {
            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var body = new Boxes();
            body.Add(new Vector3(0, .065f, 0), new Vector3(.82f, .13f, .58f));
            body.Add(new Vector3(-.37f, .32f, 0), new Vector3(.08f, .51f, .58f));
            body.Add(new Vector3(.37f, .32f, 0), new Vector3(.08f, .51f, .58f));
            body.Add(new Vector3(0, .32f, -.25f), new Vector3(.66f, .51f, .08f));
            body.Add(new Vector3(0, .32f, .25f), new Vector3(.66f, .51f, .08f));
            MeshObject(visual, "Body", MeshAsset("ChestBody", body), wood);
            var bands = new Boxes();
            bands.Add(new Vector3(0, .10f, 0), new Vector3(.835f, .045f, .595f));
            bands.Add(new Vector3(0, .555f, -.276f), new Vector3(.835f, .045f, .044f));
            bands.Add(new Vector3(0, .555f, .276f), new Vector3(.835f, .045f, .044f));
            bands.Add(new Vector3(-.395f, .555f, 0), new Vector3(.044f, .045f, .55f));
            bands.Add(new Vector3(.395f, .555f, 0), new Vector3(.044f, .045f, .55f));
            MeshObject(visual, "BodyBands", MeshAsset("ChestBands", bands), trim);
            var lockShape = new Boxes();
            lockShape.Add(new Vector3(0, .445f, -.305f), new Vector3(.24f, .22f, .035f));
            MeshObject(visual, "MatchingLock", MeshAsset("ChestLock", lockShape), color);
            var marks = new Boxes();
            for (int n = 0; n <= index; n++)
                marks.Add(new Vector3((n - index * .5f) * .043f, .445f, -.325f), new Vector3(.017f, .115f, .009f));
            MeshObject(visual, "PairMarks", MeshAsset("ChestMarks" + (index + 1), marks), trim);
            Transform lid = new GameObject("LidPivot").transform;
            lid.SetParent(visual, false);
            lid.localPosition = new Vector3(0, .60f, .29f);
            var cover = new Boxes();
            cover.Add(new Vector3(0, .018f, -.29f), new Vector3(.86f, .09f, .62f));
            MeshObject(lid, "Lid", MeshAsset("ChestLid", cover), wood);
            var lidTrim = new Boxes();
            lidTrim.Add(new Vector3(-.28f, .069f, -.29f), new Vector3(.055f, .013f, .625f));
            lidTrim.Add(new Vector3(.28f, .069f, -.29f), new Vector3(.055f, .013f, .625f));
            MeshObject(lid, "LidBands", MeshAsset("ChestLidBands", lidTrim), color);
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, .315f, 0);
            collider.size = new Vector3(.86f, .63f, .62f);
            root.AddComponent<InteractionBlocker>();
            root.AddComponent<WorldMinorSkillChest>().Configure(index, lid);
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }

    // Edit-time only. Chests need empty floor; a key may rest on a thin loose card.
    static Vector3 FindClearPosition(Vector3 desired, Vector3 size, float centerY, bool allowFlatCardSupport = false)
    {
        if (TryFindClearPosition(desired, size, centerY, allowFlatCardSupport, out Vector3 point)) return point;
        throw new InvalidOperationException("No clear ground position near " + desired
            + ". Existing objects were not moved. See Temp/minor-skill-placement-blockers.txt.");
    }

    static bool TryFindClearPosition(Vector3 desired, Vector3 size, float centerY, bool allowFlatCardSupport, out Vector3 result)
    {
        var blockers = new Dictionary<string, int>();
        int tested = 0, missingFloor = 0;
        float step = !allowFlatCardSupport && size.x <= .46f ? .10f : .20f;
        int rings = Mathf.CeilToInt(9f / step);
        for (int ring = 0; ring <= rings; ring++)
        for (int x = -ring; x <= ring; x++)
        for (int z = -ring; z <= ring; z++)
        {
            if (ring > 0 && Mathf.Abs(x) != ring && Mathf.Abs(z) != ring) continue;
            Vector3 point = desired + new Vector3(x * step, 0, z * step);
            if (point.x < -2f || point.x > 6.0f || point.z < -9.0f || point.z > 4f) continue;
            tested++;
            RaycastHit[] floors = Physics.RaycastAll(new Vector3(point.x, 1.4f, point.z), Vector3.down,
                1.8f, ~0, QueryTriggerInteraction.Ignore);
            bool foundFloor = false;
            float floorY = float.NegativeInfinity;
            foreach (RaycastHit hit in floors)
                if (IsFloor(hit.collider) && hit.normal.y > .8f && hit.point.y > floorY)
                { foundFloor = true; floorY = hit.point.y; }
            if (!foundFloor) { missingFloor++; continue; }
            float supportY = floorY;
            // The key's cube mesh begins at local Y=.0225; place its underside on the support.
            point.y = allowFlatCardSupport ? supportY + .003f - .0225f : supportY + .004f;
            bool blocked = false, stable = false;
            for (int pass = 0; pass < 3 && !blocked && !stable; pass++)
            {
                Collider[] overlaps = Physics.OverlapBox(point + Vector3.up * centerY, size * .5f,
                    Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                float nextSupport = supportY;
                foreach (Collider other in overlaps)
                {
                    if (IsFloor(other)) continue;
                    if (allowFlatCardSupport && TryGetFlatCardSurface(other, floorY, out float cardTop))
                    { nextSupport = Mathf.Max(nextSupport, cardTop); continue; }
                    blocked = true;
                    string blocker = other.GetComponentInParent<WorldCard>() != null ? "Loose card: " + other.name
                        : other.GetComponentInParent<WorldBoosterPack>() != null ? "Booster pack: " + other.name
                        : other.transform.root.name + "/" + other.name;
                    blockers.TryGetValue(blocker, out int count);
                    blockers[blocker] = count + 1;
                    break;
                }
                stable = Mathf.Abs(nextSupport - supportY) <= .0001f;
                if (!stable)
                {
                    supportY = nextSupport;
                    point.y = supportY + .003f - .0225f;
                }
            }
            if (!blocked && stable) { result = point; return true; }
        }
        var ranked = new List<KeyValuePair<string, int>>(blockers);
        ranked.Sort((a, b) => b.Value.CompareTo(a.Value));
        var report = new StringBuilder("No placement for footprint " + size + " near " + desired
            + "; candidates=" + tested + "; missing floor=" + missingFloor + "\n");
        for (int i = 0; i < Mathf.Min(20, ranked.Count); i++)
            report.AppendLine(ranked[i].Value + " candidates blocked by " + ranked[i].Key);
        Directory.CreateDirectory("Temp");
        File.AppendAllText("Temp/minor-skill-placement-blockers.txt", report.ToString());
        result = default;
        return false;
    }

    static bool TryGetFlatCardSurface(Collider collider, float floorY, out float top)
    {
        top = floorY;
        WorldCard card = collider.GetComponentInParent<WorldCard>();
        if (card == null || card.UsesPsaSlab || card.GetComponentInParent<CardShelfSlot>() != null)
            return false;
        bool found = false;
        Bounds bounds = default;
        foreach (Renderer renderer in card.GetComponentsInChildren<Renderer>(false))
        {
            if (!renderer.enabled) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (!found) bounds = collider.bounds;
        // Don't balance a placeholder on a leaning card, slab, pack or high pile.
        if (bounds.size.y > .08f || bounds.max.y > floorY + .14f || bounds.min.y < floorY - .04f)
            return false;
        top = Mathf.Max(floorY, bounds.max.y);
        return true;
    }

    static bool IsFloor(Collider collider)
    {
        for (Transform node = collider.transform; node != null; node = node.parent)
            if (node.name == "Floor" || node.name.StartsWith("Floor_Var", StringComparison.Ordinal)) return true;
        return false;
    }

    static Transform EnsureGroup(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;
        child = new GameObject(name).transform;
        child.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(child.gameObject, "Create minor-skill group");
        return child;
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    static Material MaterialAsset(string name, Color color)
    {
        string path = Folder + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("The URP Lit shader is unavailable.");
        material = new Material(shader) { name = name, enableInstancing = true };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", .15f);
        material.SetFloat("_Metallic", 0f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static Mesh MeshAsset(string name, Boxes boxes)
    {
        string path = Folder + "/Meshes/" + name + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        mesh = boxes.Build(name);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static void MeshObject(Transform parent, string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    sealed class Boxes
    {
        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<int> _triangles = new List<int>();
        public void Add(Vector3 center, Vector3 size)
        {
            Vector3 r = Vector3.right * size.x * .5f, u = Vector3.up * size.y * .5f,
                f = Vector3.forward * size.z * .5f;
            Face(center + r, f, u); Face(center - r, -f, u);
            Face(center + u, r, f); Face(center - u, r, -f);
            Face(center + f, -r, u); Face(center - f, r, u);
        }
        void Face(Vector3 c, Vector3 a, Vector3 b)
        {
            int i = _vertices.Count;
            _vertices.Add(c - a - b); _vertices.Add(c + a - b);
            _vertices.Add(c + a + b); _vertices.Add(c - a + b);
            _triangles.Add(i); _triangles.Add(i + 2); _triangles.Add(i + 1);
            _triangles.Add(i); _triangles.Add(i + 3); _triangles.Add(i + 2);
        }
        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(_vertices); mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
