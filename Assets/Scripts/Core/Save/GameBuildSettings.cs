using UnityEngine;

/// <summary>
/// Optional runtime override for Demo vs Full when the TCG_DEMO define is not set.
/// Create at Resources/GameBuildSettings.
/// </summary>
[CreateAssetMenu(fileName = "GameBuildSettings", menuName = "TCG Card Caos/Game Build Settings")]
public class GameBuildSettings : ScriptableObject
{
    public const string ResourcePath = "GameBuildSettings";

    [SerializeField] bool treatAsDemo;

    public bool TreatAsDemo => treatAsDemo;

    public static GameBuildSettings Load()
    {
        return Resources.Load<GameBuildSettings>(ResourcePath);
    }
}
