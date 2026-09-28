using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Restores settled poses first; saved throws resume only after the whole world is ready.
/// </summary>
public static class GameSaveRestore
{
    // Bound both the amount of work and elapsed time: fast restores do not pay
    // a rendered frame for every 24 records, while slower devices keep the overlay responsive.
    const int EntitiesPerFrame = 512;
    const double BatchBudgetMilliseconds = 8d;

    static readonly HashSet<string> RestoredIds = new HashSet<string>();
    static readonly List<KeyValuePair<WorldCard, ThrownPhysicsSaveState>> PendingCardPhysics =
        new List<KeyValuePair<WorldCard, ThrownPhysicsSaveState>>();
    static readonly List<KeyValuePair<WorldBoosterPack, ThrownPhysicsSaveState>> PendingPackPhysics =
        new List<KeyValuePair<WorldBoosterPack, ThrownPhysicsSaveState>>();
    static bool _remappedIds;
    public static bool LastRestoreSucceeded { get; private set; }

    // These scene references live only for one restore. In particular, PSA paths
    // must not require a full scene search and hierarchy-string build per card.
    sealed class RestoreLookups : System.IDisposable
    {
        readonly Dictionary<string, CardShelf> _shelvesByPath = new Dictionary<string, CardShelf>();
        readonly Dictionary<string, CardShelf> _shelvesByName = new Dictionary<string, CardShelf>();
        readonly Dictionary<string, PsaCabinetSlot> _psaSlotsByPath = new Dictionary<string, PsaCabinetSlot>();
        PsaCabinetSlot[] _psaSlots;
        bool _shelvesLoaded;

        public CardShelf FindFallbackShelf(string shelfId)
        {
            if (!_shelvesLoaded)
            {
                _shelvesLoaded = true;
                CardShelf[] shelves = Object.FindObjectsByType<CardShelf>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < shelves.Length; i++)
                {
                    CardShelf shelf = shelves[i];
                    if (shelf == null)
                        continue;

                    string path = PersistentId.BuildPathFallback(shelf.transform);
                    // Legacy fallback selected the first matching shelf in this order.
                    _shelvesByPath.TryAdd(path, shelf);
                    _shelvesByName.TryAdd(shelf.gameObject.name, shelf);
                }
            }

            if (_shelvesByPath.TryGetValue(shelfId, out CardShelf byPath))
                return byPath;

            string objectName = ShelfObjectName(shelfId);
            return !string.IsNullOrEmpty(objectName)
                && _shelvesByName.TryGetValue(objectName, out CardShelf byName) ? byName : null;
        }

