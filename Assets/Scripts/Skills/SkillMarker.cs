using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Temporary visual only. No collider, material instance or per-frame callback.</summary>
public sealed class SkillMarker : MonoBehaviour
{
    static Mesh _insightBeamMesh;
    static int _beamUsers;
    Mesh _ownedBeamMesh;

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
        if (_ownedBeamMesh == null || _ownedBeamMesh != _insightBeamMesh) return;
        _beamUsers--;
        if (_beamUsers > 0) return;
        Object.Destroy(_insightBeamMesh);
        _insightBeamMesh = null;
        _beamUsers = 0;
    }
    public static SkillMarker ForShelf(CardShelf shelf)
    {
        var bounds = new Bounds(Vector3.zero, Vector3.one * 0.1f);
        bool found = false;
        foreach (Renderer renderer in shelf.GetComponentsInChildren<Renderer>())
        {
            if (renderer is LineRenderer || !renderer.enabled || renderer.GetComponentInParent<SkillMarker>() != null) continue;
            Bounds world = renderer.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 point = shelf.transform.InverseTransformPoint(world.center + Vector3.Scale(world.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; } else bounds.Encapsulate(point);
            }
        }
        return Create(shelf.transform, bounds, 0.025f);
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
