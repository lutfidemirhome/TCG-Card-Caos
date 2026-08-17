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
    const string DefaultMeshChildName = "Trading_Card";
    const float DefaultHeldForwardExtra = 0.018f;
    const float DefaultOutlineSizePadding = 1.045f;
    const float DefaultOutlineSurfaceLift = 0.14f;

    [Header("Auto fit")]
    [Tooltip("Pack kalınlığı (et kalınlığı). 1 = kart kalınlığı kadar.")]
    [Min(0.25f)]
    [SerializeField] float thicknessFitMultiplier = DefaultThicknessFitMultiplier;

    [Header("Mesh child (Trading_Card)")]
    [Tooltip("FBX içindeki mesh child adı.")]
    [SerializeField] string meshChildName = DefaultMeshChildName;

    [SerializeField] Vector3 meshLocalPosition = new Vector3(0f, 0f, 0.03802678f);

    [SerializeField] Vector3 meshLocalRotationEuler = new Vector3(-90f, 0f, -0.345f);

    [SerializeField] Vector3 meshLocalScale = new Vector3(1f, 2.398732f, 1.475494f);

    [Header("Hand / outline")]
    [Tooltip("Elde pack'i kameraya biraz daha çeker; arkadaki karta girmesin diye.")]
    [Min(0f)]
    [SerializeField] float heldForwardExtra = DefaultHeldForwardExtra;

    [Tooltip("Outline çerçevesini pack yüzeyinden hafif dışarı taşır.")]
    [Min(1f)]
    [SerializeField] float outlineSizePadding = DefaultOutlineSizePadding;

    [Tooltip("Outline'ı kalınlık yönünde yüzeyden ne kadar kaldıracağı (mesh kalınlığı oranı).")]
    [Min(0f)]
    [SerializeField] float outlineSurfaceLift = DefaultOutlineSurfaceLift;

    public float ThicknessFitMultiplier => thicknessFitMultiplier;
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

    public static float GetOutlineSizePaddingOrDefault()
    {
        PackVisualSettings settings = LoadOrNull();
        return settings != null ? settings.outlineSizePadding : DefaultOutlineSizePadding;
    }

    public static float GetOutlineSurfaceLiftOrDefault()
    {
        PackVisualSettings settings = LoadOrNull();
        return settings != null ? settings.outlineSurfaceLift : DefaultOutlineSurfaceLift;
    }
}