        public PsaCabinetSlot[] PsaSlots
        {
            get
            {
                if (_psaSlots != null)
                    return _psaSlots;

                _psaSlots = Object.FindObjectsByType<PsaCabinetSlot>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < _psaSlots.Length; i++)
                {
                    PsaCabinetSlot slot = _psaSlots[i];
                    if (slot == null)
                        continue;

                    string path = PersistentId.BuildPathFallback(slot.transform);
                    // Preserve the original ambiguity rejection: duplicate paths
                    // must never silently move a saved card to a different seat.
                    if (!_psaSlotsByPath.TryAdd(path, slot))
                        _psaSlotsByPath[path] = null;
                }
                return _psaSlots;
            }
        }

        public PsaCabinetSlot FindPsaSlotByPath(string path)
        {
            _ = PsaSlots;
            _psaSlotsByPath.TryGetValue(path, out PsaCabinetSlot slot);
            return slot;
        }

        public void Dispose()
        {
            _shelvesByPath.Clear();
            _shelvesByName.Clear();
            _psaSlotsByPath.Clear();
            _psaSlots = null;
        }
    }

    public static IEnumerator RestoreRoutine(string slotId)
    {
        ClearPendingPhysics();
        LastRestoreSucceeded = false;
        GameSaveEvents.RaiseLoadStarted(slotId);

        if (!SaveFileIO.TryLoadSave(slotId, out GameSaveData data, out string error))
        {
            GameSaveEvents.RaiseLoadFailed(error ?? "Save could not be loaded.");
            yield break;
        }

        CardScatterUtility.ClearTestCards();
        PhysicsLevelLayout.SuspendAuthoredItemsForSaveRestore();
        yield return null;

        PersistentIdRegistry.RebuildWorldLookups();
        PrepareShelvesForRestore();
        Transform scatterRoot = CardScatterUtility.GetOrCreateScatterRoot();
        // The authored demo root is stable throughout this restore. Resolve it once,
        // rather than searching every scene object again for each card and pack.
        PhysicsLevelLayout layout = PhysicsLevelLayout.FindExisting();
        Transform demoRoot = layout != null ? layout.DemoCardsRoot : null;
        RestoredIds.Clear();
        _remappedIds = false;

        int processed = 0;
        var batchTimer = System.Diagnostics.Stopwatch.StartNew();
        using (var lookups = new RestoreLookups())
        {
            if (data.cards != null)
            {
                for (int i = 0; i < data.cards.Length; i++)
                {
                    RestoreCard(data.cards[i], scatterRoot, demoRoot, lookups);
                    processed++;
                    if (processed >= EntitiesPerFrame || batchTimer.Elapsed.TotalMilliseconds >= BatchBudgetMilliseconds)
                    {
                        yield return null;
                        processed = 0;
                        batchTimer.Restart();
                    }
                }
            }
        }

        if (data.packs != null)
        {
            for (int i = 0; i < data.packs.Length; i++)
            {
                RestorePack(data.packs[i], scatterRoot, demoRoot);
                processed++;
                if (processed >= EntitiesPerFrame || batchTimer.Elapsed.TotalMilliseconds >= BatchBudgetMilliseconds)
                {
                    yield return null;
                    processed = 0;
                    batchTimer.Restart();
                }
            }
        }

        FinalizeShelfRestores();
        FinalizePsaRestores();
        yield return null;

        if (data.hasPlayerState && data.player != null)
        {
            FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>();
            if (player != null)
                player.RestoreSaveState(data.player);
        }

        PlayerCardHand hand = PlayerCardHand.Instance;
        if (hand != null)
        {
            hand.RestoreSelectionIndex(data.handSelectedIndex);
        }

        GamePlayTime.BeginSession(data.playTimeSeconds);
        PersistentIdRegistry.RebuildWorldLookups();
        if (_remappedIds)
            GameSaveDirtyTracker.MarkDirty();
        else
            GameSaveDirtyTracker.Clear();
        LastRestoreSucceeded = true;
        GameSaveEvents.RaiseLoadCompleted(slotId);
    }

    public static void ClearPendingPhysics()
    {
        PendingCardPhysics.Clear();
        PendingPackPhysics.Clear();
    }

    public static void ResumePendingPhysics()
    {
        bool hasPendingMotion = false;
        // Rebuild sleeping supports before any moving item's monitor can inspect its surroundings.
        // Both passes are synchronous: no fixed step can run with only part of the pile restored.
        for (int pass = 0; pass < 2; pass++)
        {
            bool restoreSleeping = pass == 0;
            for (int i = 0; i < PendingCardPhysics.Count; i++)
            {
                var pending = PendingCardPhysics[i];
                if (pending.Key == null || pending.Value == null
                    || pending.Value.isSleeping != restoreSleeping)
                    continue;

                pending.Key.ResumeSavedPhysics(pending.Value);
                hasPendingMotion |= !restoreSleeping;
            }
            for (int i = 0; i < PendingPackPhysics.Count; i++)
            {
                var pending = PendingPackPhysics[i];
                if (pending.Key == null || pending.Value == null
                    || pending.Value.isSleeping != restoreSleeping)
                    continue;

                pending.Key.ResumeSavedPhysics(pending.Value);
                hasPendingMotion |= !restoreSleeping;
            }
        }

        // Collider construction and landing-surface activation may wake an earlier support.
        // Restore the saved sleep state once the complete pile exists; real contacts can wake it
        // naturally from the next physics step onward.
        for (int i = 0; i < PendingCardPhysics.Count; i++)
        {
            var pending = PendingCardPhysics[i];
            if (pending.Key != null && pending.Value != null && pending.Value.isSleeping)
            {
                Rigidbody body = pending.Key.PhysicsBody;
                if (body != null && !body.isKinematic)
                    body.Sleep();
            }
        }
        for (int i = 0; i < PendingPackPhysics.Count; i++)
        {
            var pending = PendingPackPhysics[i];
            if (pending.Key != null && pending.Value != null && pending.Value.isSleeping)
            {
                Rigidbody body = pending.Key.PhysicsBody;
                if (body != null && !body.isKinematic)
                    body.Sleep();
            }
        }
        ClearPendingPhysics();
        if (hasPendingMotion)
            GameSaveDirtyTracker.MarkDirty();
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

    static void RestoreCard(CardSaveRecord record, Transform scatterRoot, Transform demoRoot,
        RestoreLookups lookups)
    {
        if (record == null)
            return;

        string restoreId = AllocateRestoreId(record.id);

        WorldCard card;
        if (PersistentIdRegistry.TryGetCard(restoreId, out WorldCard existing) && existing != null)
        {
            bool demoAuthored = IsDemoAuthored(existing, demoRoot);
            // Old authored records keep their demo scene pose. Explicit physics snapshots
            // identify cards that the player has thrown and must restore that saved pile.
            if (demoAuthored && record.location == CardRuntimeLocation.World
                && (record.physics == null || !record.physics.isSimulating))
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
                if (!TryRestoreShelfCard(card, record, lookups))
                    PlaceWorldCard(card, record, scatterRoot);
                break;
            case CardRuntimeLocation.PsaCabinet:
                if (!TryRestorePsaCard(card, record, lookups))
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
        if (record.location == CardRuntimeLocation.World && record.physics != null && record.physics.isSimulating)
            PendingCardPhysics.Add(new KeyValuePair<WorldCard, ThrownPhysicsSaveState>(card, record.physics));
    }

    static bool TryRestoreShelfCard(WorldCard card, CardSaveRecord record, RestoreLookups lookups)
    {
        CardArtLibrary.EnsureLoaded();
        CardShelfSlot slot = FindShelfSlot(record, lookups);
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

    static CardShelfSlot FindShelfSlot(CardSaveRecord record, RestoreLookups lookups)
    {
        CardShelf shelf = FindShelf(record.shelfId, lookups);
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

    static CardShelf FindShelf(string shelfId, RestoreLookups lookups)
    {
        if (string.IsNullOrEmpty(shelfId))
            return null;

        if (PersistentIdRegistry.TryGetShelf(shelfId, out CardShelf shelf) && shelf != null)
            return shelf;

        return lookups.FindFallbackShelf(shelfId);
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

    static bool TryRestorePsaCard(WorldCard card, CardSaveRecord record, RestoreLookups lookups)
    {
        int slotNumber = record.psaCabinetSlot > 0
            ? record.psaCabinetSlot
            : record.psaSlot;

        PsaCabinetSlot slot = FindPsaSlot(record, slotNumber, lookups);
        if (slot == null)
        {
            Debug.LogWarning(
                "[Save] Missing PSA slot " + slotNumber
                + " (cabinet '" + record.psaCabinetId + "').");
            return false;
        }

        return slot.RestoreOccupiedCard(card, playPlacementFeedback: false);
    }

    static PsaCabinetSlot FindPsaSlot(CardSaveRecord record, int slotNumber, RestoreLookups lookups)
    {
        if (!string.IsNullOrEmpty(record.psaSlotPath))
        {
            PsaCabinetSlot exact = lookups.FindPsaSlotByPath(record.psaSlotPath);
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
        PsaCabinetSlot[] slots = lookups.PsaSlots;
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
            if (demoAuthored && !record.held
                && (record.physics == null || !record.physics.isSimulating))
                return;

            pack = existing;
            if (!demoAuthored)
            {
                WakeRestoredObject(pack.transform);
                pack.Initialize(null, record.variant, contents, preserveExistingVisualLayout: true);
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
                preRolledContents: contents);
            PersistentId.GetOrCreate(pack.gameObject).AssignExisting(restoreId);
        }

        if (record.held && PlayerCardHand.Instance != null && PlayerCardHand.Instance.RestoreHeldPack(pack))
            return;

        pack.transform.SetParent(scatterRoot, true);
        pack.transform.SetPositionAndRotation(record.Position, record.Rotation);
        pack.RestoreSavedWorldPose(record.faceDown, record.stackLayer,
            preservePhysicsPose: !record.held && record.physics != null && record.physics.isSimulating);
        CardGroundStack.TrackPack(pack);
        if (!record.held && record.physics != null && record.physics.isSimulating)
            PendingPackPhysics.Add(new KeyValuePair<WorldBoosterPack, ThrownPhysicsSaveState>(pack, record.physics));
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
