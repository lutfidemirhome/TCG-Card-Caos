using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Bounded, scene-owned pool for Assemble trails and card reveal/arrival flashes.</summary>
[DefaultExecutionOrder(110)]
public sealed class AssemblePickupTrail : MonoBehaviour
{
    const float FadeDuration = 0.24f;
    static AssemblePickupTrail _instance;
    readonly List<Emitter> _emitters = new List<Emitter>(CardDimensions.MaxHandSize);
    Material _ribbonMaterial;
    Material _sparkMaterial;
    Material _arrivalMaterial;
    Mesh _arrivalMesh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _instance = null;

    public static Emitter Begin(Transform source)
    {
        return EnsurePool(source) ? _instance.Take(source.position, source.gameObject.layer, true) : null;
    }

    public static void PlayReveal(WorldCard card)
    {
        if (card == null || !card.IsPackReveal || !card.isActiveAndEnabled || !EnsurePool(card.transform))
            return;

        Emitter effect = _instance.Take(card.transform.position, card.gameObject.layer, false);
        effect?.PlayGlow(card, true);
    }

    static bool EnsurePool(Transform source)
    {
        if (source == null || !source.gameObject.scene.IsValid())
            return false;

        if (_instance == null)
        {
            var root = new GameObject("Assemble Pickup Trails (Runtime)");
            SceneManager.MoveGameObjectToScene(root, source.gameObject.scene);
            _instance = root.AddComponent<AssemblePickupTrail>();
            _instance._ribbonMaterial = Resources.Load<Material>("UI/Skills/AssembleRibbon");
            _instance._sparkMaterial = Resources.Load<Material>("UI/Skills/AssembleSpark");
            _instance._arrivalMaterial = Resources.Load<Material>("UI/Skills/AssembleArrival");
        }
        return true;
    }

    public static void CancelArrival(WorldCard card)
    {
        if (_instance == null) return;
        foreach (Emitter emitter in _instance._emitters) emitter.CancelArrival(card);
    }

    Emitter Take(Vector3 position, int layer, bool trail)
    {
        if (_ribbonMaterial == null || _sparkMaterial == null || (!trail && _arrivalMaterial == null))
            return null;

        Emitter available = null;
        Emitter fading = null;
        foreach (Emitter emitter in _emitters)
        {
            if (!emitter.Root.activeSelf) { available = emitter; break; }
            if (!emitter.Following && (fading == null || emitter.ExpiresAt < fading.ExpiresAt))
                fading = emitter;
        }
        if (available == null && _emitters.Count < CardDimensions.MaxHandSize)
        {
            if (_arrivalMesh == null) _arrivalMesh = CreateArrivalMesh();
            available = new Emitter(transform, _ribbonMaterial, _sparkMaterial,
                _arrivalMaterial, _arrivalMesh, (uint)_emitters.Count + 17u);
            _emitters.Add(available);
        }
        // Fast repeated uses may shorten an old fade, but never steal a live card's effect.
        if (available == null) available = fading;
        if (available == null) return null;
        available.Start(position, layer, trail);
        enabled = true;
        return available;
    }

    void Update()
    {
        bool active = false;
        foreach (Emitter emitter in _emitters)
        {
            if (!emitter.Root.activeSelf) continue;
            if (!emitter.Following && Time.time >= emitter.ExpiresAt)
                emitter.Finish(true);
            else
                active = true;
        }
        enabled = active;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (_arrivalMesh != null) Destroy(_arrivalMesh);
    }

    void LateUpdate()
    {
        // The hand applies its fan and landing pose first; the flash follows that final pose.
        foreach (Emitter emitter in _emitters) emitter.UpdateArrival();
    }

