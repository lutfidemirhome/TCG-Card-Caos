using System.Collections.Generic;
using UnityEngine;

public static class CoopPlayerColors
{
    static readonly Color[] Colors = { new Color(.24f,.77f,1f), new Color(1f,.66f,.28f),
        new Color(.47f,.9f,.48f), new Color(.96f,.48f,.83f) };

    public static Color For(ulong id, IReadOnlyList<ulong> roster)
    {
        int rank = 0;
        foreach (ulong peer in roster) if (peer < id) rank++;
        return Colors[rank % Colors.Length];
    }
}
