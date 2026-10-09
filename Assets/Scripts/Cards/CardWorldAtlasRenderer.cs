using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shares far-away ground-card textures so Unity can instance their existing meshes.
/// Does not replace card objects, geometry, physics, picking, outlines or saved state.
/// </summary>
[DefaultExecutionOrder(1000)]
public sealed class CardWorldAtlasRenderer : MonoBehaviour
{
    sealed class Tracked
    {
        public WorldCard card;
        public MeshRenderer renderer;
        public Transform transform;
        public Material detail, atlas;
        public Vector4 rect;
        public bool usingAtlas, shelf;
        public int index;
    }

    const int UpdatesPerFrame = 512;
    static readonly int RectId = Shader.PropertyToID("_CardAtlasRect");
    static CardWorldAtlasRenderer _instance;
    static bool _unavailable;
    CardWorldAtlas _catalog;
    Camera _camera;
    readonly Dictionary<WorldCard, Tracked> _byCard = new(6000);
    readonly List<Tracked> _cards = new(6000);
    readonly Dictionary<(int, Texture, Color, Vector2, Vector2, float), Material> _materials = new();
    MaterialPropertyBlock _block;
    int _cursor;

    void Awake() => _block = new MaterialPropertyBlock();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _instance = null; _unavailable = false; }

    public static void Track(WorldCard card, MeshRenderer renderer, Material detail)
    {
        if (!Application.isPlaying || _unavailable || !SystemInfo.supportsInstancing) return;
        if (card.Definition == null) { Untrack(card); return; }
        if (!_instance)
        {
            var catalog = Resources.Load<CardWorldAtlas>("Cards/WorldAtlas/Catalog");
            if (!catalog) { _unavailable = true; return; }
            _instance = new GameObject("Distant card rendering").AddComponent<CardWorldAtlasRenderer>();
            _instance._catalog = catalog;
        }
        _instance.Register(card, renderer, detail);
    }

    void Register(WorldCard card, MeshRenderer renderer, Material detail)
    {
        if (!_catalog.TryGet(card.Definition.DefinitionId, out var item)) { Untrack(card); return; }
        if (!_byCard.TryGetValue(card, out var entry))
        {
            entry = new Tracked { card = card, index = _cards.Count };
            _byCard.Add(card, entry); _cards.Add(entry);
        }
        // WorldCard just assigned its authoritative detail material. Remove only our UV override.
        if (entry.usingAtlas && entry.renderer) entry.renderer.SetPropertyBlock(null);
        entry.usingAtlas = false;
        entry.renderer = renderer; entry.transform = renderer.transform; entry.detail = detail; entry.rect = item.uvRect;
        entry.shelf = card.GetComponentInParent<CardShelfSlot>() != null || card.GetComponentInParent<PsaCabinetSlot>() != null;
        var back = detail.GetTexture("_CardBackMap");
        var tint = detail.GetColor("_BaseColor");
        var scale = detail.GetTextureScale("_BaseMap");
        var offset = detail.GetTextureOffset("_BaseMap");
        // Custom tiling cannot repeat outside an atlas tile. Keep such cards on their authored material.
        if (scale != Vector2.one || offset != Vector2.zero) { Untrack(card); return; }
        float cull = detail.GetFloat("_Cull");
        var key = (item.page, back, tint, scale, offset, cull);
        if (!_materials.TryGetValue(key, out var material))
        {
            material = new Material(_catalog.pages[item.page]) { name = "World card atlas " + item.page, enableInstancing = true };
            material.SetTexture("_CardBackMap", back);
            material.SetColor("_BaseColor", tint);
            material.SetTextureScale("_BaseMap", scale);
            material.SetTextureOffset("_BaseMap", offset);
            material.SetFloat("_Cull", cull);
            _materials.Add(key, material);
        }
        entry.atlas = material;
        if (!_camera) _camera = Camera.main;
        if (_camera && isActiveAndEnabled) Refresh(entry, _camera.transform.position, DetailDistance());
    }

    float DetailDistance()
    {
        // Zoom must reveal original art before its pixels become large on screen.
        return 5f * Mathf.Max(1f, Mathf.Tan(GameSettings.Fov * Mathf.Deg2Rad * .5f)
            / Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * .5f));
    }

    void LateUpdate()
    {
        if (!_camera) _camera = Camera.main;
        if (!_camera || !CardInstancedRenderManager.IsGameplayReady) return;
        Vector3 position = _camera.transform.position;
        float distance = DetailDistance();
        int count = Mathf.Min(UpdatesPerFrame, _cards.Count);
        for (int i = 0; i < count; i++)
        {
            if (_cursor >= _cards.Count) _cursor = 0;
            Refresh(_cards[_cursor++], position, distance);
        }
    }

    void Refresh(Tracked entry, Vector3 cameraPosition, float detailDistance)
    {
        if (!entry.card || !entry.renderer) return;
        float threshold = detailDistance + (entry.usingAtlas ? 0f : 1f);
        bool useAtlas = !entry.shelf && entry.card.CanUseWorldAtlas
            && (entry.transform.position - cameraPosition).sqrMagnitude > threshold * threshold;
        if (useAtlas == entry.usingAtlas) return;
        entry.usingAtlas = useAtlas;
        entry.renderer.sharedMaterial = useAtlas ? entry.atlas : entry.detail;
        if (useAtlas)
        {
            _block.Clear(); _block.SetVector(RectId, entry.rect);
            entry.renderer.SetPropertyBlock(_block);
        }
        else entry.renderer.SetPropertyBlock(null);
    }

    public static void Untrack(WorldCard card)
    {
        if (!_instance || !_instance._byCard.TryGetValue(card, out var entry)) return;
        if (entry.usingAtlas && entry.renderer)
        {
            if (entry.renderer.sharedMaterial == entry.atlas)
                entry.renderer.sharedMaterial = entry.detail;
            entry.renderer.SetPropertyBlock(null);
        }
        int last = _instance._cards.Count - 1;
        var moved = _instance._cards[last];
        _instance._cards[entry.index] = moved; moved.index = entry.index;
        _instance._cards.RemoveAt(last); _instance._byCard.Remove(card);
    }

    void OnDisable()
    {
        foreach (var entry in _cards)
            if (entry.usingAtlas && entry.renderer) { entry.renderer.sharedMaterial = entry.detail; entry.renderer.SetPropertyBlock(null); entry.usingAtlas = false; }
    }

    void OnDestroy()
    {
        foreach (var material in _materials.Values) if (material) Destroy(material);
        if (_instance == this) _instance = null;
    }
}
