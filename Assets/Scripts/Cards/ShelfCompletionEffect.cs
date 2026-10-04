using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Gold flash and visual pop for completed rows; saved card poses and shared materials stay unchanged.</summary>
[DefaultExecutionOrder(110)]
public sealed class ShelfCompletionEffect : MonoBehaviour
{
    const int Capacity = 64;
    const float Duration = 0.62f;
    const float PopPeakScale = 1.15f;
    const float PopRiseSeconds = 0.13f;
    const float PopEndSeconds = 0.50f;
    const float QuadExpansion = 1.32f;
    static ShelfCompletionEffect _instance;
    readonly List<Handle> _effects = new List<Handle>(Capacity);
    Material _material;
    Mesh _mesh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _instance = null;

    public static Handle Play(WorldCard card, Transform visual, Mesh mesh)
    {
        if (!card || !card.isActiveAndEnabled || !visual || !mesh || card.UsesPsaSlab
            || card.IsInHand || card.IsFlyingToShelf || card.IsPackReveal
            || !card.gameObject.scene.IsValid()) return null;

        if (_instance == null)
        {
            Material material = Resources.Load<Material>("Effects/CardFeedback/ShelfCompletion");
            if (!material) return null;
            var root = new GameObject("Shelf completion effects (Runtime)");
            SceneManager.MoveGameObjectToScene(root, card.gameObject.scene);
            _instance = root.AddComponent<ShelfCompletionEffect>();
            _instance._material = material;
            _instance._mesh = CreateQuad();
        }

        Handle available = null;
        Handle oldest = null;
        foreach (Handle effect in _instance._effects)
        {
            if (effect.IsActiveFor(card)) return effect;
            if (!effect.Active && available == null) available = effect;
            if (oldest == null || effect.StartedAt < oldest.StartedAt) oldest = effect;
        }
        if (available == null && _instance._effects.Count < Capacity)
        {
            available = new Handle(_instance.transform, _instance._mesh, _instance._material);
            _instance._effects.Add(available);
        }
        // An exceptionally large simultaneous batch may shorten the oldest visual only.
        if (available == null) available = oldest;
        available.Start(card, visual, mesh.bounds);
        _instance.enabled = true;
        return available;
    }

    public static void Cancel(WorldCard card)
    {
        if (_instance == null) return;
        foreach (Handle effect in _instance._effects)
            if (effect.IsActiveFor(card)) effect.Stop();
    }

    void LateUpdate()
    {
        bool active = false;
        foreach (Handle effect in _effects)
        {
            effect.Tick();
            active |= effect.Active;
        }
        enabled = active;
    }

    void OnDisable()
    {
        foreach (Handle effect in _effects) effect.Stop();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
        foreach (Handle effect in _effects) effect.Stop();
        if (_mesh) Destroy(_mesh);
    }

