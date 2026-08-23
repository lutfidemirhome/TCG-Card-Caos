using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Collects pure save DTOs on the main thread. O(n) over cards/packs/shelves.
/// </summary>
public static class GameSaveWorldCollector
{
    static readonly List<CardSaveRecord> CardScratch = new List<CardSaveRecord>(512);
    static readonly List<PackSaveRecord> PackScratch = new List<PackSaveRecord>(16);
    static readonly List<WorldCard> HeldCardScratch = new List<WorldCard>(10);
    static readonly List<WorldBoosterPack> HeldPackScratch = new List<WorldBoosterPack>(4);
    static readonly HashSet<WorldCard> HeldCardSet = new HashSet<WorldCard>();
    static readonly HashSet<WorldBoosterPack> HeldPackSet = new HashSet<WorldBoosterPack>();
    static readonly Dictionary<WorldCard, CardShelfSlot> OccupiedShelfSlotByCard =
        new Dictionary<WorldCard, CardShelfSlot>(64);
    static readonly Dictionary<WorldCard, PsaCabinetSlot> OccupiedPsaSlotByCard =
        new Dictionary<WorldCard, PsaCabinetSlot>(16);

    public static GameSaveData Collect(string slotId, SaveSlotType slotType, int slotIndex)
    {
        PersistentIdRegistry.RebuildWorldLookups();

        CardScratch.Clear();
        PackScratch.Clear();
        HeldCardScratch.Clear();
        HeldPackScratch.Clear();
        HeldCardSet.Clear();
        HeldPackSet.Clear();
        BuildOccupiedShelfSlotMap();
        BuildOccupiedPsaSlotMap();

        PlayerCardHand hand = PlayerCardHand.Instance;
        if (hand != null)
        {
            hand.CopyHeldCards(HeldCardScratch);
            hand.CopyHeldPacks(HeldPackScratch);
            for (int i = 0; i < HeldCardScratch.Count; i++)
            {
                if (HeldCardScratch[i] != null)
                    HeldCardSet.Add(HeldCardScratch[i]);
            }

            for (int i = 0; i < HeldPackScratch.Count; i++)
            {
                if (HeldPackScratch[i] != null)
                    HeldPackSet.Add(HeldPackScratch[i]);
            }
        }

        WorldCard[] cards = Object.FindObjectsByType<WorldCard>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cards.Length; i++)
        {
            CardSaveRecord record = CollectCard(cards[i], HeldCardSet);
            if (record != null)
                CardScratch.Add(record);
        }

