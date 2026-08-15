using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Regional twinkling stars on a pack-reveal card face until the player collects the cards.
/// Drop a white star PNG at Resources/PackRevealStar.png (optional — procedural fallback exists).
/// </summary>
public sealed class PackRevealCardSparkle : MonoBehaviour
{
    const string StarTextureResourcePath = "PackRevealStar";
    const float DepthInFrontOfCard = 0.006f;
    const float OutsideOverflowFraction = 0.22f;
    const int RegionColumns = 3;
    const int RegionRows = 3;
    const int RegionCount = RegionColumns * RegionRows;
    const int MaxActiveStars = 16;
    const float SpawnIntervalMin = 0.22f;
    const float SpawnIntervalMax = 0.48f;
    const float InitialSpawnDelayMax = 0.18f;
    const float LargerStarChance = 0.16f;
    const int CenterRegionIndex = 4;

    static Texture2D _sharedStarTexture;
    static Shader _sharedShader;

    readonly List<PackRevealTwinkleStar> _activeStars = new List<PackRevealTwinkleStar>(MaxActiveStars);
    readonly Queue<int> _regionQueue = new Queue<int>(RegionCount);

    Coroutine _spawnRoutine;
    bool _spawning;

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
        if (_spawning)
            return;

        _spawning = true;
        gameObject.SetActive(true);
        RefillRegionQueue();
        _spawnRoutine = StartCoroutine(SpawnLoopRoutine());
    }

    IEnumerator SpawnLoopRoutine()
    {
        if (InitialSpawnDelayMax > 0f)
            yield return new WaitForSeconds(Random.Range(0.04f, InitialSpawnDelayMax));

        while (_spawning)
        {
            PruneFinishedStars();

            if (_activeStars.Count < MaxActiveStars)
                SpawnStarInNextRegion();

            float wait = Random.Range(SpawnIntervalMin, SpawnIntervalMax);
            yield return new WaitForSeconds(wait);
        }
    }

    void SpawnStarInNextRegion()
    {
        Texture2D texture = GetStarTexture();
        Shader shader = GetStarShader();
        if (texture == null || shader == null)
            return;

        if (_regionQueue.Count == 0)
            RefillRegionQueue();

        int region = _regionQueue.Dequeue();
        bool larger = Random.value < LargerStarChance;

        float minSize = CardDimensions.Width * (larger ? 0.11f : 0.06f);
        float maxSize = CardDimensions.Width * (larger ? 0.22f : 0.19f);
        float size = Random.Range(minSize, maxSize);
        Vector3 position = SampleRegionPosition(region);

        var starObject = new GameObject(larger ? "TwinkleStar_Large" : "TwinkleStar");
        starObject.transform.SetParent(transform, false);
        starObject.transform.localPosition = position;
        starObject.transform.localRotation = Quaternion.identity;
        starObject.transform.localScale = Vector3.one * size;

        var star = starObject.AddComponent<PackRevealTwinkleStar>();
        star.Initialize(shader, texture);
        _activeStars.Add(star);
    }

    void RefillRegionQueue()
    {
        _regionQueue.Clear();

        var regions = new int[RegionCount - 1];
        int index = 0;
        for (int i = 0; i < RegionCount; i++)
        {
            if (i == CenterRegionIndex)
                continue;

            regions[index++] = i;
        }

        for (int i = regions.Length - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (regions[i], regions[swapIndex]) = (regions[swapIndex], regions[i]);
        }

        for (int i = 0; i < regions.Length; i++)
            _regionQueue.Enqueue(regions[i]);
    }

    Vector3 SampleRegionPosition(int region)
    {
        float cardHalfWidth = CardDimensions.Width * 0.5f;
        float cardHalfHeight = CardDimensions.Height * 0.5f;
        float extentHalfWidth = cardHalfWidth * (1f + OutsideOverflowFraction);
        float extentHalfHeight = cardHalfHeight * (1f + OutsideOverflowFraction);

        int column = region % RegionColumns;
        int row = region / RegionColumns;
        float cellWidth = (extentHalfWidth * 2f) / RegionColumns;
        float cellHeight = (extentHalfHeight * 2f) / RegionRows;
        float minX = -extentHalfWidth + column * cellWidth;
        float minY = -extentHalfHeight + row * cellHeight;

        float paddingX = cellWidth * 0.14f;
        float paddingY = cellHeight * 0.14f;

        return new Vector3(
            Random.Range(minX + paddingX, minX + cellWidth - paddingX),
            Random.Range(minY + paddingY, minY + cellHeight - paddingY),
            DepthInFrontOfCard);
    }

    void PruneFinishedStars()
    {
        for (int i = _activeStars.Count - 1; i >= 0; i--)
        {
            PackRevealTwinkleStar star = _activeStars[i];
            if (star == null)
                _activeStars.RemoveAt(i);
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
        _spawning = false;

        if (_spawnRoutine != null)
        {
            StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
        }

        if (Application.isPlaying)
            Destroy(gameObject);
        else
            DestroyImmediate(gameObject);
    }
}

sealed class PackRevealTwinkleStar : MonoBehaviour
{
    const float HoldAtPeakDuration = 2f;
    const float FadeInDurationMin = 0.28f;
    const float FadeInDurationMax = 0.42f;
    const float FadeOutDurationMin = 0.85f;
    const float FadeOutDurationMax = 1.2f;

    Material _material;
    float _elapsed;
    float _fadeInDuration;
    float _fadeOutDuration;
    float _lifetime;
    float _twinkleSpeed;
    float _twinklePhase;
    float _peakAlpha;

    public void Initialize(Shader shader, Texture2D texture)
    {
        _fadeInDuration = Random.Range(FadeInDurationMin, FadeInDurationMax);
        _fadeOutDuration = Random.Range(FadeOutDurationMin, FadeOutDurationMax);
        _lifetime = _fadeInDuration + HoldAtPeakDuration + _fadeOutDuration;
        _twinkleSpeed = Random.Range(4.5f, 8.5f);
        _twinklePhase = Random.Range(0f, Mathf.PI * 2f);
        _peakAlpha = Random.Range(0.45f, 0.9f);
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
        _material.color = new Color(1f, 1f, 1f, 0f);
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
        if (_elapsed >= _lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float envelope;
        if (_elapsed < _fadeInDuration)
        {
            envelope = Mathf.SmoothStep(0f, 1f, _elapsed / _fadeInDuration);
        }
        else if (_elapsed < _fadeInDuration + HoldAtPeakDuration)
        {
            envelope = 1f;
        }
        else
        {
            float fadeOutElapsed = _elapsed - _fadeInDuration - HoldAtPeakDuration;
            envelope = 1f - Mathf.SmoothStep(0f, 1f, fadeOutElapsed / _fadeOutDuration);
        }

        float twinkle = envelope >= 0.98f
            ? 0.92f + 0.08f * Mathf.Sin(_elapsed * _twinkleSpeed + _twinklePhase)
            : 0.84f + 0.16f * Mathf.Sin(_elapsed * _twinkleSpeed + _twinklePhase);
        float scalePulse = 1f + 0.06f * Mathf.Sin(_elapsed * (_twinkleSpeed * 0.7f) + _twinklePhase);

        if (transform.childCount > 0)
            transform.GetChild(0).localScale = Vector3.one * scalePulse;

        SetAlpha(_peakAlpha * envelope * twinkle);
    }

    void SetAlpha(float alpha)
    {
        if (_material == null)
            return;

        Color color = Color.white;
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
