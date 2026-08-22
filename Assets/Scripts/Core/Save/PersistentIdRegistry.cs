using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// O(1) lookup for persistent entities. Rebuilt at save/load, not every frame.
/// </summary>
public static class PersistentIdRegistry
{
    static readonly Dictionary<string, PersistentId> Ids = new Dictionary<string, PersistentId>(256);
    static readonly Dictionary<string, WorldCard> Cards = new Dictionary<string, WorldCard>(2048);
    static readonly Dictionary<string, WorldBoosterPack> Packs = new Dictionary<string, WorldBoosterPack>(64);
    static readonly Dictionary<string, CardShelf> Shelves = new Dictionary<string, CardShelf>(128);
    static readonly List<CardShelf> UniqueShelves = new List<CardShelf>(64);
    static readonly Dictionary<string, PsaCabinet> PsaCabinets = new Dictionary<string, PsaCabinet>(8);

    public static void Register(PersistentId persistent)
    {
        if (persistent == null || !persistent.HasValue)
            return;

        if (Ids.TryGetValue(persistent.Value, out PersistentId existing)
            && existing != null
            && existing != persistent)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[Save] Duplicate PersistentId '" + persistent.Value + "' on '"
                + persistent.gameObject.name + "'. Other object: '"
                + existing.gameObject.name + "'.",
                persistent);
#endif
            return;
        }

        Ids[persistent.Value] = persistent;
    }

    public static void Unregister(PersistentId persistent)
    {
        if (persistent == null || !persistent.HasValue)
            return;

        if (Ids.TryGetValue(persistent.Value, out PersistentId existing) && existing == persistent)
            Ids.Remove(persistent.Value);
    }

    public static void RebuildWorldLookups()
    {
        Cards.Clear();
        Packs.Clear();
        Shelves.Clear();
        UniqueShelves.Clear();
        PsaCabinets.Clear();

        WorldCard[] cards = Object.FindObjectsByType<WorldCard>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cards.Length; i++)
        {
            WorldCard card = cards[i];
            if (card == null)
                continue;
            string id = PersistentId.Resolve(card);
            if (!string.IsNullOrEmpty(id))
                Cards[id] = card;
        }

        WorldBoosterPack[] packs = Object.FindObjectsByType<WorldBoosterPack>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < packs.Length; i++)
        {
            WorldBoosterPack pack = packs[i];
            if (pack == null)
                continue;
            string id = PersistentId.Resolve(pack);
            if (!string.IsNullOrEmpty(id))
                Packs[id] = pack;
        }

        CardShelf[] shelves = Object.FindObjectsByType<CardShelf>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < shelves.Length; i++)
        {
            CardShelf shelf = shelves[i];
            if (shelf == null)
                continue;
            PersistentId.GetOrCreate(shelf.gameObject);
            UniqueShelves.Add(shelf);
            string id = PersistentId.Resolve(shelf);
            if (!string.IsNullOrEmpty(id))
                Shelves[id] = shelf;

            string pathId = PersistentId.BuildPathFallback(shelf.transform);
            if (!string.IsNullOrEmpty(pathId) && pathId != id)
                Shelves[pathId] = shelf;
        }

        PsaCabinet[] cabinets = Object.FindObjectsByType<PsaCabinet>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cabinets.Length; i++)
        {
            PsaCabinet cabinet = cabinets[i];
            if (cabinet == null)
                continue;
            PersistentId.GetOrCreate(cabinet.gameObject);
            string id = PersistentId.Resolve(cabinet);
            if (!string.IsNullOrEmpty(id))
                PsaCabinets[id] = cabinet;
        }
    }

    public static bool TryGetCard(string id, out WorldCard card) => Cards.TryGetValue(id, out card);
    public static bool TryGetPack(string id, out WorldBoosterPack pack) => Packs.TryGetValue(id, out pack);
    public static bool TryGetShelf(string id, out CardShelf shelf) => Shelves.TryGetValue(id, out shelf);
    public static bool TryGetPsaCabinet(string id, out PsaCabinet cabinet) => PsaCabinets.TryGetValue(id, out cabinet);

    public static List<CardShelf> AllShelves => UniqueShelves;
    public static Dictionary<string, PsaCabinet>.ValueCollection AllPsaCabinets => PsaCabinets.Values;
}
