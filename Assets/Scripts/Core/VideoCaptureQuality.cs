using UnityEngine;

/// <summary>
/// video_tcg_1: max visual fidelity for capture — no distance cull, full card mips, high engine quality.
/// </summary>
public static class VideoCaptureQuality
{
    public const float MinCardDrawDistance = 500f;
    public const int TextureAniso = 16;
    public const float TextureMipBias = -1.25f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyEngineSettingsEarly()
    {
        QualitySettings.globalTextureMipmapLimit = 0;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;

        int high = GameSettings.ToQualityLevel(GameSettings.QualityTier.High);
        if (QualitySettings.GetQualityLevel() < high)
            QualitySettings.SetQualityLevel(high, applyExpensiveChanges: false);
    }

    public static void ApplySceneSettings()
    {
        CardInstancedRenderManager manager = Object.FindFirstObjectByType<CardInstancedRenderManager>();
        if (manager != null)
            manager.EnsureVideoDrawDistance();
    }
}
