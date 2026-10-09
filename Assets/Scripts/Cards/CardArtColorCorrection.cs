using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Compensates card artwork for the shop's exposure/contrast/saturation before the
/// existing camera grading. One set of globals per camera; no per-card update or extra draw.
/// </summary>
public static class CardArtColorCorrection
{
    static readonly int EnabledId = Shader.PropertyToID("_CardArtGradingEnabled");
    static readonly int InverseId = Shader.PropertyToID("_CardArtInverseGrading");
    static Shader _shader;
    static bool _subscribed;
    static readonly Dictionary<int, Volume[]> VolumesByMask = new Dictionary<int, Volume[]>();
    static Vector3 _lastAdjustments = new Vector3(float.NaN, 0f, 0f);
    static Vector4 _inverse;

    public static Shader GetShader()
    {
        Subscribe();
        if (_shader == null)
        {
            // An instancing-enabled material asset keeps this shader's instanced
            // variants in player builds, where all actual card materials are cached at runtime.
            var template = Resources.Load<Material>("Cards/CardColorPreservingTemplate");
            _shader = template != null ? template.shader : null;
        }
        return _shader != null && _shader.isSupported ? _shader : null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneUnloaded -= SceneUnloaded;
        _subscribed = false;
        VolumesByMask.Clear();
        Shader.SetGlobalFloat(EnabledId, 0f);
        Subscribe();
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    static void InitializeEditor()
    {
        Subscribe();
        UnityEditor.EditorApplication.hierarchyChanged += EditorHierarchyChanged;
    }

    static void EditorHierarchyChanged()
    {
        if (!Application.isPlaying)
            VolumesByMask.Clear();
    }
#endif

    static void SceneLoaded(Scene scene, LoadSceneMode mode) => VolumesByMask.Clear();
    static void SceneUnloaded(Scene scene) => VolumesByMask.Clear();

    static void Subscribe()
    {
        if (_subscribed)
            return;
        RenderPipelineManager.beginCameraRendering += BeforeCamera;
        SceneManager.sceneLoaded += SceneLoaded;
        SceneManager.sceneUnloaded += SceneUnloaded;
        _subscribed = true;
    }

    static void BeforeCamera(ScriptableRenderContext context, Camera camera)
    {
        // URP updates its volume stack after this callback. Read the shop's single
        // global volume directly, so Scene/Game/Recorder cameras cannot use stale grading.
        Shader.SetGlobalFloat(EnabledId, 0f);
        if (camera == null || QualitySettings.activeColorSpace != ColorSpace.Linear
            || !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
            || pipeline.colorGradingMode != ColorGradingMode.LowDynamicRange)
            return;

        LayerMask mask = 1;
        if (camera.cameraType == CameraType.SceneView)
        {
#if UNITY_EDITOR
            if (!CoreUtils.ArePostProcessesEnabled(camera))
                return;
            Camera main = Camera.main;
            if (main != null && main.TryGetComponent(out UniversalAdditionalCameraData mainData))
                mask = mainData.volumeLayerMask;
#else
            return;
#endif
        }
        else
        {
            if (!camera.TryGetComponent(out UniversalAdditionalCameraData data) || !data.renderPostProcessing)
                return;
            mask = data.volumeLayerMask;
        }

        VolumeProfile profile = null;
        // URP's GetVolumes allocates an array. Cache it until scene membership changes;
        // normal frames only read references and create no managed garbage.
        if (!VolumesByMask.TryGetValue(mask.value, out Volume[] volumes))
        {
            volumes = VolumeManager.instance.GetVolumes(mask);
            VolumesByMask[mask.value] = volumes;
        }
        for (int i = 0; i < volumes.Length; i++)
        {
            Volume volume = volumes[i];
            if (volume == null || !volume.isActiveAndEnabled || volume.weight <= 0f)
                continue;
            // This compensation is deliberately limited to the current shop setup.
            // Fall back to ordinary Unlit if overlapping/local grading is introduced.
            if (!volume.isGlobal || volume.weight < 1f || profile != null)
                return;
            profile = volume.HasInstantiatedProfile() ? volume.profile : volume.sharedProfile;
        }

        if (profile == null || !profile.TryGet(out ColorAdjustments adjustments) || !adjustments.active)
            return;
        if ((adjustments.hueShift.overrideState && !Mathf.Approximately(adjustments.hueShift.value, 0f))
            || (adjustments.colorFilter.overrideState && adjustments.colorFilter.value != Color.white))
            return;
        if (profile.TryGet(out Tonemapping tone) && tone.active && tone.mode.overrideState
            && tone.mode.value != TonemappingMode.None)
            return;
        if (profile.TryGet(out WhiteBalance balance) && balance.active
            && ((balance.temperature.overrideState && !Mathf.Approximately(balance.temperature.value, 0f))
                || (balance.tint.overrideState && !Mathf.Approximately(balance.tint.value, 0f))))
            return;

        float contrast = adjustments.contrast.overrideState ? adjustments.contrast.value : 0f;
        float saturation = adjustments.saturation.overrideState ? adjustments.saturation.value : 0f;
        float exposure = adjustments.postExposure.overrideState ? adjustments.postExposure.value : 0f;
        if (contrast <= -99f || saturation <= -99f)
            return;
        var values = new Vector3(contrast, saturation, exposure);
        if (!_lastAdjustments.Equals(values))
        {
            float inverseContrast = 1f / (1f + contrast * 0.01f);
            // Algebraic inverse of URP's LogC contrast: one pow, without log->pow round-trips.
            float logMultiplier = Mathf.Pow(10f, (0.386036f - 0.4135884f) * (inverseContrast - 1f) / 0.244161f);
            _inverse = new Vector4(inverseContrast,
                1f / (1f + saturation * 0.01f), logMultiplier, Mathf.Pow(2f, -exposure));
            _lastAdjustments = values;
        }
        Shader.SetGlobalVector(InverseId, _inverse);
        Shader.SetGlobalFloat(EnabledId, 1f);
    }
}
