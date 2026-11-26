using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// Only immutable identifiers are shared over the network. Unity instance IDs, object
// enumeration order and printed card names can all differ between players.
internal sealed class CoopShopLayout
{
    public readonly Dictionary<Guid, WorldCard> Cards = new Dictionary<Guid, WorldCard>();
    public readonly Dictionary<WorldCard, Guid> CardIds = new Dictionary<WorldCard, Guid>();
    public readonly Dictionary<Guid, DisplaySlot> Slots = new Dictionary<Guid, DisplaySlot>();
    public readonly Dictionary<Component, Guid> SlotIds = new Dictionary<Component, Guid>();
    readonly Dictionary<Guid, CardDefinition> packContents = new Dictionary<Guid, CardDefinition>();
    readonly Dictionary<Guid, Guid> sourcePacks = new Dictionary<Guid, Guid>();
    readonly Dictionary<WorldBoosterPack, Guid[]> packKeys = new Dictionary<WorldBoosterPack, Guid[]>();
    readonly Dictionary<Guid, Guid[]> products = new Dictionary<Guid, Guid[]>();
    readonly Dictionary<Guid, WorldBoosterPack> packs = new Dictionary<Guid, WorldBoosterPack>();
    readonly Dictionary<WorldCard, Guid> previews = new Dictionary<WorldCard, Guid>();

