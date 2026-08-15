using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Multiple twinkling stars on a pack-reveal card face after it flips to the front.
/// Drop a white star PNG at Resources/PackRevealStar.png (optional — procedural fallback exists).
/// </summary>
public sealed class PackRevealCardSparkle : MonoBehaviour
{
    const string StarTextureResourcePath = "PackRevealStar";
    const float DepthInFrontOfCard = 0.006f;
    const int MinStarCount = 10;
    const int MaxStarCount = 17;

    static Texture2D _sharedStarTexture;
    static Shader _sharedShader;

    readonly List<PackRevealTwinkleStar> _stars = new List<PackRevealTwinkleStar>(MaxStarCount);
    bool _shown;

    public static PackRevealCardSparkle Attach(Transform cardVisual, float revealScale)
    {
        if (cardVisual == null)
            return null;

        var root = new GameObject("PackRevealSparkle");
        root.transform.SetParent(cardVisual, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        var sparkle = root.AddComponent<PackRevealCardSparkle>();
        root.SetActive(false);
        return sparkle;
    }

    public void Show()
    {
        if (_shown)
            return;

        _shown = true;
        gameObject.SetActive(true);
        SpawnStars();
    }

    void SpawnStars()
    {
        Texture2D texture = GetStarTexture();
        Shader shader = GetStarShader();
        if (texture == null || shader == null)
            return;

        int count = Random.Range(MinStarCount, MaxStarCount + 1);
        float halfWidth = CardDimensions.Width * 0.4f;
        float halfHeight = CardDimensions.Height * 0.4f;
        float minSize = CardDimensions.Width * 0.08f;
        float maxSize = CardDimensions.Width * 0.22f;

        for (int i = 0; i < count; i++)
        {
            float size = Random.Range(minSize, maxSize);
            var position = new Vector3(
                Random.Range(-halfWidth, halfWidth),
                Random.Range(-halfHeight, halfHeight),
                DepthInFrontOfCard);

            var starObject = new GameObject("TwinkleStar");
            starObject.transform.SetParent(transform, false);
            starObject.transform.localPosition = position;
            starObject.transform.localRotation = Quaternion.identity;
            starObject.transform.localScale = Vector3.one * size;

            var star = starObject.AddComponent<PackRevealTwinkleStar>();
            star.Initialize(shader, texture);
            _stars.Add(star);
        }
    }

    static Texture2D GetStarTexture()
    {
        if (_sharedStarTexture != null)
            return _sharedStarTexture;

        Texture2D loaded = Resources.Load<Texture2D>(StarTextureResourcePath);
        _sharedStarTexture = loaded != null ? loaded : CreateProceduralStarTexture();
        return _sharedStarTexture;
    }

    static Shader GetStarShader()
    {
        if (_sharedShader != null)
            return _sharedShader;

        _sharedShader = Shader.Find("Sprites/Default")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Transparent");
        return _sharedShader;
    }

    static Texture2D CreateProceduralStarTexture()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "PackRevealStar_Fallback",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float center = (size - 1) * 0.5f;
        float radius = size * 0.46f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center);
                float dy = Mathf.Abs(y - center);
                float diamond = (dx + dy) / radius;
                float cross = Mathf.Max(dx / (radius * 0.18f), dy / (radius * 0.85f));
                float alpha = Mathf.Clamp01(1f - Mathf.Max(diamond, cross * 0.55f));
                alpha = alpha * alpha;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        return texture;
    }

    public void DestroySparkle()
    {
        if (Application.isPlaying)
            Destroy(gameObject);
        else
            DestroyImmediate(gameObject);
    }
}

sealed class PackRevealTwinkleStar : MonoBehaviour
{
    const float MinLifetime = 0.55f;
    const float MaxLifetime = 1.05f;
    const float FadeInPortion = 0.14f;
    const float FadeOutStart = 0.32f;

    Material _material;
    float _elapsed;
    float _lifetime;
    float _twinkleSpeed;
    float _twinklePhase;
    float _peakAlpha;

    public void Initialize(Shader shader, Texture2D texture)
    {
        _lifetime = Random.Range(MinLifetime, MaxLifetime);
        _twinkleSpeed = Random.Range(10f, 18f);
        _twinklePhase = Random.Range(0f, Mathf.PI * 2f);
        _peakAlpha = Random.Range(0.5f, 1f);
        transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "StarQuad";
        quad.transform.SetParent(transform, false);
        quad.transform.localRotation = Quaternion.identity;
        quad.transform.localScale = Vector3.one;

        Collider collider = quad.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        _material = new Material(shader);
        _material.mainTexture = texture;
        _material.color = Color.white;
        _material.renderQueue = (int)RenderQueue.Transparent + 3;

        if (_material.HasProperty("_Surface"))
            _material.SetFloat("_Surface", 1f);
        if (_material.HasProperty("_Blend"))
            _material.SetFloat("_Blend", 0f);

        var renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = _material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        float t = _elapsed / _lifetime;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        float envelope;
        if (t < FadeInPortion)
            envelope = t / FadeInPortion;
        else if (t < FadeOutStart)
            envelope = 1f;
        else
            envelope = 1f - Mathf.SmoothStep(0f, 1f, (t - FadeOutStart) / (1f - FadeOutStart));

        float twinkle = 0.72f + 0.28f * Mathf.Sin(_elapsed * _twinkleSpeed + _twinklePhase);
        SetAlpha(_peakAlpha * envelope * twinkle);
    }

    void SetAlpha(float alpha)
    {
        if (_material == null)
            return;

        Color color = _material.color;
        color.a = alpha;
        _material.color = color;
    }

    void OnDestroy()
    {
        if (_material != null)
        {
            if (Application.isPlaying)
                Destroy(_material);
            else
                DestroyImmediate(_material);
        }
    }
}
