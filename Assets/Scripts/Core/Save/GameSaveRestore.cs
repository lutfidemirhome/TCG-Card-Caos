using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Physics-safe world restore. Spawns cards settled; does not wake Rigidbodies.
/// </summary>
public static class GameSaveRestore
{
    const int EntitiesPerFrame = 24;

    static readonly HashSet<string> RestoredIds = new HashSet<string>();
    static bool _remappedIds;
    static int _shelfRestored;
    static int _shelfFailed;
    static int _psaRestored;
    public static bool LastRestoreSucceeded { get; private set; }

    public static IEnumerator RestoreRoutine(string slotId)
    {
        LastRestoreSucceeded = false;
        GameSaveEvents.RaiseLoadStarted(slotId);

        if (!SaveFileIO.TryLoadSave(slotId, out GameSaveData data, out string error))
        {
            GameSaveEvents.RaiseLoadFailed(error ?? "Save could not be loaded.");
            yield break;
        }

        CardScatterUtility.ClearTestCards();
        yield return null;

        PersistentIdRegistry.RebuildWorldLookups();
        PrepareShelvesForRestore();
        Transform scatterRoot = CardScatterUtility.GetOrCreateScatterRoot();
        RestoredIds.Clear();
        _remappedIds = false;
        _shelfRestored = 0;
        _shelfFailed = 0;
        _psaRestored = 0;

        int processed = 0;
        if (data.cards != null)
        {
            for (int i = 0; i < data.cards.Length; i++)
            {
                RestoreCard(data.cards[i], scatterRoot);
                processed++;
                if (processed % EntitiesPerFrame == 0)
                    yield return null;
            }
        }

        if (data.packs != null)
        {
            for (int i = 0; i < data.packs.Length; i++)
            {
                RestorePack(data.packs[i], scatterRoot);
                processed++;
                if (processed % EntitiesPerFrame == 0)
                    yield return null;
            }
        }

        FinalizeShelfRestores();
        yield return null;

        PlayerCardHand hand = PlayerCardHand.Instance;
        if (hand != null)
            hand.RestoreSelectionIndex(data.handSelectedIndex);

        GamePlayTime.BeginSession(data.playTimeSeconds);
        PersistentIdRegistry.RebuildWorldLookups();
        if (_remappedIds)
            GameSaveDirtyTracker.MarkDirty();
        else
            GameSaveDirtyTracker.Clear();
        LastRestoreSucceeded = true;
        GameSaveEvents.RaiseLoadCompleted(slotId);
        LogRestore(slotId, data);
    }

    static void LogRestore(string slotId, GameSaveData data)
    {
        int shelfCards = 0;
        int psaCards = 0;
        if (data != null && data.cards != null)
        {
            for (int i = 0; i < data.cards.Length; i++)
            {
                CardSaveRecord card = data.cards[i];
                if (card == null)
                    continue;
                if (card.location == CardRuntimeLocation.Shelf)
                    shelfCards++;
                else if (card.location == CardRuntimeLocation.PsaCabinet)
                    psaCards++;
            }
        }

        Debug.Log(
            "[Save] Restored " + slotId
            + " shelf=" + _shelfRestored + "/" + shelfCards
            + (_shelfFailed > 0 ? " missing=" + _shelfFailed : string.Empty)
            + " psa=" + _psaRestored + "/" + psaCards
            + " total=" + (data != null && data.cards != null ? data.cards.Length : 0));
    }

    static string AllocateRestoreId(string savedId)
    {
        if (!string.IsNullOrEmpty(savedId) && RestoredIds.Add(savedId))
            return savedId;

        _remappedIds = true;
        string generated = System.Guid.NewGuid().ToString("N");
        RestoredIds.Add(generated);
        return generated;
    }

    static void RestoreCard(CardSaveRecord record, Transform scatterRoot)
    {
        if (record == null)
            return;

        string restoreId = AllocateRestoreId(record.id);

        WorldCard card = CreateCard(record);
        if (card == null)
            return;

        PersistentId.GetOrCreate(card.gameObject).AssignExisting(restoreId);

        switch (record.location)
        {
            case CardRuntimeLocation.Shelf:
                if (TryRestoreShelfCard(card, record))
                    _shelfRestored++;
                else
                {
                    _shelfFailed++;
                    PlaceWorldCard(card, record, scatterRoot);
                }
                break;
            case CardRuntimeLocation.PsaCabinet:
                if (TryRestorePsaCard(card, record))
                    _psaRestored++;
                else
                    PlaceWorldCard(card, record, scatterRoot);
                break;
            case CardRuntimeLocation.Held:
                if (!TryRestoreHeldCard(card))
                    PlaceWorldCard(card, record, scatterRoot);
                break;
            default:
                PlaceWorldCard(card, record, scatterRoot);
                break;
        }
    }

