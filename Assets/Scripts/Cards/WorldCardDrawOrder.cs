/// <summary>
/// Stable draw / stack ordering helpers for <see cref="WorldCard"/> instances.
/// </summary>
public static class WorldCardDrawOrder
{
    public static int CompareStableInstanceId(WorldCard a, WorldCard b)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a == null)
            return -1;
        if (b == null)
            return 1;

        return a.GetInstanceID().CompareTo(b.GetInstanceID());
    }
}
