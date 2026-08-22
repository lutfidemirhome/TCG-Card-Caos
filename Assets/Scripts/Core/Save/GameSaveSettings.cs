using UnityEngine;

[CreateAssetMenu(fileName = "GameSaveSettings", menuName = "TCG Card Caos/Game Save Settings")]
public class GameSaveSettings : ScriptableObject
{
    public const string ResourcePath = "GameSaveSettings";
    public const int CurrentSaveVersion = 1;
    public const int AutosaveSlotCount = 3;

    [SerializeField] float periodicAutosaveSeconds = 60f;
    [SerializeField] int maxManualSlots = 8;
    [SerializeField] int thumbnailWidth = 256;
    [SerializeField] int thumbnailHeight = 144;
    [SerializeField] bool debugLogs;

    public float PeriodicAutosaveSeconds => Mathf.Max(5f, periodicAutosaveSeconds);
    public int MaxManualSlots => Mathf.Max(1, maxManualSlots);
    public int ThumbnailWidth => Mathf.Clamp(thumbnailWidth, 64, 512);
    public int ThumbnailHeight => Mathf.Clamp(thumbnailHeight, 36, 288);
    public bool DebugLogs => debugLogs;

    public static GameSaveSettings LoadOrDefault()
    {
        GameSaveSettings settings = Resources.Load<GameSaveSettings>(ResourcePath);
        return settings != null ? settings : CreateInstance<GameSaveSettings>();
    }
}