    static Mesh CreateArrivalMesh()
    {
        var mesh = new Mesh { name = "Assemble arrival quad" };
        mesh.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f),
            new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);
        return mesh;
    }

    public sealed class Emitter
    {
        public readonly GameObject Root;
        public readonly TrailRenderer Ribbon;
        public readonly ParticleSystem Sparks;
        public readonly GameObject Glow;
        public readonly MeshRenderer GlowRenderer;
        public bool Following { get; private set; }
        public float ExpiresAt { get; private set; }
        public bool IsGlowing => _arrivalCard != null;
        const float ArrivalDuration = 0.28f;
        static readonly int ArrivalTimeId = Shader.PropertyToID("_ArrivalTime");
        readonly MaterialPropertyBlock _arrivalProperties = new MaterialPropertyBlock();
        WorldCard _arrivalCard;
        Transform _arrivalVisual;
        Vector3 _arrivalLocalPosition;
        Vector3 _arrivalLocalScale;
        float _arrivalStarted;
        bool _revealGlow;

        internal Emitter(Transform parent, Material ribbonMaterial, Material sparkMaterial,
            Material arrivalMaterial, Mesh arrivalMesh, uint seed)
        {
            Root = new GameObject("Pooled card trail");
            Root.SetActive(false);
            Root.transform.SetParent(parent, false);
            Ribbon = Root.AddComponent<TrailRenderer>();
            Ribbon.sharedMaterial = ribbonMaterial;
            Ribbon.time = 0.16f;
            Ribbon.minVertexDistance = 0.035f;
            Ribbon.widthMultiplier = 0.04f;
            Ribbon.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            Ribbon.colorGradient = FadeGradient(0.78f);
            Ribbon.numCornerVertices = 2;
            Ribbon.numCapVertices = 2;
            Ribbon.alignment = LineAlignment.View;
            Ribbon.textureMode = LineTextureMode.Stretch;
            Ribbon.autodestruct = false;
            Ribbon.emitting = false;
            ConfigureRenderer(Ribbon);

            var sparkObject = new GameObject("Sparkles");
            sparkObject.transform.SetParent(Root.transform, false);
            Sparks = sparkObject.AddComponent<ParticleSystem>();
            Sparks.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            Sparks.useAutoRandomSeed = false;
            Sparks.randomSeed = seed;
            var main = Sparks.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startSpeed = 0.025f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.038f);
            main.startColor = Color.white;
            main.maxParticles = 32;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = false;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = Sparks.emission;
            emission.rateOverTime = 56f;
            emission.rateOverDistance = 12f;
            var shape = Sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.012f;
            var color = Sparks.colorOverLifetime;
            color.enabled = true;
            color.color = FadeGradient(0.95f);
            var size = Sparks.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            var renderer = Sparks.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = sparkMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.maxParticleSize = 0.08f;
            ConfigureRenderer(renderer);

            Glow = new GameObject("Arrival glow");
            Glow.transform.SetParent(Root.transform, false);
            Glow.AddComponent<MeshFilter>().sharedMesh = arrivalMesh;
            GlowRenderer = Glow.AddComponent<MeshRenderer>();
            GlowRenderer.sharedMaterial = arrivalMaterial;
            ConfigureRenderer(GlowRenderer);
            Glow.SetActive(false);
        }

        internal void Start(Vector3 position, int layer, bool trail)
        {
            Finish(true);
            Root.layer = layer;
            Sparks.gameObject.layer = layer;
            Glow.layer = layer;
            Root.transform.position = position;
            Root.SetActive(true);
            Ribbon.Clear();
            Ribbon.emitting = trail;
            if (trail) Sparks.Play(false);
            Following = trail;
        }

        public void Follow(Vector3 position)
        {
            if (Root != null && Following)
                Root.transform.position = position;
        }

        public void Finish(bool immediate = false)
        {
            if (Root == null) return;
            Following = false;
            Ribbon.emitting = false;
            Sparks.Stop(false, immediate ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting);
            ExpiresAt = Time.time + FadeDuration;
            if (!immediate) return;
            ClearArrival();
            Ribbon.Clear();
            Root.SetActive(false);
        }

        public void PlayArrival(WorldCard card)
        {
            PlayGlow(card, false);
        }

        internal void PlayGlow(WorldCard card, bool reveal)
        {
            if (Root == null || !Root.activeSelf || card == null || !card.isActiveAndEnabled
                || (reveal ? !card.IsPackReveal : !card.IsHeld)
                || GlowRenderer.sharedMaterial == null
                || !card.TryGetSkillArrivalGlowSurface(out Transform visual, out Bounds bounds))
                return;

            // Cache the surface once. Never replace a card's material or rescan its model each frame.
            _arrivalCard = card;
            _revealGlow = reveal;
            _arrivalVisual = visual;
            _arrivalLocalPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z + 0.001f);
            _arrivalLocalScale = new Vector3(bounds.size.x * 1.06f, bounds.size.y * 1.06f, 1f);
            _arrivalStarted = Time.time;
            ExpiresAt = Mathf.Max(ExpiresAt, Time.time + ArrivalDuration);
            Glow.SetActive(true);
            UpdateArrival();
        }

        public void UpdateArrival()
        {
            if (Glow == null || !Glow.activeSelf) return;
            float age = (Time.time - _arrivalStarted) / ArrivalDuration;
            if (_arrivalCard == null || !_arrivalCard.isActiveAndEnabled
                || (_revealGlow ? !_arrivalCard.IsPackReveal : !_arrivalCard.IsHeld)
                || _arrivalVisual == null || age >= 1f)
            {
                ClearArrival();
                return;
            }
            Glow.transform.SetPositionAndRotation(_arrivalVisual.TransformPoint(_arrivalLocalPosition), _arrivalVisual.rotation);
            Glow.transform.localScale = Vector3.Scale(_arrivalVisual.lossyScale, _arrivalLocalScale);
            _arrivalProperties.SetFloat(ArrivalTimeId, Mathf.Clamp01(age));
            GlowRenderer.SetPropertyBlock(_arrivalProperties);
        }

        void ClearArrival()
        {
            _arrivalCard = null;
            _arrivalVisual = null;
            if (Glow != null) Glow.SetActive(false);
        }

        internal void CancelArrival(WorldCard card)
        {
            if (ReferenceEquals(_arrivalCard, card)) ClearArrival();
        }

        static Gradient FadeGradient(float alpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] {
                new GradientColorKey(new Color(1f, 0.96f, 0.74f), 0f),
                new GradientColorKey(new Color(1f, 0.73f, 0.28f), 1f)
            }, new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        static void ConfigureRenderer(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }
    }
}
