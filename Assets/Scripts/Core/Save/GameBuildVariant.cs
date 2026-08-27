/// <summary>
/// Demo and Full Release keep completely separate save trees.
/// Switch via the TCG_DEMO scripting define, or Resources/GameBuildSettings.
/// </summary>
public static class GameBuildVariant
{
    public const string Demo = "Demo";
    public const string Full = "Full";

    public const string DemoFolderName = "TCGCardChaos_Demo";
    public const string FullFolderName = "TCGCardChaos";

    public static string Current
    {
        get
        {
#if TCG_DEMO
            return Demo;
#else
            GameBuildSettings settings = GameBuildSettings.Load();
            return settings != null && settings.TreatAsDemo ? Demo : Full;
#endif
        }
    }

    public static bool IsDemo => Current == Demo;

    public static string FolderName => Current == Demo ? DemoFolderName : FullFolderName;

    public static bool Matches(string variant)
    {
        return string.Equals(variant, Current, System.StringComparison.Ordinal);
    }
}
