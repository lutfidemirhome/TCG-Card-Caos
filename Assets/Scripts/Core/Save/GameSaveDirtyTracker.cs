/// <summary>
/// Cheap dirty flag. Gameplay only sets this; disk writes happen later.
/// </summary>
public static class GameSaveDirtyTracker
{
    public static bool IsDirty { get; private set; }
    public static ulong Revision { get; private set; }

    public static void MarkDirty()
    {
        AdvanceRevision();
        IsDirty = true;
    }

    public static bool Consume()
    {
        bool dirty = IsDirty;
        Clear();
        return dirty;
    }

    public static void Clear()
    {
        // Resetting for a new/restored world also invalidates any older in-flight
        // save. Do not reset the revision to zero when the gameplay session changes.
        AdvanceRevision();
        IsDirty = false;
    }

    public static bool ClearIfUnchanged(ulong savedRevision)
    {
        if (Revision != savedRevision)
            return false;

        Clear();
        return true;
    }

    static void AdvanceRevision()
    {
        unchecked { Revision++; }
    }
}