        WorldBoosterPack[] packs = Object.FindObjectsByType<WorldBoosterPack>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < packs.Length; i++)
        {
            PackSaveRecord record = CollectPack(packs[i], HeldPackSet);
            if (record != null)
                PackScratch.Add(record);
        }

        CountProgress(out int cardsPlaced, out int totalShelves, out int shelvesCompleted,
            out int totalCabinets, out int cabinetsCompleted, out int totalCards);

        var data = new GameSaveData
        {
            saveVersion = GameSaveSettings.CurrentSaveVersion,
            slotId = slotId,
            slotType = slotType,
            slotIndex = slotIndex,
            buildVariant = GameBuildVariant.Current,
            worldId = GameScenes.Game,
            timestampUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            playTimeSeconds = GamePlayTime.TotalSeconds,
            cardsPlaced = cardsPlaced,
            totalCards = totalCards,
            shelvesCompleted = shelvesCompleted,
            totalShelves = totalShelves,
            cabinetsCompleted = cabinetsCompleted,
            totalCabinets = totalCabinets,
            handSelectedIndex = hand != null ? hand.SelectedIndex : 0,
            cards = CardScratch.ToArray(),
            packs = PackScratch.ToArray(),
        };

        return data;
    }

    static CardSaveRecord CollectCard(WorldCard card, HashSet<WorldCard> heldCards)
    {
        if (card == null)
            return null;

        PersistentId.GetOrCreate(card.gameObject);
        var record = new CardSaveRecord
        {
            id = PersistentId.Resolve(card),
            definitionId = card.Definition != null ? card.Definition.DefinitionId : string.Empty,
            psaSlot = card.PsaSlotNumber,
            psaVariant = card.PsaVariantIndex,
            palette = card.PaletteIndex,
            faceDown = card.IsGroundFaceDown,
            stackLayer = card.GroundStackLayer,
        };
        record.SetPosition(card.transform.position);
        record.SetRotation(card.transform.rotation);

        if (heldCards.Contains(card) || card.IsInHand)
        {
            record.location = CardRuntimeLocation.Held;
            return record;
        }

        if (OccupiedShelfSlotByCard.TryGetValue(card, out CardShelfSlot occupiedSlot))
        {
            ApplyShelfRecord(record, card, occupiedSlot);
            return record;
        }

        CardShelfSlot parentSlot = card.GetComponentInParent<CardShelfSlot>();
        if (parentSlot != null)
        {
            ApplyShelfRecord(record, card, parentSlot);
            return record;
        }

        if (OccupiedPsaSlotByCard.TryGetValue(card, out PsaCabinetSlot occupiedPsaSlot))
        {
            ApplyPsaRecord(record, occupiedPsaSlot);
            return record;
        }

        PsaCabinetSlot psaSlot = card.GetComponentInParent<PsaCabinetSlot>();
        if (psaSlot != null)
        {
            ApplyPsaRecord(record, psaSlot);
            return record;
        }

        record.location = CardRuntimeLocation.World;
        return record;
    }

    static PackSaveRecord CollectPack(WorldBoosterPack pack, HashSet<WorldBoosterPack> heldPacks)
    {
        if (pack == null || pack.State == WorldBoosterPack.PackState.Opening)
            return null;

        PersistentId.GetOrCreate(pack.gameObject);
        IReadOnlyList<CardDefinition> contents = pack.PeekPreRolledContents();
        string[] ids = System.Array.Empty<string>();
        if (contents != null && contents.Count > 0)
        {
            ids = new string[contents.Count];
            for (int i = 0; i < contents.Count; i++)
                ids[i] = contents[i] != null ? contents[i].DefinitionId : string.Empty;
        }

        var record = new PackSaveRecord
        {
            id = PersistentId.Resolve(pack),
            variant = pack.PackVariantIndex,
            held = heldPacks.Contains(pack) || pack.IsInHand,
            faceDown = pack.GroundShowsBack,
            stackLayer = pack.GroundStackLayer,
            contents = ids,
        };
        record.SetPosition(pack.transform.position);
        record.SetRotation(pack.transform.rotation);
        return record;
    }

    static PsaCabinet ResolvePsaCabinet(PsaCabinetSlot slot)
    {
        if (slot == null)
            return null;

        PsaCabinet cabinet = slot.GetComponentInParent<PsaCabinet>();
        if (cabinet != null)
            return cabinet;

        foreach (PsaCabinet candidate in PersistentIdRegistry.AllPsaCabinets)
        {
            if (candidate == null)
                continue;

            PsaCabinetSlot[] slots = candidate.Slots;
            if (slots == null)
            {
                candidate.CollectSlots();
                slots = candidate.Slots;
            }

            if (slots == null)
                continue;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == slot)
                    return candidate;
            }
        }

        return Object.FindFirstObjectByType<PsaCabinet>();
    }

    public static void CountProgress(
        out int cardsPlaced,
        out int totalShelves,
        out int shelvesCompleted,
        out int totalCabinets,
        out int cabinetsCompleted)
    {
        CountProgress(
            out cardsPlaced,
            out totalShelves,
            out shelvesCompleted,
            out totalCabinets,
            out cabinetsCompleted,
            out _);
    }

    public static void CountProgress(
        out int cardsPlaced,
        out int totalShelves,
        out int shelvesCompleted,
        out int totalCabinets,
        out int cabinetsCompleted,
        out int totalCards)
    {
        cardsPlaced = 0;
        shelvesCompleted = 0;
        cabinetsCompleted = 0;
        totalShelves = GameHudLimits.MaxShelves;
        totalCabinets = GameHudLimits.MaxShelves;
        totalCards = CountOwnedCards();

        var countedPsaRoots = new HashSet<Transform>();

        CardShelf[] shelves = Object.FindObjectsByType<CardShelf>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < shelves.Length; i++)
        {
            CardShelf shelf = shelves[i];
            if (shelf == null)
                continue;

            cardsPlaced += shelf.CountCorrectlyPlacedCards();
            if (shelf.IsComplete())
                shelvesCompleted++;
        }

        PsaCabinet[] cabinets = Object.FindObjectsByType<PsaCabinet>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < cabinets.Length; i++)
        {
            PsaCabinet cabinet = cabinets[i];
            if (cabinet == null || !countedPsaRoots.Add(cabinet.transform))
                continue;

            cardsPlaced += cabinet.CountCorrectlyPlacedCards();
            if (cabinet.IsComplete())
            {
                shelvesCompleted++;
                cabinetsCompleted++;
            }
        }

        CountOrphanPsaUnits(countedPsaRoots, ref cardsPlaced, ref shelvesCompleted, ref cabinetsCompleted);
    }

    static int CountOwnedCards()
    {
        int total = 0;
        WorldCard[] cards = Object.FindObjectsByType<WorldCard>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] != null)
                total++;
        }

        WorldBoosterPack[] packs = Object.FindObjectsByType<WorldBoosterPack>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < packs.Length; i++)
        {
            WorldBoosterPack pack = packs[i];
            if (pack == null || pack.State == WorldBoosterPack.PackState.Opening)
                continue;

            IReadOnlyList<CardDefinition> contents = pack.PeekPreRolledContents();
            if (contents != null && contents.Count > 0)
            {
                total += contents.Count;
                continue;
            }

            total += CardDimensions.CardsPerBoosterPack;
        }

        return total;
    }

    static void CountOrphanPsaUnits(
        HashSet<Transform> countedRoots,
        ref int cardsPlaced,
        ref int shelvesCompleted,
        ref int cabinetsCompleted)
    {
        PsaCabinetSlot[] slots = Object.FindObjectsByType<PsaCabinetSlot>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot == null || !PsaArtLibrary.IsCabinetSlotNumber(slot.SlotNumber))
                continue;

            Transform root = ResolvePsaUnitRoot(slot);
            if (root == null || !countedRoots.Add(root))
                continue;

            PsaCabinetSlot[] unitSlots = root.GetComponentsInChildren<PsaCabinetSlot>(true);
            cardsPlaced += CountCorrectPsaSlots(unitSlots);
            if (IsPsaSlotGroupComplete(unitSlots))
            {
                shelvesCompleted++;
                cabinetsCompleted++;
            }
        }
    }

    static Transform ResolvePsaUnitRoot(PsaCabinetSlot slot)
    {
        if (slot == null)
            return null;

        PsaCabinet cabinet = slot.GetComponentInParent<PsaCabinet>();
        if (cabinet != null)
            return cabinet.transform;

        Transform current = slot.transform;
        Transform best = current;
        while (current != null)
        {
            if (CountCabinetSlotsUnder(current) > 0)
                best = current;
            current = current.parent;
        }

        return best;
    }

    static int CountCabinetSlotsUnder(Transform root)
    {
        if (root == null)
            return 0;

        PsaCabinetSlot[] slots = root.GetComponentsInChildren<PsaCabinetSlot>(true);
        int count = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot != null && PsaArtLibrary.IsCabinetSlotNumber(slot.SlotNumber))
                count++;
        }

        return count;
    }

    static int CountCorrectPsaSlots(PsaCabinetSlot[] slots)
    {
        int count = 0;
        if (slots == null)
            return 0;

        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot == null || slot.IsEmpty || !PsaArtLibrary.IsCabinetSlotNumber(slot.SlotNumber))
                continue;

            WorldCard card = slot.OccupiedCard;
            if (card != null && !card.IsInHand && slot.IsCorrectPlacement(card))
                count++;
        }

        return count;
    }

    static bool IsPsaSlotGroupComplete(PsaCabinetSlot[] slots)
    {
        bool hasCabinetSlot = false;
        if (slots == null)
            return false;

        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot == null || !PsaArtLibrary.IsCabinetSlotNumber(slot.SlotNumber))
                continue;

            hasCabinetSlot = true;
            WorldCard card = slot.OccupiedCard;
            if (slot.IsEmpty || card == null || card.IsInHand || !slot.IsCorrectPlacement(card))
                return false;
        }

        return hasCabinetSlot;
    }

    static void BuildOccupiedShelfSlotMap()
    {
        OccupiedShelfSlotByCard.Clear();
        CardShelfSlot[] slots = Object.FindObjectsByType<CardShelfSlot>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < slots.Length; i++)
        {
            CardShelfSlot slot = slots[i];
            if (slot == null || slot.IsEmpty)
                continue;

            WorldCard card = slot.OccupiedCard;
            if (card != null)
                OccupiedShelfSlotByCard[card] = slot;
        }
    }

    static void BuildOccupiedPsaSlotMap()
    {
        OccupiedPsaSlotByCard.Clear();
        PsaCabinetSlot[] slots = Object.FindObjectsByType<PsaCabinetSlot>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot == null || slot.IsEmpty)
                continue;

            WorldCard card = slot.OccupiedCard;
            // Skip stale occupancy once the card has left the holder (hand / world throw).
            if (card == null || card.IsInHand)
                continue;

            // Keep in-flight placements (Occupy runs before parent) and seated cards only.
            if (!card.IsFlyingToShelf && card.GetComponentInParent<PsaCabinetSlot>() != slot)
                continue;

            OccupiedPsaSlotByCard[card] = slot;
        }
    }

    static void ApplyPsaRecord(CardSaveRecord record, PsaCabinetSlot psaSlot)
    {
        if (record == null || psaSlot == null)
            return;

        PsaCabinet cabinet = ResolvePsaCabinet(psaSlot);
        if (cabinet != null)
            PersistentId.GetOrCreate(cabinet.gameObject);

        record.location = CardRuntimeLocation.PsaCabinet;
        record.psaCabinetId = cabinet != null ? PersistentId.Resolve(cabinet) : string.Empty;
        record.psaCabinetSlot = psaSlot.SlotNumber;
    }

    static void ApplyShelfRecord(CardSaveRecord record, WorldCard card, CardShelfSlot shelfSlot)
    {
        if (record == null || card == null || shelfSlot == null)
            return;

        CardShelf shelf = shelfSlot.GetComponentInParent<CardShelf>();
        if (shelf != null)
            PersistentId.GetOrCreate(shelf.gameObject);

        shelfSlot.SyncIndicesFromHierarchy();
        record.location = CardRuntimeLocation.Shelf;
        record.shelfId = shelf != null
            ? PersistentId.BuildPathFallback(shelf.transform)
            : string.Empty;
        record.slotRow = shelfSlot.RowIndex;
        record.slotColumn = shelfSlot.ColumnIndex;
        record.shelfSlotName = shelfSlot.gameObject.name;
        record.shelfSlotPath = BuildChildPath(shelf != null ? shelf.transform : null, shelfSlot.transform);
        SetShelfRecordPose(record, shelfSlot, shelf);
    }

    static void SetShelfRecordPose(CardSaveRecord record, CardShelfSlot shelfSlot, CardShelf shelf)
    {
        if (record == null || shelfSlot == null)
            return;

        float padding = shelf != null ? shelf.SurfacePadding : 0.003f;
        float halfHeight = CardDimensions.Height * CardDimensions.WorldCardScale * 0.5f;
        Transform slotTransform = shelfSlot.transform;
        record.SetPosition(slotTransform.position + slotTransform.up * (halfHeight + padding));
        record.SetRotation(slotTransform.rotation);
    }

    static string BuildChildPath(Transform root, Transform child)
    {
        if (root == null || child == null || !child.IsChildOf(root))
            return string.Empty;

        var parts = new System.Collections.Generic.List<string>(8);
        Transform current = child;
        while (current != null && current != root)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        if (current != root)
            return string.Empty;

        parts.Reverse();
        return string.Join("/", parts);
    }
}
