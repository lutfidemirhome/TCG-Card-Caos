/// <summary>
/// Cheap gameplay hooks. Does not serialize or write files.
/// </summary>
public static class GameSaveSignals
{
    public static void MarkDirty()
    {
        GameSaveDirtyTracker.MarkDirty();
        GameProgressCounter.InvalidateCache();
    }

    public static void NotifyMilestone()
    {
        GameSaveDirtyTracker.MarkDirty();
        GameProgressCounter.InvalidateCache();
        GameSaveManager.RequestMilestoneAutosave();
    }
}
