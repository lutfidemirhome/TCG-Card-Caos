using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>A brief render-only celebration. Cabinet/slot/card transforms and colliders never move.</summary>
[DefaultExecutionOrder(120)]
public sealed class CabinetCompletionEffect : MonoBehaviour
{
    const int Capacity = 4;
    const float Duration = 0.62f;
    static readonly List<CabinetCompletionEffect> Pool = new List<CabinetCompletionEffect>(Capacity);
    static readonly int ProgressId = Shader.PropertyToID("_Progress");
    static readonly int TextureId = Shader.PropertyToID("_MainTex");
    static readonly int TextureSTId = Shader.PropertyToID("_MainTex_ST");
    static readonly int ColorId = Shader.PropertyToID("_SourceColor");
    static readonly int CardBackTextureId = Shader.PropertyToID("_CardBackMap");
    static readonly int CardSinglePassId = Shader.PropertyToID("_CardSinglePass");
    readonly List<Part> _parts = new List<Part>(256);
    readonly List<MeshRenderer> _sources = new List<MeshRenderer>(256);
    readonly List<MeshRenderer> _suppressedOutlines = new List<MeshRenderer>(8);
    readonly List<WorldCard> _cards = new List<WorldCard>(100);
    readonly List<Transform> _cardParents = new List<Transform>(100);
    readonly Dictionary<Mesh, Mesh> _mirroredDrawMeshes = new Dictionary<Mesh, Mesh>();
    Component _owner;
    Material _gold;
    int _partCount;
    float _startedAt;
    float _scale = 1f;
    Vector3 _pivot;

    sealed class Part
    {
        public MeshRenderer Source;
        public Mesh Mesh;
        public Material[] Materials;
        public MaterialPropertyBlock[] Original;
        public MaterialPropertyBlock[] Gold;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Pool.Clear();

    public static bool IsActiveFor(Transform owner)
    {
        foreach (var effect in Pool)
            if (effect && effect.enabled && effect._owner && effect._owner.transform == owner) return true;
        return false;
    }

    public static void Cancel(Transform owner)
    {
        foreach (var effect in Pool)
            if (effect && effect._owner && effect._owner.transform == owner) effect.Stop();
    }

    public static void CancelForCard(WorldCard card)
    {
        if (!card) return;
        foreach (var effect in Pool)
            if (effect && effect._owner && card.transform.IsChildOf(effect._owner.transform)) effect.Stop();
    }

    public static bool Play(Component cabinet)
    {
        if (!cabinet || !cabinet.gameObject.activeInHierarchy || !cabinet.gameObject.scene.IsValid()) return false;
        if (IsActiveFor(cabinet.transform)) return true;
        CabinetCompletionEffect available = null;
        CabinetCompletionEffect oldest = null;
        for (int i = Pool.Count - 1; i >= 0; i--)
        {
            var effect = Pool[i];
            if (!effect) { Pool.RemoveAt(i); continue; }
            if (!effect.enabled) available = effect;
            if (!oldest || effect._startedAt < oldest._startedAt) oldest = effect;
        }
        if (!available && Pool.Count < Capacity)
        {
            Material material = Resources.Load<Material>("UI/Skills/CabinetCompletion");
            if (!material) return false;
            var root = new GameObject("Cabinet completion (Runtime)");
            SceneManager.MoveGameObjectToScene(root, cabinet.gameObject.scene);
            available = root.AddComponent<CabinetCompletionEffect>();
            available._gold = material;
            Pool.Add(available);
        }
        if (!available) available = oldest;
        return available && available.Begin(cabinet);
    }

    bool Begin(Component cabinet)
    {
        Stop();
        _owner = cabinet;
        cabinet.GetComponentsInChildren(false, _cards);
        foreach (WorldCard card in _cards)
        {
            // A whole-cabinet pop replaces any still-running row/arrival feedback.
            card.ClearShelfPlacementStatus();
            AssemblePickupTrail.CancelArrival(card);
            _cardParents.Add(card.transform.parent);
        }
        cabinet.GetComponentsInChildren(false, _sources);
        Bounds bounds = default;
        bool hasBounds = false;
        foreach (MeshRenderer source in _sources)
        {
            if (!source.enabled || source.forceRenderingOff) continue;
            if (source.GetComponentInParent<SkillMarker>()
                || source.name.IndexOf("Outline", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _suppressedOutlines.Add(source);
                source.forceRenderingOff = true;
                continue;
            }
            MeshFilter filter = source.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh) continue;
            Material[] materials = source.sharedMaterials;
            if (materials.Length == 0) continue;
            if (_partCount == _parts.Count) _parts.Add(new Part());
            Part part = _parts[_partCount++];
            part.Source = source;
            part.Mesh = GetDrawMesh(filter.sharedMesh, source.transform.localToWorldMatrix);
            part.Materials = materials;
            if (part.Original == null || part.Original.Length != materials.Length)
            {
                part.Original = new MaterialPropertyBlock[materials.Length];
                part.Gold = new MaterialPropertyBlock[materials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    part.Original[i] = new MaterialPropertyBlock();
                    part.Gold[i] = new MaterialPropertyBlock();
                }
            }
            for (int i = 0; i < materials.Length; i++)
            {
                source.GetPropertyBlock(part.Original[i], i);
                if (part.Original[i].isEmpty) source.GetPropertyBlock(part.Original[i]);
                Material material = materials[i];
                string tex = material && material.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                Texture texture = material && material.HasProperty(tex) ? material.GetTexture(tex) : null;
                Vector2 uvScale = material && material.HasProperty(tex) ? material.GetTextureScale(tex) : Vector2.one;
                Vector2 uvOffset = material && material.HasProperty(tex) ? material.GetTextureOffset(tex) : Vector2.zero;
                Color color = material && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
                    : material && material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                var properties = part.Gold[i];
                properties.Clear();
                properties.SetTexture(TextureId, texture ? texture : Texture2D.whiteTexture);
                properties.SetVector(TextureSTId, new Vector4(uvScale.x, uvScale.y, uvOffset.x, uvOffset.y));
                properties.SetColor(ColorId, color);
                // A combined card still has different art on its front and back/edges.
                // Carry its selector into the existing gold pass without extra draws.
                bool singlePassCard = material && material.IsKeywordEnabled("_CARD_SINGLE_PASS")
                    && material.HasProperty(CardBackTextureId);
                properties.SetFloat(CardSinglePassId, singlePassCard ? 1f : 0f);
                if (singlePassCard)
                {
                    Texture back = material.GetTexture(CardBackTextureId);
                    properties.SetTexture(CardBackTextureId, back ? back : Texture2D.whiteTexture);
                }
            }
            if (hasBounds) bounds.Encapsulate(source.bounds);
            else { bounds = source.bounds; hasBounds = true; }
            source.forceRenderingOff = true;
        }
        _sources.Clear();
        if (!hasBounds) { Stop(); return false; }
        _pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        _startedAt = Time.time;
        _scale = 1f;
        enabled = true;
        return true;
    }

