using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Physics-safe world restore. Spawns cards settled; does not wake Rigidbodies.
/// </summary>
public static class GameSaveRestore
{
    // Fixed batches keep the overlay responsive without paying a rendered frame
    // for every 24 items in a full save (more than 230 frames).
    const int EntitiesPerFrame = 512;

    static readonly HashSet<string> RestoredIds = new HashSet<string>();
    static bool _remappedIds;
    static int _shelfRestored;
    static int _shelfFailed;
    static int _psaRestored;
    static int _removedFloorDuplicates;
    public static bool LastRestoreSucceeded { get; private set; }

    public static IEnumerator RestoreRoutine(string slotId)
    {
        var workTimer = System.Diagnostics.Stopwatch.StartNew();
        int batchWaits = 0;
        LastRestoreSucceeded = false;
        GameSaveEvents.RaiseLoadStarted(slotId);

        if (!SaveFileIO.TryLoadSave(slotId, out GameSaveData data, out string error))
        {
            GameSaveEvents.RaiseLoadFailed(error ?? "Save could not be loaded.");
            yield break;
        }

        CardScatterUtility.ClearTestCards();
        PhysicsLevelLayout.SuspendAuthoredItemsForSaveRestore();
        workTimer.Stop();
        yield return null;
        workTimer.Start();

        PersistentIdRegistry.RebuildWorldLookups();
        PrepareShelvesForRestore();
        Transform scatterRoot = CardScatterUtility.GetOrCreateScatterRoot();
        // The authored demo root is stable throughout this restore. Resolve it once,
        // rather than searching every scene object again for each card and pack.
        PhysicsLevelLayout layout = PhysicsLevelLayout.FindExisting();
        Transform demoRoot = layout != null ? layout.DemoCardsRoot : null;
        var removedPackDuplicates = layout != null
            ? new HashSet<string>(layout.RemovedPackDuplicateCardIds)
            : new HashSet<string>();
        RestoredIds.Clear();
        _remappedIds = false;
        _shelfRestored = 0;
        _shelfFailed = 0;
        _psaRestored = 0;
        _removedFloorDuplicates = 0;

        int processed = 0;
        if (data.cards != null)
        {
            for (int i = 0; i < data.cards.Length; i++)
            {
                CardSaveRecord record = data.cards[i];
                bool retiredFloorCopy = record != null && record.psaSlot == 0
                    && !string.IsNullOrEmpty(record.id) && removedPackDuplicates.Contains(record.id);
                if (retiredFloorCopy && record.location == CardRuntimeLocation.World)
                {
                    // One-time migration of the removed authored floor copies. Do not
                    // filter by definition: a player can drop a legitimate pack card.
                    _remappedIds = true;
                    _removedFloorDuplicates++;
                }
                else
                {
                    RestoreCard(record, scatterRoot, demoRoot, retiredFloorCopy);
                }
                processed++;
                if (processed % EntitiesPerFrame == 0)
                {
                    batchWaits++;
                    workTimer.Stop();
                    yield return null;
                    workTimer.Start();
                }
            }
        }

        if (data.packs != null)
        {
            for (int i = 0; i < data.packs.Length; i++)
            {
                RestorePack(data.packs[i], scatterRoot, demoRoot);
                processed++;
                if (processed % EntitiesPerFrame == 0)
                {
                    batchWaits++;
                    workTimer.Stop();
                    yield return null;
                    workTimer.Start();
                }
            }
        }

        FinalizeShelfRestores();
        FinalizePsaRestores();
        workTimer.Stop();
        yield return null;
        workTimer.Start();

        if (data.player != null)
        {
            FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>();
            if (player != null)
                player.RestoreSaveState(data.player);
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
        GameProgressCounter.InvalidateCache();
        GameSaveEvents.RaiseLoadCompleted(slotId);
        LogRestore(slotId, data);
        workTimer.Stop();
        Debug.Log($"[Loading] Restore work={workTimer.Elapsed.TotalSeconds:F2}s batch waits={batchWaits}");
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
            + (_removedFloorDuplicates > 0 ? " removedFloorDuplicates=" + _removedFloorDuplicates : string.Empty)
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

    static void RestoreCard(CardSaveRecord record, Transform scatterRoot, Transform demoRoot, bool retiredFloorCopy)
    {
        if (record == null)
            return;

        // Keep cards already collected into a hand/shelf in older saves. Give them
        // a fresh identity so dropping them later does not trigger the floor migration.
        string restoreId = AllocateRestoreId(retiredFloorCopy ? null : record.id);

        WorldCard card;
        if (PersistentIdRegistry.TryGetCard(restoreId, out WorldCard existing) && existing != null)
        {
            bool demoAuthored = IsDemoAuthored(existing, demoRoot);
            if (demoAuthored && record.location == CardRuntimeLocation.World)
                return;

            card = existing;
            if (!demoAuthored)
                WakeRestoredObject(card.transform);
        }
        else
        {
            card = CreateCard(record);
            if (card == null)
                return;

            PersistentId.GetOrCreate(card.gameObject).AssignExisting(restoreId);
        }

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
                record.psaVariant,
                cardSet: (PsaCardSet)record.psaSet);
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

    static void FinalizePsaRestores()
    {
        PsaCabinetSlot[] slots = UnityEngine.Object.FindObjectsByType<PsaCabinetSlot>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot == null || slot.IsEmpty)
                continue;

            WorldCard card = slot.OccupiedCard;
            if (card == null)
                continue;

            slot.RestoreOccupiedCard(card, playPlacementFeedback: false);
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

        PsaCabinetSlot slot = FindPsaSlot(record, slotNumber);
        if (slot == null)
        {
            Debug.LogWarning(
                "[Save] Missing PSA slot " + slotNumber
                + " (cabinet '" + record.psaCabinetId + "').");
            return false;
        }

        return slot.RestoreOccupiedCard(card, playPlacementFeedback: false);
    }

    static PsaCabinetSlot FindPsaSlot(CardSaveRecord record, int slotNumber)
    {
        PsaCabinetSlot[] slots = UnityEngine.Object.FindObjectsByType<PsaCabinetSlot>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        if (!string.IsNullOrEmpty(record.psaSlotPath))
        {
            PsaCabinetSlot exact = null;
            for (int i = 0; i < slots.Length; i++)
            {
                PsaCabinetSlot slot = slots[i];
                if (slot == null || PersistentId.BuildPathFallback(slot.transform) != record.psaSlotPath)
                    continue;

                // Ambiguous hierarchy names must not silently select a different seat.
                if (exact != null)
                    return null;
                exact = slot;
            }

            return exact != null && exact.IsEmpty ? exact : null;
        }

        // Legacy saves have only a grade and a possibly shared prefab cabinet ID.
        // Recover from the saved world position, never the first matching grade.
        // If the old position cannot identify a nearby seat, keep the card in the
        // world at its saved position instead of moving it to an unrelated cabinet.
        const float MaxLegacySeatDistance = 0.5f;
        float bestDistanceSq = MaxLegacySeatDistance * MaxLegacySeatDistance;
        PsaCabinetSlot nearest = null;
        bool ambiguous = false;
        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot == null || slot.SlotNumber != slotNumber)
                continue;

            slot.GetPlacementPose(out Vector3 position, out _);
            float distanceSq = (position - record.Position).sqrMagnitude;
            if (nearest != null && Mathf.Abs(distanceSq - bestDistanceSq) < 0.000001f)
            {
                ambiguous = true;
                continue;
            }
            if (distanceSq >= bestDistanceSq)
                continue;

            bestDistanceSq = distanceSq;
            nearest = slot;
            ambiguous = false;
        }