    public CoopShopLayout()
    {
        using var hash = SHA256.Create();
        var cards = UnityEngine.Object.FindObjectsByType<WorldCard>();
        var seen = new HashSet<Guid>();
        var repeated = new HashSet<Guid>();
        foreach (var card in cards)
            if (Guid.TryParse(PersistentId.Resolve(card), out var id) && !seen.Add(id)) repeated.Add(id);
        foreach (var card in cards)
        {
            if (card.IsPackReveal) continue;
            if (!Guid.TryParse(PersistentId.Resolve(card), out var id) || repeated.Contains(id))
                id = SceneKey(hash, card.transform);
            AddCard(id, card);
        }
        foreach (var slot in UnityEngine.Object.FindObjectsByType<CardShelfSlot>())
            AddSlot(hash, slot, new DisplaySlot(slot, null));
        foreach (var slot in UnityEngine.Object.FindObjectsByType<PsaCabinetSlot>())
            AddSlot(hash, slot, new DisplaySlot(null, slot));
        foreach (var pack in UnityEngine.Object.FindObjectsByType<WorldBoosterPack>())
        {
            var contents = pack.PeekPreRolledContents();
            if (contents == null || contents.Count == 0) continue;
            Guid source = SceneKey(hash, pack.transform);
            var keys = new Guid[contents.Count];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = Hash(hash, source.ToString("N") + "/" + i.ToString(CultureInfo.InvariantCulture));
                packContents.Add(keys[i], contents[i]);
                sourcePacks.Add(keys[i], source);
            }
            packKeys.Add(pack, keys);
            products.Add(source, keys);
            packs.Add(source, pack);
        }
    }

    static Guid SceneKey(HashAlgorithm hash, Transform t)
    {
        Vector3 p = t.position;
        // Several copies of a shelf prefab can have identical hierarchy names.
        string path = PersistentId.BuildPathFallback(t) + string.Format(CultureInfo.InvariantCulture,
            "@{0},{1},{2}", Mathf.RoundToInt(p.x * 1000), Mathf.RoundToInt(p.y * 1000), Mathf.RoundToInt(p.z * 1000));
        return Hash(hash, path);
    }

    static Guid Hash(HashAlgorithm hash, string value)
    {
        var digest = hash.ComputeHash(Encoding.UTF8.GetBytes(value));
        var bytes = new byte[16];
        Array.Copy(digest, bytes, bytes.Length);
        return new Guid(bytes);
    }

    void AddCard(Guid id, WorldCard card)
    {
        Cards.Add(id, card);
        CardIds.Add(card, id);
    }

    void AddSlot(HashAlgorithm hash, Component component, DisplaySlot slot)
    {
        var key = SceneKey(hash, component.transform);
        Slots.Add(key, slot);
        SlotIds.Add(component, key);
    }

    internal readonly struct DisplaySlot
    {
        readonly CardShelfSlot regular;
        readonly PsaCabinetSlot psa;
        public DisplaySlot(CardShelfSlot regular, PsaCabinetSlot psa) { this.regular = regular; this.psa = psa; }
        public Vector3 Position
        {
            get
            {
                if (regular) return regular.transform.position;
                psa.GetPlacementPose(out var position, out _);
                return position;
            }
        }
        public bool IsRegular => regular != null;
        public bool Accepts(WorldCard card) => card && (regular ? !card.UsesPsaSlab : psa.AcceptsPsaCard(card));
        public bool CanPlace(WorldCard card) => Accepts(card) && (regular ? regular.IsEmpty : psa.IsEmpty);

        public void Place(WorldCard card, bool restoring)
        {
            if (regular)
            {
                var shelf = regular.GetComponentInParent<CardShelf>();
                if (restoring) regular.RestoreOccupiedCard(card, shelf.SurfacePadding, shelf.IsCorrectPlacement(card, regular), false);
                else shelf.PlaceCoopCard(card, regular);
            }
            else if (restoring) psa.RestoreOccupiedCard(card, false);
            else psa.PlaceCoopCard(card);
        }

        public void Remove(WorldCard card)
        {
            if (regular)
            {
                if (card.IsFlyingToShelf) regular.GetComponentInParent<CardShelf>().CancelCoopFlight(card);
                regular.ClearIfMatches(card);
            }
            else psa.RemoveCoopCard(card);
        }
    }

    public void BindReveal(WorldBoosterPack pack, int index, WorldCard preview)
    {
        if (packKeys.TryGetValue(pack, out var keys) && index >= 0 && index < keys.Length)
            previews[preview] = keys[index];
    }

    public bool ConsumePreview(WorldCard preview, out Guid id)
    {
        if (!previews.TryGetValue(preview, out id)) return false;
        previews.Remove(preview);
        preview.gameObject.SetActive(false);
        UnityEngine.Object.Destroy(preview.gameObject);
        return true;
    }

    public void DiscardPreview(WorldCard preview) => previews.Remove(preview);

    public bool SourcePack(Guid id, out Guid pack) => sourcePacks.TryGetValue(id, out pack);
    public bool CanRestorePlacement(Guid cardId, Guid slotId)
    {
        if (!Slots.TryGetValue(slotId, out var slot)) return false;
        if (Cards.TryGetValue(cardId, out var card)) return slot.Accepts(card);
        // Unopened packs cannot be shelved. Their individual products are regular cards.
        return slot.IsRegular && packContents.ContainsKey(cardId);
    }
    public Guid[] Products(Guid source) => products[source];
    public bool PackId(WorldBoosterPack pack, out Guid id)
    {
        id = default;
        return !ReferenceEquals(pack, null) && packKeys.TryGetValue(pack, out var keys) && SourcePack(keys[0], out id);
    }
    public bool UnconsumedPack(Guid id) => packs.ContainsKey(id);
    public WorldBoosterPack AvailablePack(Guid id) => packs.TryGetValue(id, out var pack) ? pack : null;

    public void ConsumePack(Guid source)
    {
        if (!packs.TryGetValue(source, out var pack)) return;
        packs.Remove(source);
        if (!pack || pack.State == WorldBoosterPack.PackState.Opening) return;
        if (PlayerCardHand.Instance) PlayerCardHand.Instance.ClearHeldPackReference(pack);
        pack.gameObject.SetActive(false);
        UnityEngine.Object.Destroy(pack.gameObject);
    }

    public WorldCard Resolve(Guid id, Vector3 position)
    {
        if (Cards.TryGetValue(id, out var card)) return card;
        if (!packContents.TryGetValue(id, out var definition) || !definition) return null;
        card = CardFactory.CreateWorldCard(position, Quaternion.identity, definition, 0);
        PersistentId.GetOrCreate(card.gameObject).AssignExisting(id.ToString("N"));
        AddCard(id, card);
        return card;
    }
}
