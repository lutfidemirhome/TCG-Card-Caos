using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Temporary visuals only. No collider or per-frame scene search.</summary>
public sealed class SkillMarker : MonoBehaviour
{
    static Mesh _insightBeamMesh;
    static int _beamUsers;
    Mesh _ownedBeamMesh;
    Material _cabinetMask, _cabinetFill;
    readonly Dictionary<Mesh, Mesh> _outlineMeshes = new Dictionary<Mesh, Mesh>();
    readonly List<Mesh> _ownedOutlineMeshes = new List<Mesh>(2);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetBeamMesh()
    {
        if (_insightBeamMesh != null) Object.Destroy(_insightBeamMesh);
        _insightBeamMesh = null;
        _beamUsers = 0;
    }

    public static SkillMarker ForCard(WorldCard card)
    {
        BoxCollider box = card.GetComponent<BoxCollider>();
        Bounds bounds = box != null ? new Bounds(box.center, box.size)
            : new Bounds(Vector3.zero, new Vector3(CardDimensions.Width, CardDimensions.Thickness, CardDimensions.Height));
        SkillMarker marker = Create(card.transform, bounds, 0.012f);
        marker.AddInsightBeam(bounds.center);
        return marker;
    }

    // One shared quad/material. Upward motion and sparkles run entirely in the shader.
    void AddInsightBeam(Vector3 center)
    {
        Material material = Resources.Load<Material>("UI/Skills/InsightBeam");
        if (material == null) return;
        if (_insightBeamMesh == null)
        {
            _insightBeamMesh = new Mesh { name = "Insight Beam Quad", hideFlags = HideFlags.HideAndDontSave };
            _insightBeamMesh.vertices = new[] {
                new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f),
                new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f)
            };
            _insightBeamMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            _insightBeamMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _insightBeamMesh.UploadMeshData(true);
        }

        var obj = new GameObject("InsightBeam", typeof(MeshFilter), typeof(MeshRenderer));
        obj.layer = 2;
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = center;
        obj.GetComponent<MeshFilter>().sharedMesh = _insightBeamMesh;
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        // The shader faces the camera and stays world-up regardless of the card's rotation.
        // Conservative local bounds cover that world-space geometry at any card scale.
        Vector3 scale = obj.transform.lossyScale;
        float smallestScale = Mathf.Max(0.001f, Mathf.Min(Mathf.Abs(scale.x), Mathf.Min(Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
        renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * (3f / smallestScale));
        _ownedBeamMesh = _insightBeamMesh;
        _beamUsers++;
    }

    void OnDestroy()
    {
        if (_cabinetMask != null) Object.Destroy(_cabinetMask);
        if (_cabinetFill != null) Object.Destroy(_cabinetFill);
        foreach (Mesh mesh in _ownedOutlineMeshes)
            if (mesh != null) Object.Destroy(mesh);
        _ownedOutlineMeshes.Clear();
        _outlineMeshes.Clear();
        if (_ownedBeamMesh == null || _ownedBeamMesh != _insightBeamMesh) return;
        _beamUsers--;
        if (_beamUsers > 0) return;
        Object.Destroy(_insightBeamMesh);
        _insightBeamMesh = null;
        _beamUsers = 0;
    }
    public static SkillMarker ForShelf(CardShelf shelf)
    {
        return CreateCabinetOutline(shelf.transform, false);
    }

    public static SkillMarker ForPsaCabinet(PsaCabinet cabinet)
    {
        return CreateCabinetOutline(cabinet.transform, true);
    }

    static SkillMarker CreateCabinetOutline(Transform cabinet, bool psa)
    {
        Material mask = Resources.Load<Material>("Materials/OutlineMask");
        Material fill = Resources.Load<Material>("Materials/OutlineFill");
        if (mask == null || fill == null) return null;

        // Use the actual exterior mesh, not a wire box. Separate render-only children
        // leave cabinet materials, card hover outlines and shared source meshes untouched.
        var obj = new GameObject("SkillCabinetOutline");
        obj.layer = 2;
        obj.transform.SetParent(cabinet, false);
        SkillMarker marker = obj.AddComponent<SkillMarker>();
        marker._cabinetMask = new Material(mask) { hideFlags = HideFlags.HideAndDontSave };
        marker._cabinetFill = new Material(fill) { hideFlags = HideFlags.HideAndDontSave };
        marker._cabinetMask.SetFloat("_ZTest", (float)CompareFunction.Always);
        // Match OutlineVisible: keep the silhouette mask, but let scene depth hide occluded edges.
        marker._cabinetFill.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
        marker._cabinetFill.SetColor("_OutlineColor", CardOutlineSettings.GetPaletteOrDefaults().shelfCorrect);
        marker._cabinetFill.SetFloat("_OutlineWidth", 6f);

        var copies = new Dictionary<Transform, Transform> { { cabinet, obj.transform } };
        foreach (MeshRenderer renderer in cabinet.GetComponentsInChildren<MeshRenderer>())
        {
            if (!renderer.enabled || renderer.GetComponentInParent<SkillMarker>() != null
                || renderer.GetComponentInParent<WorldCard>() != null) continue;
            if (!IsCabinetExterior(renderer, cabinet, psa)) continue;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            Transform parent = CopyVisualTransform(renderer.transform, copies);
            Mesh mesh = marker.GetOutlineMesh(filter.sharedMesh, psa);
            AddOutlinePass(parent, mesh, marker._cabinetMask, "OutlineMask");
            AddOutlinePass(parent, mesh, marker._cabinetFill, "OutlineFill");
        }
        return marker;
    }

    Mesh GetOutlineMesh(Mesh source, bool psa)
    {
        if (_outlineMeshes.TryGetValue(source, out Mesh mesh)) return mesh;
        // These small render-only meshes are baked in the editor, so imported cabinet
        // meshes can keep Read/Write disabled in builds. Never destroy the shared bake.
        Mesh baked = Resources.Load<Mesh>(psa ? "UI/Skills/PsaCabinetOutline" : "UI/Skills/CabinetOutline");
        // The baked asset uses its filename; identify the original exterior separately.
        string expectedSourceName = psa ? "Counter_attlsv" : "Shelf_kicg9f";
        if (baked != null && source.name == expectedSourceName && baked.vertexCount == source.vertexCount
            && baked.bounds.center == source.bounds.center && baked.bounds.size == source.bounds.size)
        {
            _outlineMeshes.Add(source, baked);
            return baked;
        }
        if (!source.isReadable)
        {
            _outlineMeshes.Add(source, source);
            return source;
        }
        mesh = new Mesh { name = source.name + " Skill Outline", hideFlags = HideFlags.HideAndDontSave,
            indexFormat = source.indexFormat };
        mesh.vertices = source.vertices;
        mesh.normals = source.normals;
        mesh.triangles = source.triangles;
        if (mesh.normals.Length != mesh.vertexCount) mesh.RecalculateNormals();
        SetSmoothOutlineNormals(mesh);
        mesh.bounds = source.bounds;
        mesh.UploadMeshData(true);
        _outlineMeshes.Add(source, mesh);
        _ownedOutlineMeshes.Add(mesh);
        return mesh;
    }

    // Match QuickOutline's coincident-vertex smoothing, on an owned mesh only.
    public static void SetSmoothOutlineNormals(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        var sums = new Dictionary<Vector3, Vector3>(vertices.Length);
        for (int i = 0; i < vertices.Length; i++)
        {
            sums.TryGetValue(vertices[i], out Vector3 sum);
            sums[vertices[i]] = sum + normals[i];
        }
        var smooth = new List<Vector3>(vertices.Length);
        for (int i = 0; i < vertices.Length; i++) smooth.Add(sums[vertices[i]].normalized);
        mesh.SetUVs(3, smooth);
    }

    static bool IsCabinetExterior(MeshRenderer renderer, Transform cabinet, bool psa)
    {
        for (Transform part = renderer.transform; part != null && part != cabinet; part = part.parent)
        {
            // PSA holder seats and their card labels must not be outlined individually.
            if (psa && (part.name == "Counter_attlsv" || part.name == "Table_Visual")) return true;
            if (!psa && (part.name == "Shelf_kicg9f" || part.name == "BottomKickPlate"))
            {
                // Shelf boards are children of the body in some cabinet prefabs.
                for (Transform child = renderer.transform; child != part; child = child.parent)
                    if (child.name.StartsWith("Shelf_kicg9f_shelf", System.StringComparison.Ordinal)) return false;
                return renderer.GetComponentInParent<CardShelfSlot>() == null;
            }
        }
        return false;
    }

    static Transform CopyVisualTransform(Transform source, Dictionary<Transform, Transform> copies)
    {
        if (copies.TryGetValue(source, out Transform copy)) return copy;
        Transform parent = CopyVisualTransform(source.parent, copies);
        copy = new GameObject(source.name).transform;
        copy.gameObject.layer = 2;
        copy.SetParent(parent, false);
        copy.localPosition = source.localPosition;
        copy.localRotation = source.localRotation;
        copy.localScale = source.localScale;
        copies.Add(source, copy);
        return copy;
    }

    static void AddOutlinePass(Transform parent, Mesh mesh, Material material, string name)
    {
        var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        obj.layer = 2;
        obj.transform.SetParent(parent, false);
        obj.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        var materials = new Material[mesh.subMeshCount];
        for (int i = 0; i < materials.Length; i++) materials[i] = material;
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }
    static SkillMarker Create(Transform parent, Bounds bounds, float width)
    {
        var obj = new GameObject("SkillMarker");
        obj.layer = 2;
        obj.transform.SetParent(parent, false);
        var marker = obj.AddComponent<SkillMarker>();
        var line = obj.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.sharedMaterial = Resources.Load<Material>("UI/Skills/SkillMarker");
        line.widthMultiplier = width;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = LightProbeUsage.Off;
        line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        bounds.Expand(0.006f);
        Vector3 a = bounds.min, b = bounds.max;
        line.positionCount = 16;
        line.SetPositions(new[] {
            new Vector3(a.x,a.y,a.z), new Vector3(b.x,a.y,a.z), new Vector3(b.x,a.y,b.z), new Vector3(a.x,a.y,b.z),
            new Vector3(a.x,a.y,a.z), new Vector3(a.x,b.y,a.z), new Vector3(b.x,b.y,a.z), new Vector3(b.x,a.y,a.z),
            new Vector3(b.x,b.y,a.z), new Vector3(b.x,b.y,b.z), new Vector3(b.x,a.y,b.z), new Vector3(b.x,b.y,b.z),
            new Vector3(a.x,b.y,b.z), new Vector3(a.x,a.y,b.z), new Vector3(a.x,b.y,b.z), new Vector3(a.x,b.y,a.z)
        });
        return marker;
    }
}
