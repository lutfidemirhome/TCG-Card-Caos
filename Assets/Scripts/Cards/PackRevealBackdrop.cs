using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Semi-transparent screen dimmer during pack reveal. A camera-local quad is sized from
/// the live viewport frustum so it covers every PC resolution and aspect ratio, sitting
/// just behind the reveal cards.
/// </summary>
public sealed class PackRevealBackdrop : MonoBehaviour
{
    const float PlaneDistancePadding = 0.07f;
    const float FrustumOverscan = 1.08f;

    Camera _camera;
    float _revealPlaneDistance;
    float _currentAlpha;
    MeshRenderer _renderer;
    Material _material;

    public static PackRevealBackdrop Create(Camera camera, float revealPlaneDistance)
    {
        if (camera == null)
            return null;

        var root = new GameObject("PackRevealBackdrop");
        root.transform.SetParent(camera.transform, false);
        root.transform.localRotation = Quaternion.identity;

        var backdrop = root.AddComponent<PackRevealBackdrop>();
        backdrop._camera = camera;
        backdrop._revealPlaneDistance = revealPlaneDistance;
        backdrop.BuildQuad();
        return backdrop;
    }

    void BuildQuad()
    {
        var quadGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadGo.name = "DimmerQuad";
        quadGo.transform.SetParent(transform, false);
        quadGo.transform.localRotation = Quaternion.identity;

        Collider collider = quadGo.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        _renderer = quadGo.GetComponent<MeshRenderer>();
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.lightProbeUsage = LightProbeUsage.Off;
        _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        _material = CreateDimmerMaterial();
        _renderer.sharedMaterial = _material;

        RefreshFrustumCoverage();
        SetAlpha(0f);
    }

    void LateUpdate()
    {
        RefreshFrustumCoverage();
    }

    void RefreshFrustumCoverage()
    {
        if (_camera == null)
            return;

        float planeDistance = Mathf.Max(0.01f, _revealPlaneDistance + PlaneDistancePadding);
        float halfFovRad = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float frustumHeight = 2f * planeDistance * Mathf.Tan(halfFovRad);
        float frustumWidth = frustumHeight * _camera.aspect;

        transform.localPosition = new Vector3(0f, 0f, planeDistance);
        transform.localScale = new Vector3(
            frustumWidth * FrustumOverscan,
            frustumHeight * FrustumOverscan,
            1f);
    }

    static Material CreateDimmerMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("UI/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");

        var material = new Material(shader);
        material.color = Color.black;
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    public IEnumerator FadeTo(float targetAlpha, float duration)
    {
        targetAlpha = Mathf.Clamp01(targetAlpha);
        float startAlpha = _currentAlpha;
        if (duration <= 0f)
        {
            SetAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    void SetAlpha(float alpha)
    {
        _currentAlpha = alpha;
        if (_material != null)
            _material.color = new Color(0f, 0f, 0f, alpha);
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
