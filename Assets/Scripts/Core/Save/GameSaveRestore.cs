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
        Transform scatterRoot = CardScatterUtility.GetOrCreateScatterRoot();
        RestoredIds.Clear();
        _remappedIds = false;

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
                if (!TryRestoreShelfCard(card, record))
                    PlaceWorldCard(card, record, scatterRoot);
                break;
            case CardRuntimeLocation.PsaCabinet:
                if (!TryRestorePsaCard(card, record))
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
        card.PlaceOnSurface(scatterRoot, record.Position, record.Rotation);
        card.SetGroundShowsBack(record.faceDown);
        card.SetGroundStackLayer(record.stackLayer);
    }

    static bool TryRestoreShelfCard(WorldCard card, CardSaveRecord record)
    {
        CardShelf shelf = FindShelf(record.shelfId);
        if (shelf == null)
        {
            Debug.LogWarning("[Save] Missing shelf '" + record.shelfId + "'.");
            return false;
        }

        return shelf.TryRestoreCard(card, record.slotRow, record.slotColumn);
    }

    static CardShelf FindShelf(string shelfId)
    {
        if (!string.IsNullOrEmpty(shelfId)
            && PersistentIdRegistry.TryGetShelf(shelfId, out CardShelf shelf)
            && shelf != null)
            return shelf;

        string objectName = ShelfObjectName(shelfId);
        if (string.IsNullOrEmpty(objectName))
            return null;

        foreach (CardShelf candidate in PersistentIdRegistry.AllShelves)
        {
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

        PsaCabinetSlot[] slots = Object.FindObjectsByType<PsaCabinetSlot>(
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
