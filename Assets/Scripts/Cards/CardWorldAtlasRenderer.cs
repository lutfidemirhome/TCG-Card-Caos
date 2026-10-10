using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Demo adaptation: batch distant front art and combine compatible front/back draws while
/// retaining the existing lit response, geometry, interaction rules and authored card poses.
/// </summary>
[DefaultExecutionOrder(1000)]
public sealed class CardWorldAtlasRenderer : MonoBehaviour
{
    public const string ShaderName = "TCG/Demo Card World Atlas Lit";
    sealed class Tracked
    {
        public WorldCard card;
        public MeshRenderer renderer;
        public Transform transform;
        public Material[] detail, atlas;
        public Vector4 rect;
        public bool usingAtlas, shelf;
        public int index;
    }

    const int UpdatesPerFrame = 512;
    static readonly int RectId = Shader.PropertyToID("_CardAtlasRect");
    static readonly string[] UnsupportedKeywords = {
        "_NORMALMAP", "_PARALLAXMAP", "_DETAIL_MULX2", "_DETAIL_SCALED", "_EMISSION",
        "_METALLICSPECGLOSSMAP", "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", "_OCCLUSIONMAP", "_ALPHATEST_ON"
    };
    static CardWorldAtlasRenderer _instance;
    static bool _unavailable;
    static readonly Dictionary<(Material, Material), Material> SinglePassMaterials = new();
    static Material _singlePassTemplate;

    internal static Material GetSinglePassMaterial(Material front, Material back)
    {
        if (!Supported(front) || !Supported(back)
            || front.GetTextureScale("_BaseMap") != Vector2.one || front.GetTextureOffset("_BaseMap") != Vector2.zero
            || front.shader.name != "Universal Render Pipeline/Lit" || back.shader != front.shader
            || back.GetTextureScale("_BaseMap") != CardArtLibrary.BackTextureUScale
            || back.GetTextureOffset("_BaseMap") != CardArtLibrary.BackTextureUOffset) return null;
        var key = (front, back);
        if (SinglePassMaterials.TryGetValue(key, out var material)) return material;
        // The two faces must retain the same lighting; custom/tinted backs stay on the original path.
        if (MaterialSignature(front) != MaterialSignature(back)) return null;
        if (!_singlePassTemplate) _singlePassTemplate = Resources.Load<Material>("Cards/CardSinglePassLit");
        if (!_singlePassTemplate) return null;
        material = new Material(front) { name = front.name + " Single Pass", shader = _singlePassTemplate.shader,
            hideFlags = HideFlags.HideAndDontSave, enableInstancing = true };
        material.EnableKeyword("_CARD_SINGLE_PASS");
        material.SetTexture("_CardBackMap", back.GetTexture("_BaseMap"));
        material.SetVector(RectId, new Vector4(1, 1, 0, 0));
        SinglePassMaterials.Add(key, material);
        return material;
    }
    CardWorldAtlas _catalog;
    Camera _camera;
    readonly Dictionary<WorldCard, Tracked> _byCard = new(6000);
    readonly List<Tracked> _cards = new(6000);
    readonly Dictionary<(int, string), Material> _materials = new();
    readonly Dictionary<Material, string> _signatures = new();
    MaterialPropertyBlock _block;
    int _cursor;

