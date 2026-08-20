using UnityEngine;

/// <summary>
/// PSA slab 3D model tuning. Asset path: Resources/Settings/PsaVisualSettings
/// </summary>
[CreateAssetMenu(fileName = "PsaVisualSettings", menuName = "TCG Card Caos/PSA Visual Settings")]
public class PsaVisualSettings : ScriptableObject
{
    public const string ResourcePath = "Settings/PsaVisualSettings";
    const float DefaultHeldThicknessFitMultiplier = 1f;
    const float DefaultFrontYawOffset = -90f;
    const float DefaultHeldForwardExtra = 0.012f;
    static readonly Vector3 DefaultModelRootRotationEuler = Vector3.zero;
    static readonly Vector3 DefaultModelRootPosition = Vector3.zero;
    static readonly Vector3 DefaultModelRootScale = new Vector3(1.841724f, 2.041484f, 2.041484f);

    [Header("Hand")]
    [Tooltip("Elde slab et kalınlığı. 1 = normal kart kalınlığı kadar incelir (pack gibi).")]
    [Min(0.25f)]
    [SerializeField] float heldThicknessFitMultiplier = DefaultHeldThicknessFitMultiplier;

    [Tooltip("Elde kameraya biraz daha çeker.")]
    [Min(0f)]
    [SerializeField] float heldForwardExtra = DefaultHeldForwardExtra;

    [Header("Model root (Inspector'dan sen ayarlarsın)")]
    [Tooltip("PsaVisual local rotation.")]
    [SerializeField] Vector3 modelRootRotationEuler = DefaultModelRootRotationEuler;

    [Tooltip("PsaVisual local position.")]
    [SerializeField] Vector3 modelRootPosition = DefaultModelRootPosition;

    [Tooltip("PsaVisual local scale.")]
    [SerializeField] Vector3 modelRootScale = DefaultModelRootScale;

    [Tooltip("Elde tutulurken ek Y dönüşü: ön texture sol tarafta görünsün diye.")]
    [SerializeField] float frontYawOffsetDegrees = DefaultFrontYawOffset;

    public float HeldThicknessFitMultiplier => heldThicknessFitMultiplier;
    public float HeldForwardExtra => heldForwardExtra;
    public Vector3 ModelRootRotationEuler => modelRootRotationEuler;
    public Vector3 ModelRootPosition => modelRootPosition;
    public Vector3 ModelRootScale => modelRootScale;
    public float FrontYawOffsetDegrees => frontYawOffsetDegrees;

    public static PsaVisualSettings LoadOrNull() =>
        Resources.Load<PsaVisualSettings>(ResourcePath);

    public static float GetHeldThicknessFitMultiplierOrDefault()
    {
        PsaVisualSettings settings = LoadOrNull();
        return settings != null ? settings.HeldThicknessFitMultiplier : DefaultHeldThicknessFitMultiplier;
    }

    public static float GetHeldForwardExtraOrDefault()
    {
        PsaVisualSettings settings = LoadOrNull();
        return settings != null ? settings.heldForwardExtra : DefaultHeldForwardExtra;
    }

    public static float GetFrontYawOffsetOrDefault()
    {
        PsaVisualSettings settings = LoadOrNull();
        return settings != null ? settings.frontYawOffsetDegrees : DefaultFrontYawOffset;
    }

    public static Vector3 GetModelRootScaleOrDefault()
    {
        PsaVisualSettings settings = LoadOrNull();
        return settings != null ? settings.modelRootScale : DefaultModelRootScale;
    }

    public static Vector3 GetModelRootRotationEulerOrDefault()
    {
        PsaVisualSettings settings = LoadOrNull();
        return settings != null ? settings.modelRootRotationEuler : DefaultModelRootRotationEuler;
    }

    public static Vector3 GetModelRootPositionOrDefault()
    {
        PsaVisualSettings settings = LoadOrNull();
        return settings != null ? settings.modelRootPosition : DefaultModelRootPosition;
    }
}