        return !ambiguous && nearest != null && nearest.IsEmpty ? nearest : null;
    }

    static bool TryRestoreHeldCard(WorldCard card)
    {
        PlayerCardHand hand = PlayerCardHand.Instance;
        if (hand == null)
            return false;

        return hand.RestoreHeldCard(card);
    }

    static bool IsDemoAuthored(Component component, Transform demoRoot)
    {
        if (component == null)
            return false;

        PhysicsLevelItem item = component.GetComponent<PhysicsLevelItem>();
        if (item != null && item.Area == PhysicsLevelItem.AreaKind.Demo)
            return true;

        return demoRoot != null && component.transform.IsChildOf(demoRoot);
    }

    static void WakeRestoredObject(Transform transform)
    {
        if (transform == null)
            return;

        transform.SetParent(null, true);
        if (!transform.gameObject.activeSelf)
            transform.gameObject.SetActive(true);
    }

    static void RestorePack(PackSaveRecord record, Transform scatterRoot, Transform demoRoot)
    {
        if (record == null)
            return;

        string restoreId = AllocateRestoreId(record.id);
        List<CardDefinition> contents = ResolvePackContents(record.contents);

        WorldBoosterPack pack;
        if (PersistentIdRegistry.TryGetPack(restoreId, out WorldBoosterPack existing) && existing != null)
        {
            bool demoAuthored = IsDemoAuthored(existing, demoRoot);
            if (demoAuthored && !record.held)
                return;

            pack = existing;
            if (!demoAuthored)
            {
                WakeRestoredObject(pack.transform);
                pack.Initialize(null, record.variant, contents, (PackCardSet)record.packSet);
            }
        }
        else
        {
            pack = PackFactory.CreateWorldPack(
                record.Position,
                record.Rotation,
                packDefinition: null,
                packName: "Booster Pack",
                packVariantIndex: record.variant,
                preRolledContents: contents,
                packSet: (PackCardSet)record.packSet);
            PersistentId.GetOrCreate(pack.gameObject).AssignExisting(restoreId);
        }

        pack.PreserveSavedContents();
        if (!string.IsNullOrEmpty(record.assignmentLabel))
            pack.AssignmentLabel = record.assignmentLabel;

        if (record.held && PlayerCardHand.Instance != null && PlayerCardHand.Instance.RestoreHeldPack(pack))
            return;

        pack.transform.SetParent(scatterRoot, true);
        pack.transform.SetPositionAndRotation(record.Position, record.Rotation);
        pack.RestoreSavedWorldPose(record.faceDown, record.stackLayer);
        CardGroundStack.TrackPack(pack);
    }

    static List<CardDefinition> ResolvePackContents(string[] ids)
    {
        var contents = new List<CardDefinition>(ids != null ? ids.Length : 0);
        if (ids == null)
            return contents;

        for (int i = 0; i < ids.Length; i++)
        {
            // Preserve the original five positions, including unresolved entries. Silently
            // shortening the list used to reroll the entire saved pack on registration/opening.
            CardDefinition definition = null;
            if (!string.IsNullOrEmpty(ids[i]))
                CardCatalog.TryGetById(ids[i], out definition);
            contents.Add(definition);
        }

        return contents;
    }
}