    static WorldCard CreateCard(CardSaveRecord record)
    {
        if (record.psaSlot > 0)
        {
            return CardFactory.CreateWorldPsaCard(
                record.Position,
                record.Rotation,
                record.psaSlot,
                record.psaVariant);
        }

        if (string.IsNullOrEmpty(record.definitionId)
            || !CardCatalog.TryGetById(record.definitionId, out CardDefinition definition))
        {
            Debug.LogWarning("[Save] Missing card definition '" + record.definitionId + "'.");
            return null;
        }

        return CardFactory.CreateWorldCard(
            record.Position,
            record.Rotation,
            definition,
            record.palette,
            ensureArtLoaded: false);
    }

    static void PlaceWorldCard(WorldCard card, CardSaveRecord record, Transform scatterRoot)
    {
        card.transform.SetParent(scatterRoot, true);
        card.transform.SetPositionAndRotation(record.Position, record.Rotation);
        card.transform.localScale = Vector3.one * CardDimensions.GroundCardScale;
        card.SetGroundShowsBack(record.faceDown);
        card.SetGroundStackLayer(record.stackLayer);
    }

    static bool TryRestoreShelfCard(WorldCard card, CardSaveRecord record)
    {
        CardArtLibrary.EnsureLoaded();
        CardShelfSlot slot = FindShelfSlot(record);
        if (slot == null)
        {
            Debug.LogWarning(
                "[Save] Missing shelf slot '" + record.shelfId
                + "' path='" + record.shelfSlotPath
                + "' r" + record.slotRow + " c" + record.slotColumn + ".");
            return false;
        }

        CardShelf owner = slot.GetComponentInParent<CardShelf>();
        float padding = owner != null ? owner.SurfacePadding : 0.003f;
        bool isCorrect = owner != null && owner.IsCorrectPlacement(card, slot);
        // Skip green/red flash on load — StartCoroutine during bulk restore is noisy and
        // RefreshRenderMode at flash end is unnecessary when we re-finalize visuals after.
        return slot.RestoreOccupiedCard(card, padding, isCorrect, playPlacementFeedback: false);
    }

