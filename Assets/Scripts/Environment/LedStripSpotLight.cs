using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public class LedStripSpotLight : MonoBehaviour
{
    const string LightChildName = "StripSpotLight";

    [SerializeField] Color lightColor = new(1f, 0.95f, 0.86f);
    [SerializeField] float intensity = 3.5f;
    [SerializeField] float range = 9f;
    [SerializeField] float spotAngle = 110f;
    [SerializeField] float innerSpotAngle = 88f;
    [SerializeField] Vector3 localLightPosition = new(0f, -0.03f, 0f);
    [SerializeField] Vector3 localLightEuler = new(75f, 0f, 0f);
    [SerializeField] Texture cookie;

    Light _stripLight;

    public void Refresh() => EnsureLight();

    void OnEnable() => EnsureLight();

    void OnValidate() => EnsureLight();

    void EnsureLight()
    {
        if (_stripLight == null)
        {
            Transform existing = transform.Find(LightChildName);
            if (existing != null)
                _stripLight = existing.GetComponent<Light>();
        }

        if (_stripLight == null)
        {
            var lightObject = new GameObject(LightChildName);
            lightObject.transform.SetParent(transform, false);
            _stripLight = lightObject.AddComponent<Light>();
        }

        Transform lightTransform = _stripLight.transform;
        lightTransform.localPosition = localLightPosition;
        lightTransform.localRotation = Quaternion.Euler(localLightEuler);

        _stripLight.type = LightType.Spot;
        _stripLight.color = lightColor;
        _stripLight.intensity = intensity;
        _stripLight.range = range;
        _stripLight.spotAngle = spotAngle;
        _stripLight.innerSpotAngle = innerSpotAngle;
        _stripLight.shadows = LightShadows.None;
        _stripLight.renderMode = LightRenderMode.Auto;
        _stripLight.cookie = cookie;
    }
}
