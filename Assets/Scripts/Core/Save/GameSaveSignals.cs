/// <summary>
/// Cheap gameplay hooks. Does not serialize or write files.
/// </summary>
public static class GameSaveSignals
{
    public static void MarkDirty()
    {
        GameSaveDirtyTracker.MarkDirty();
    }

    public static void NotifyMilestone()
    {
        GameSaveDirtyTracker.MarkDirty();
        GameSaveManager.RequestMilestoneAutosave();
    }
}
