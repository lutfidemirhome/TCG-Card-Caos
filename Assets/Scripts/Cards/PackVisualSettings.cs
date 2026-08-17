using UnityEngine;

/// <summary>
/// Booster pack 3D model tuning. Edit in Project without touching code.
/// Asset path: Resources/Settings/PackVisualSettings
/// </summary>
[CreateAssetMenu(fileName = "PackVisualSettings", menuName = "TCG Card Caos/Pack Visual Settings")]
public class PackVisualSettings : ScriptableObject
{
    public const string ResourcePath = "Settings/PackVisualSettings";
    const float DefaultThicknessFitMultiplier = 3.5f;
    const float DefaultHeldThicknessFitMultiplier = 1f;
    const string DefaultMeshChildName = "Trading_Card";
    const float DefaultHeldForwardExtra = 0.018f;
    const float DefaultQuickOutlineWidth = 7.44f;

    [Header("Auto fit")]
    [Tooltip("Pack kalınlığı (et kalınlığı). 1 = kart kalınlığı kadar.")]
    [Min(0.25f)]
    [SerializeField] float thicknessFitMultiplier = DefaultThicknessFitMultiplier;

    [Tooltip("Elde pack et kalınlığı. 1 = kart kadar ince; yan yüz görünmediği için elde kullanılır.")]
    [Min(0.25f)]
    [SerializeField] float heldThicknessFitMultiplier = DefaultHeldThicknessFitMultiplier;

    [Header("Mesh child (Trading_Card)")]
    [Tooltip("FBX içindeki mesh child adı.")]
    [SerializeField] string meshChildName = DefaultMeshChildName;

    [SerializeField] Vector3 meshLocalPosition = new Vector3(0f, 0f, 0.03802678f);

    [SerializeField] Vector3 meshLocalRotationEuler = new Vector3(-90f, 0f, -0.345f);

    [SerializeField] Vector3 meshLocalScale = new Vector3(1f, 2.398732f, 1.475494f);

    [Header("Hand")]
    [Tooltip("Elde pack'i kameraya biraz daha çeker; arkadaki karta girmesin diye.")]
    [Min(0f)]
    [SerializeField] float heldForwardExtra = DefaultHeldForwardExtra;

    [Header("Quick Outline (pack only)")]
    [Tooltip("Pack mesh üzerindeki Quick Outline kalınlığı.")]
    [Min(0f)]
    [SerializeField] float quickOutlineWidth = DefaultQuickOutlineWidth;

    public float ThicknessFitMultiplier => thicknessFitMultiplier;
    public float HeldThicknessFitMultiplier => heldThicknessFitMultiplier;
    public float QuickOutlineWidth => quickOutlineWidth;
    public string MeshChildName => meshChildName;
    public Vector3 MeshLocalPosition => meshLocalPosition;
    public Vector3 MeshLocalRotationEuler => meshLocalRotationEuler;
    public Vector3 MeshLocalScale => meshLocalScale;

    public static PackVisualSettings LoadOrNull() =>
        Resources.Load<PackVisualSettings>(ResourcePath);

    public static float GetThicknessFitMultiplierOrDefault()
    {
        PackVisualSettings settings = LoadOrNull();
        return settings != null ? settings.ThicknessFitMultiplier : DefaultThicknessFitMultiplier;
    }

    public static float GetHeldForwardExtraOrDefault()
    {
        PackVisualSettings settings = LoadOrNull();
        return settings != null ? settings.heldForwardExtra : DefaultHeldForwardExtra;
    }

    public static float GetHeldThicknessFitMultiplierOrDefault()
    {
        PackVisualSettings settings = LoadOrNull();
        return settings != null ? settings.HeldThicknessFitMultiplier : DefaultHeldThicknessFitMultiplier;
    }

    public static float GetQuickOutlineWidthOrDefault()
    {
        PackVisualSettings settings = LoadOrNull();
        return settings != null ? settings.QuickOutlineWidth : DefaultQuickOutlineWidth;
    }
}
