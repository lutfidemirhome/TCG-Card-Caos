using System.Collections.Generic;
using UnityEngine;

/// <summary>Shares full-resolution streaming requests between hand, reveal and inspect visuals.</summary>
public static class CardTextureStreaming
{
    static readonly Dictionary<Texture2D, int> Owners = new Dictionary<Texture2D, int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        foreach (Texture2D texture in Owners.Keys)
            if (texture != null) texture.ClearRequestedMipmapLevel();
        Owners.Clear();
    }

    public static void AcquireFullDetail(Texture2D texture)
    {
        if (texture == null || !texture.streamingMipmaps) return;
        Owners.TryGetValue(texture, out int count);
        Owners[texture] = count + 1;
        if (count == 0) texture.requestedMipmapLevel = 0;
    }

    public static void ReleaseFullDetail(Texture2D texture)
    {
        // A destroyed Unity object can still be a dictionary key until its owner releases it.
        if (ReferenceEquals(texture, null) || !Owners.TryGetValue(texture, out int count)) return;
        if (count > 1) { Owners[texture] = count - 1; return; }
        Owners.Remove(texture);
        if (texture != null) texture.ClearRequestedMipmapLevel();
    }
}
