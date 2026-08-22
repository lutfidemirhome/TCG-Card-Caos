/// <summary>
/// Cheap dirty flag. Gameplay only sets this; disk writes happen later.
/// </summary>
public static class GameSaveDirtyTracker
{
    public static bool IsDirty { get; private set; }

    public static void MarkDirty()
    {
        IsDirty = true;
    }

    public static bool Consume()
    {
        bool dirty = IsDirty;
        IsDirty = false;
        return dirty;
    }

    public static void Clear()
    {
        IsDirty = false;
    }
}