    static void PrepareShelvesForRestore()
    {
        CardShelf[] shelves = UnityEngine.Object.FindObjectsByType<CardShelf>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < shelves.Length; i++)
        {
            if (shelves[i] != null)
                shelves[i].RefreshSlotCache();
        }
    }

    static CardShelfSlot FindShelfSlot(CardSaveRecord record)
    {
        CardShelf shelf = FindShelf(record.shelfId);
        if (shelf == null)
            return null;

        return ResolveSlotOnShelf(shelf, record);
    }

    static CardShelfSlot ResolveSlotOnShelf(CardShelf shelf, CardSaveRecord record)
    {
        if (shelf == null)
            return null;

        if (!string.IsNullOrEmpty(record.shelfSlotPath))
        {
            CardShelfSlot byPath = shelf.FindSlotByRelativePath(record.shelfSlotPath);
            if (byPath != null)
                return byPath;
        }

        if (!string.IsNullOrEmpty(record.shelfSlotName))
        {
            CardShelfSlot byName = shelf.FindSlotByRelativePath(record.shelfSlotName);
            if (byName != null)
                return byName;
        }

        CardShelfSlot authored = shelf.FindSlotByAuthoredHierarchy(
            record.slotRow,
            record.slotColumn);
        if (authored != null)
            return authored;

        return shelf.FindSlotForRestore(null, record.slotRow, record.slotColumn, record.Position);
    }

    static void FinalizeShelfRestores()
    {
        CardShelfSlot[] slots = UnityEngine.Object.FindObjectsByType<CardShelfSlot>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < slots.Length; i++)
        {
            CardShelfSlot slot = slots[i];
            if (slot == null || slot.IsEmpty)
                continue;

            WorldCard card = slot.OccupiedCard;
            if (card == null)
                continue;

            CardShelf shelf = slot.GetComponentInParent<CardShelf>();
            float padding = shelf != null ? shelf.SurfacePadding : 0.003f;
            bool isCorrect = shelf != null && shelf.IsCorrectPlacement(card, slot);
            slot.RestoreOccupiedCard(card, padding, isCorrect, playPlacementFeedback: false);
            card.RefreshShelfVisualAfterLoad();
        }
    }

    static CardShelf FindShelf(string shelfId)
    {
        if (string.IsNullOrEmpty(shelfId))
            return null;

        if (PersistentIdRegistry.TryGetShelf(shelfId, out CardShelf shelf) && shelf != null)
            return shelf;

        CardShelf[] shelves = UnityEngine.Object.FindObjectsByType<CardShelf>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < shelves.Length; i++)
        {
            CardShelf candidate = shelves[i];
            if (candidate == null)
                continue;

            string path = PersistentId.BuildPathFallback(candidate.transform);
            if (path == shelfId)
                return candidate;
        }

        string objectName = ShelfObjectName(shelfId);
        if (string.IsNullOrEmpty(objectName))
            return null;

        for (int i = 0; i < shelves.Length; i++)
        {
            CardShelf candidate = shelves[i];
            if (candidate != null && candidate.gameObject.name == objectName)
                return candidate;
        }

        return null;
    }

    static string ShelfObjectName(string shelfId)
    {
        if (string.IsNullOrEmpty(shelfId))
            return string.Empty;

        int slash = shelfId.LastIndexOf('/');
        return slash >= 0 && slash < shelfId.Length - 1
            ? shelfId.Substring(slash + 1)
            : shelfId;
    }

    static bool TryRestorePsaCard(WorldCard card, CardSaveRecord record)
    {
        int slotNumber = record.psaCabinetSlot > 0
            ? record.psaCabinetSlot
            : record.psaSlot;

        PsaCabinetSlot slot = FindPsaSlot(record.psaCabinetId, slotNumber);
        if (slot == null)
        {
            Debug.LogWarning(
                "[Save] Missing PSA slot " + slotNumber
                + " (cabinet '" + record.psaCabinetId + "').");
            return false;
        }

        return slot.RestoreOccupiedCard(card);
    }

    static PsaCabinetSlot FindPsaSlot(string cabinetId, int slotNumber)
    {
        if (!string.IsNullOrEmpty(cabinetId)
            && PersistentIdRegistry.TryGetPsaCabinet(cabinetId, out PsaCabinet cabinet)
            && cabinet != null)
        {
            PsaCabinetSlot slot = cabinet.FindSlot(slotNumber);
            if (slot != null)
                return slot;
        }

        foreach (PsaCabinet candidate in PersistentIdRegistry.AllPsaCabinets)
        {
            if (candidate == null)
                continue;

            PsaCabinetSlot slot = candidate.FindSlot(slotNumber);
            if (slot != null)
                return slot;
        }

        PsaCabinetSlot[] slots = UnityEngine.Object.FindObjectsByType<PsaCabinetSlot>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        PsaCabinetSlot occupiedFallback = null;
        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot == null || slot.SlotNumber != slotNumber)
                continue;

            if (slot.IsEmpty)
                return slot;

            if (occupiedFallback == null)
                occupiedFallback = slot;
        }

        return occupiedFallback;
    }

    static bool TryRestoreHeldCard(WorldCard card)
    {
        PlayerCardHand hand = PlayerCardHand.Instance;
        if (hand == null)
            return false;

        return hand.RestoreHeldCard(card);
    }

    static void RestorePack(PackSaveRecord record, Transform scatterRoot)
    {
        if (record == null)
            return;

        string restoreId = AllocateRestoreId(record.id);

        List<CardDefinition> contents = ResolvePackContents(record.contents);
        WorldBoosterPack pack = PackFactory.CreateWorldPack(
            record.Position,
            record.Rotation,
            packDefinition: null,
            packName: "Booster Pack",
            packVariantIndex: record.variant,
            preRolledContents: contents);

        PersistentId.GetOrCreate(pack.gameObject).AssignExisting(restoreId);

        if (record.held && PlayerCardHand.Instance != null && PlayerCardHand.Instance.RestoreHeldPack(pack))
            return;

        pack.transform.SetParent(scatterRoot, true);
        pack.transform.SetPositionAndRotation(record.Position, record.Rotation);
        pack.SetGroundShowsBack(record.faceDown);
        pack.SetGroundStackLayer(record.stackLayer);
        CardGroundStack.TrackPack(pack);
    }

    static List<CardDefinition> ResolvePackContents(string[] ids)
    {
        var contents = new List<CardDefinition>(ids != null ? ids.Length : 0);
        if (ids == null)
            return contents;

        for (int i = 0; i < ids.Length; i++)
        {
            if (CardCatalog.TryGetById(ids[i], out CardDefinition definition))
                contents.Add(definition);
        }

        return contents;
    }
}