    void Awake() => _block = new MaterialPropertyBlock();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _instance = null; _unavailable = false; _singlePassTemplate = null;
        foreach (var material in SinglePassMaterials.Values) if (material) Destroy(material);
        SinglePassMaterials.Clear();
    }

    public static void Track(WorldCard card, MeshRenderer renderer, Material[] detail)
    {
        if (!Application.isPlaying || _unavailable || !SystemInfo.supportsInstancing) return;
        if (!card.Definition || detail.Length == 0 || !Supported(detail[0])
            || detail[0].GetTextureScale("_BaseMap") != Vector2.one || detail[0].GetTextureOffset("_BaseMap") != Vector2.zero) { Untrack(card); return; }
        if (!_instance)
        {
            var catalog = Resources.Load<CardWorldAtlas>("Cards/WorldAtlas/Catalog");
            if (!catalog) { _unavailable = true; return; }
            _instance = new GameObject("Distant card rendering").AddComponent<CardWorldAtlasRenderer>();
            _instance._catalog = catalog;
        }
        _instance.Register(card, renderer, detail);
    }

    static bool Supported(Material material)
    {
        // Arbitrary custom maps/alpha/tiling need their original sampling. Safely leave them alone.
        if (!material || (material.shader.name != "Universal Render Pipeline/Lit" && material.shader.name != ShaderName)
            || material.GetFloat("_Surface") != 0 || material.GetFloat("_AlphaClip") != 0) return false;
        foreach (string keyword in UnsupportedKeywords)
            if (material.IsKeywordEnabled(keyword)) return false;
        return true;
    }

    void Register(WorldCard card, MeshRenderer renderer, Material[] detail)
    {
        if (!_catalog.TryGet(card.Definition.DefinitionId, out var item)) { Untrack(card); return; }
        if (!_byCard.TryGetValue(card, out var entry))
        {
            // Never take ownership of an unrelated effect's property block.
            if (renderer.HasPropertyBlock()) return;
            entry = new Tracked { card = card, index = _cards.Count };
            _byCard.Add(card, entry); _cards.Add(entry);
        }
        // WorldCard has just assigned its authoritative original materials.
        if (entry.usingAtlas && entry.renderer) entry.renderer.SetPropertyBlock(null, 0);
        entry.usingAtlas = false;
        entry.renderer = renderer; entry.transform = renderer.transform;
        entry.detail = (Material[])detail.Clone(); entry.rect = item.uvRect;
        entry.shelf = card.GetComponentInParent<CardShelfSlot>() || card.GetComponentInParent<PsaCabinetSlot>();
        Material front = detail[0];
        if (!_signatures.TryGetValue(front, out string signature))
            _signatures.Add(front, signature = MaterialSignature(front));
        var key = (item.page, signature);
        if (!_materials.TryGetValue(key, out var material))
        {
            material = new Material(front) { name = "Demo world card atlas " + item.page,
                shader = _catalog.pages[item.page].shader, enableInstancing = true };
            material.SetTexture("_BaseMap", _catalog.pages[item.page].GetTexture("_BaseMap"));
            material.SetTexture("_MainTex", material.GetTexture("_BaseMap"));
            _materials.Add(key, material);
        }
        entry.atlas = (Material[])detail.Clone(); entry.atlas[0] = material;
        if (!_camera) _camera = Camera.main;
        if (_camera && isActiveAndEnabled) Refresh(entry, _camera.transform.position, DetailDistance());
    }

    static string MaterialSignature(Material material)
    {
        // Group only identical lighting/state; definition-specific front textures are the sole exception.
        var key = new StringBuilder();
        key.Append(material.renderQueue).Append('|').Append((int)material.globalIlluminationFlags);
        foreach (string keyword in material.shaderKeywords) key.Append('|').Append(keyword);
        Shader shader = material.shader;
        for (int i = 0; i < shader.GetPropertyCount(); i++)
        {
            string name = shader.GetPropertyName(i);
            if (name == "_BaseMap" || name == "_MainTex") continue;
            key.Append('|').Append(name).Append('=');
            switch (shader.GetPropertyType(i))
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    key.Append(material.GetFloat(name).ToString("R", CultureInfo.InvariantCulture)); break;
                case ShaderPropertyType.Int: key.Append(material.GetInteger(name)); break;
                case ShaderPropertyType.Color:
                case ShaderPropertyType.Vector:
                    Vector4 value = shader.GetPropertyType(i) == ShaderPropertyType.Color
                        ? (Vector4)material.GetColor(name) : material.GetVector(name);
                    for (int n = 0; n < 4; n++) key.Append(value[n].ToString("R", CultureInfo.InvariantCulture)).Append(',');
                    break;
                case ShaderPropertyType.Texture:
                    var texture = material.GetTexture(name); key.Append(texture ? texture.GetInstanceID() : 0); break;
            }
        }
        return key.ToString();
    }

    float DetailDistance() => 5f * Mathf.Max(1f, Mathf.Tan(GameSettings.Fov * Mathf.Deg2Rad * .5f)
        / Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * .5f));

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

    void Refresh(Tracked entry, Vector3 position, float detailDistance)
    {
        if (!entry.card || !entry.renderer) return;
        float threshold = detailDistance + (entry.usingAtlas ? 0f : 1f);
        bool useAtlas = !entry.shelf && entry.card.CanUseWorldAtlas
            && (entry.transform.position - position).sqrMagnitude > threshold * threshold;
        if (useAtlas == entry.usingAtlas) return;
        entry.usingAtlas = useAtlas;
        entry.renderer.sharedMaterials = useAtlas ? entry.atlas : entry.detail;
        if (useAtlas)
        {
            _block.Clear(); _block.SetVector(RectId, entry.rect);
            // Only front triangles use atlas UVs. Demo English/Japanese backs remain untouched.
            entry.renderer.SetPropertyBlock(_block, 0);
        }
        else entry.renderer.SetPropertyBlock(null, 0);
    }

    static void Restore(Tracked entry)
    {
        if (!entry.usingAtlas || !entry.renderer) return;
        if (entry.renderer.sharedMaterial == entry.atlas[0]) entry.renderer.sharedMaterials = entry.detail;
        entry.renderer.SetPropertyBlock(null, 0); entry.usingAtlas = false;
    }

    public static void Untrack(WorldCard card)
    {
        if (!_instance || !_instance._byCard.TryGetValue(card, out var entry)) return;
        Restore(entry);
        int last = _instance._cards.Count - 1;
        var moved = _instance._cards[last];
        _instance._cards[entry.index] = moved; moved.index = entry.index;
        _instance._cards.RemoveAt(last); _instance._byCard.Remove(card);
    }

    void OnDisable() { foreach (var entry in _cards) Restore(entry); }
    void OnDestroy()
    {
        foreach (var material in _materials.Values) if (material) Destroy(material);
        if (_instance == this) _instance = null;
    }
}