    Mesh GetDrawMesh(Mesh source, Matrix4x4 matrix)
    {
        // Shelf art uses a negative X scale. MeshRenderer compensates its face culling,
        // but Graphics.DrawMesh does not; without this, the celebration shows the back.
        if (matrix.determinant >= 0f || !source.isReadable)
            return source;
        if (_mirroredDrawMeshes.TryGetValue(source, out Mesh mirrored) && mirrored)
            return mirrored;

        mirrored = Instantiate(source);
        mirrored.name = source.name + " (cabinet mirrored draw)";
        mirrored.hideFlags = HideFlags.DontSave;
        for (int submesh = 0; submesh < source.subMeshCount; submesh++)
        {
            if (source.GetTopology(submesh) != MeshTopology.Triangles) continue;
            int[] indices = source.GetIndices(submesh, applyBaseVertex: false);
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                int second = indices[i + 1];
                indices[i + 1] = indices[i + 2];
                indices[i + 2] = second;
            }
            mirrored.SetIndices(indices, MeshTopology.Triangles, submesh, calculateBounds: false,
                baseVertex: (int)source.GetBaseVertex(submesh));
        }
        // Only winding changes. Keep authored geometry, art UVs and shading normals.
        mirrored.UploadMeshData(true);
        _mirroredDrawMeshes[source] = mirrored;
        return mirrored;
    }

    void LateUpdate()
    {
        float elapsed = Time.time - _startedAt;
        if (!_owner || !_owner.gameObject.activeInHierarchy
            || (_owner is Behaviour behaviour && !behaviour.isActiveAndEnabled) || elapsed >= Duration)
        { Stop(); return; }
        for (int i = 0; i < _cards.Count; i++)
        {
            WorldCard card = _cards[i];
            if (!card || !card.isActiveAndEnabled || card.transform.parent != _cardParents[i]
                || card.IsInHand || card.IsFlyingToShelf || card.IsPackReveal || card.HasActivePhysics)
            { Stop(); return; }
        }
        float pop = elapsed <= 0.13f ? Mathf.SmoothStep(0f, 1f, elapsed / 0.13f)
            : 1f - Mathf.SmoothStep(0f, 1f, (elapsed - 0.13f) / 0.37f);
        _scale = Mathf.Lerp(1f, 1.15f, pop);
        Matrix4x4 group = Matrix4x4.TRS(_pivot * (1f - _scale), Quaternion.identity, Vector3.one * _scale);
        float progress = Mathf.Clamp01(elapsed / Duration);
        for (int p = 0; p < _partCount; p++)
        {
            Part part = _parts[p];
            MeshRenderer source = part.Source;
            if (!source || !source.enabled || !source.gameObject.activeInHierarchy) continue;
            Matrix4x4 matrix = group * source.transform.localToWorldMatrix;
            for (int i = 0; i < part.Materials.Length; i++)
            {
                if (!part.Materials[i]) continue;
                int submesh = Mathf.Min(i, part.Mesh.subMeshCount - 1);
                Graphics.DrawMesh(part.Mesh, matrix, part.Materials[i], source.gameObject.layer, null, submesh,
                    part.Original[i], ShadowCastingMode.Off, source.receiveShadows, source.probeAnchor, LightProbeUsage.Off);
                part.Gold[i].SetFloat(ProgressId, progress);
                Graphics.DrawMesh(part.Mesh, matrix, _gold, source.gameObject.layer, null, submesh,
                    part.Gold[i], ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
            }
        }
    }

    void Stop()
    {
        for (int i = 0; i < _partCount; i++)
        {
            Part part = _parts[i];
            if (part.Source) part.Source.forceRenderingOff = false;
            part.Source = null;
            part.Mesh = null;
            part.Materials = null;
            foreach (var block in part.Original) block.Clear();
            foreach (var block in part.Gold) block.Clear();
        }
        _partCount = 0;
        foreach (MeshRenderer outline in _suppressedOutlines)
            if (outline) outline.forceRenderingOff = false;
        _suppressedOutlines.Clear();
        _cards.Clear();
        _cardParents.Clear();
        _sources.Clear();
        _owner = null;
        _scale = 1f;
        enabled = false;
    }

    void OnDisable() => Stop();
    void OnDestroy()
    {
        Stop();
        Pool.Remove(this);
        foreach (Mesh mesh in _mirroredDrawMeshes.Values)
        {
            if (!mesh) continue;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
        _mirroredDrawMeshes.Clear();
    }
}