    static Mesh CreateQuad()
    {
        var mesh = new Mesh { name = "Shelf completion halo quad" };
        mesh.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f),
            new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);
        return mesh;
    }

    public sealed class Handle
    {
        static readonly int ProgressId = Shader.PropertyToID("_Progress");
        static readonly int TextureId = Shader.PropertyToID("_MainTex");
        static readonly int TextureSTId = Shader.PropertyToID("_MainTex_ST");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");
        readonly GameObject _root;
        readonly MeshRenderer _renderer;
        readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        WorldCard _card;
        Transform _visual;
        Transform _visualParent;
        Transform _cardParent;
        Vector3 _localPosition;
        Vector3 _localScale;
        Vector3 _restVisualPosition;
        Vector3 _restVisualScale;
        Vector3 _bottomPivotOffset;
        Vector3 _appliedVisualPosition;
        Vector3 _appliedVisualScale;
        public bool Active => _card != null && _root != null && _root.activeSelf;
        public float StartedAt { get; private set; }
        public bool IsActiveFor(WorldCard card) => ReferenceEquals(_card, card) && Active;

        internal Handle(Transform parent, Mesh mesh, Material material)
        {
            _root = new GameObject("Pooled shelf gold glow");
            _root.SetActive(false);
            _root.transform.SetParent(parent, false);
            _root.AddComponent<MeshFilter>().sharedMesh = mesh;
            _renderer = _root.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        internal void Start(WorldCard card, Transform visual, Bounds bounds)
        {
            Stop();
            _card = card;
            _visual = visual;
            _visualParent = visual.parent;
            _cardParent = card.transform.parent;
            _restVisualPosition = visual.localPosition;
            _restVisualScale = visual.localScale;
            _appliedVisualPosition = _restVisualPosition;
            _appliedVisualScale = _restVisualScale;
            // Grow the mesh about its bottom edge so the card still rests on its shelf.
            Vector3 bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            _bottomPivotOffset = visual.localRotation * Vector3.Scale(_restVisualScale, bottom);
            _localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z + 0.0012f);
            _localScale = new Vector3(bounds.size.x * QuadExpansion, bounds.size.y * QuadExpansion, 1f);
            StartedAt = Time.time;
            _root.layer = card.gameObject.layer;

            // Reuse the card's currently loaded art and UV transform; no texture loads or material clones.
            Renderer sourceRenderer = visual.GetComponent<Renderer>();
            Material source = sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
            string textureProperty = source != null && source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
            Texture texture = source != null && source.HasProperty(textureProperty) ? source.GetTexture(textureProperty) : null;
            Vector2 scale = source != null && source.HasProperty(textureProperty) ? source.GetTextureScale(textureProperty) : Vector2.one;
            Vector2 offset = source != null && source.HasProperty(textureProperty) ? source.GetTextureOffset(textureProperty) : Vector2.zero;
            _properties.SetTexture(TextureId, texture != null ? texture : Texture2D.whiteTexture);
            _properties.SetVector(TextureSTId, new Vector4(scale.x, scale.y, offset.x, offset.y));
            _properties.SetFloat(AspectId, bounds.size.x / Mathf.Max(0.0001f, bounds.size.y));
            _root.SetActive(true);
            Tick();
        }

        internal void Tick()
        {
            if (_root == null || !_root.activeSelf) return;
            float elapsed = Time.time - StartedAt;
            float progress = elapsed / Duration;
            if (!_card || !_card.isActiveAndEnabled || !_visual || _visual.parent != _visualParent
                || _card.transform.parent != _cardParent
                || _card.IsInHand || _card.IsFlyingToShelf || _card.IsPackReveal || _card.HasActivePhysics
                || progress >= 1f)
            {
                Stop();
                return;
            }
            float pop = elapsed <= PopRiseSeconds
                ? Mathf.SmoothStep(0f, 1f, elapsed / PopRiseSeconds)
                : 1f - Mathf.SmoothStep(0f, 1f, (elapsed - PopRiseSeconds) / (PopEndSeconds - PopRiseSeconds));
            float scale = Mathf.Lerp(1f, PopPeakScale, pop);
            _appliedVisualScale = _restVisualScale * scale;
            _appliedVisualPosition = _restVisualPosition + _bottomPivotOffset * (1f - scale);
            _visual.localScale = _appliedVisualScale;
            _visual.localPosition = _appliedVisualPosition;
            _root.transform.SetPositionAndRotation(_visual.TransformPoint(_localPosition), _visual.rotation);
            _root.transform.localScale = Vector3.Scale(_visual.lossyScale, _localScale);
            _properties.SetFloat(ProgressId, Mathf.Clamp01(progress));
            _renderer.SetPropertyBlock(_properties);
        }

        internal void Stop()
        {
            // Restore only our own last pose, never a newer hand/reveal pose set by another owner.
            if (_visual && _visual.parent == _visualParent)
            {
                if (_visual.localScale == _appliedVisualScale) _visual.localScale = _restVisualScale;
                if (_visual.localPosition == _appliedVisualPosition) _visual.localPosition = _restVisualPosition;
            }
            _card = null;
            _visual = null;
            _visualParent = null;
            _cardParent = null;
            _properties.Clear();
            if (_renderer) _renderer.SetPropertyBlock(null);
            if (_root) _root.SetActive(false);
        }
    }
}
