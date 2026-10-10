using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Low resolution world art. Original definition textures remain authoritative for close views.</summary>
public sealed class CardWorldAtlas : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string definitionId;
        public int page;
        public Vector4 uvRect;
    }

    public Material[] pages;
    public Entry[] entries;
    Dictionary<string, Entry> _lookup;

    public bool TryGet(string id, out Entry entry)
    {
        if (_lookup == null)
        {
            _lookup = new Dictionary<string, Entry>(entries.Length, StringComparer.Ordinal);
            foreach (Entry item in entries) _lookup[item.definitionId] = item;
        }
        return _lookup.TryGetValue(id, out entry);
    }
}
