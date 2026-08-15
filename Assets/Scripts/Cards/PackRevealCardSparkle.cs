using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Rotating yellow magic_blast sparkle placed just behind a pack-reveal card face.
/// </summary>
public sealed class PackRevealCardSparkle : MonoBehaviour
{
    const string MaterialResourcePath = "PackRevealMagicBlast";
    const float DepthBehindCard = 0.012f;
    const float SizeFactor = 1.5f;

    static Material _sharedMaterial;

    [SerializeField] float rotationSpeed = 92f;
    [SerializeField] float pulseSpeed = 2.4f;
    [SerializeField] float pulseAmount = 0.08f;

    Transform _visual;
    float _baseScale = 1f;
    float _pulsePhase;

    public static PackRevealCardSparkle Attach(Transform cardVisual, float revealScale)
    {
        if (cardVisual == null)
            return null;

        var root = new GameObject("PackRevealSparkle");
        root.transform.SetParent(cardVisual, false);
        root.transform.localPosition = new Vector3(0f, 0f, -DepthBehindCard);
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        var sparkle = root.AddComponent<PackRevealCardSparkle>();
        sparkle.Initialize(revealScale);
        return sparkle;
    }

    public void Initialize(float revealScale)
    {
        rotationSpeed = Random.Range(80f, 104f);
        BuildVisual(revealScale);
    }

    void BuildVisual(float revealScale)
    {
        Material material = GetSharedMaterial();
        if (material == null)
            return;

        float size = CardDimensions.Width * revealScale * SizeFactor;
        _baseScale = size;
        _pulsePhase = Random.Range(0f, Mathf.PI * 2f);

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "MagicBlastSparkle";
        quad.transform.SetParent(transform, false);
        quad.transform.localRotation = Quaternion.identity;
        quad.transform.localScale = Vector3.one * size;

        Collider collider = quad.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        var renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        _visual = quad.transform;
    }

    static Material GetSharedMaterial()
    {
        if (_sharedMaterial != null)
            return _sharedMaterial;

        Material loaded = Resources.Load<Material>(MaterialResourcePath);
        if (loaded != null)
        {
            _sharedMaterial = new Material(loaded);
            ApplySparkleMaterialSettings(_sharedMaterial);
            return _sharedMaterial;
        }

        _sharedMaterial = CreateFallbackMaterial();
        return _sharedMaterial;
    }

    static Material CreateFallbackMaterial()
    {
        Texture mainTexture = null;
        Material template = Resources.Load<Material>(MaterialResourcePath);
        if (template != null)
            mainTexture = template.mainTexture;

        Shader shader = Shader.Find("Sprites/Default")
            ?? Shader.Find("Unlit/Transparent")
            ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            return null;

        var material = new Material(shader);
        if (mainTexture != null)
            material.mainTexture = mainTexture;

        material.color = new Color(1f, 0.88f, 0.2f, 0.9f);
        ApplySparkleMaterialSettings(material);
        return material;
    }

    static void ApplySparkleMaterialSettings(Material material)
    {
        material.renderQueue = (int)RenderQueue.Transparent + 2;
    }

    void Update()
    {
        if (_visual == null)
            return;

        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime, Space.Self);

        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed + _pulsePhase) * pulseAmount;
        _visual.localScale = Vector3.one * (_baseScale * pulse);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void DestroySparkle()
    {
        if (Application.isPlaying)
            Destroy(gameObject);
        else
            DestroyImmediate(gameObject);
    }
}
