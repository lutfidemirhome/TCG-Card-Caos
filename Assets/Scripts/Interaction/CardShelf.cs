using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Cabinet shelf that places held cards into author-placed <see cref="CardShelfSlot"/> markers.
/// Look at a board / near a slot while holding a card — yellow outline shows the target seat — press E.
/// </summary>
public class CardShelf : MonoBehaviour, IInteractable
{
    [SerializeField] Transform placementRoot;
    [Tooltip("When aiming, prefer empty slots within this vertical distance of the hit point.")]
    [SerializeField] float levelYTolerance = 0.28f;
    [Tooltip("Empty slot must be this close (XZ) to the look hit, or the place prompt hides.")]
    [SerializeField] float slotAimMaxDistance = 0.18f;
    [SerializeField] float surfacePadding = 0.003f;

    [Header("Placement flight")]
    [SerializeField] float shelfFlightDuration = 0.4f;
    [SerializeField] float shelfFlightArcHeight = 0.22f;

    [Header("Cabinet category")]
    [Tooltip("Category asset for this cabinet (id + sign material). When set, overrides Category Id below.")]
    [SerializeField] CardShelfCategoryDefinition categoryDefinition;
    [Tooltip("Only cards with the same Shelf Category Id can be placed here.")]
    [SerializeField] string categoryId = CardShelfCategories.NormalCommon;

    readonly List<CardShelfSlot> _slots = new List<CardShelfSlot>(32);
    readonly Dictionary<CardShelfSlot, int> _resolvedSlotNumbers = new Dictionary<CardShelfSlot, int>(32);

    struct ShelfFlightEntry
    {
        public WorldCard Card;
        public CardShelfSlot Slot;
        public bool IsCorrect;
    }

    readonly List<ShelfFlightEntry> _shelfFlights = new List<ShelfFlightEntry>(4);

    Vector3 _aimWorldPoint;
    bool _hasAimPoint;
    GameObject _placementOutline;
    CardShelfSlot _previewSlot;

    void Awake()
    {
        if (placementRoot == null)
            placementRoot = transform;
        PersistentId.GetOrCreate(gameObject);
        RefreshSlotCache();
    }

    void Start()
    {
        CabinetSignCompleteOverlay.Refresh(this);
    }

    void OnDestroy()
    {
        DestroyPlacementOutline();
    }

    void LateUpdate()
    {
        if (_shelfFlights.Count == 0)
            return;

        UpdateShelfFlights();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (categoryDefinition != null && !string.IsNullOrWhiteSpace(categoryDefinition.CategoryId))
            categoryId = categoryDefinition.CategoryId;
    }
#endif

    public void RefreshSlotCache()
    {
        _slots.Clear();
        _resolvedSlotNumbers.Clear();
        GetComponentsInChildren(true, _slots);
        for (int i = 0; i < _slots.Count; i++)
        {
            CardShelfSlot slot = _slots[i];
            if (slot == null)
                continue;

            slot.SyncIndicesFromHierarchy();
            if (!slot.gameObject.activeInHierarchy)
            {
                _slots[i] = null;
            }
        }

        _slots.RemoveAll(slot => slot == null);
        RebuildResolvedSlotNumbers();
    }

    /// <summary>
    /// Customer-facing slot number (1 = leftmost along the row when standing in the aisle).
    /// Derived from world layout so cabinet rotation does not invert numbering.
    /// </summary>
    public int ResolveSlotNumber(CardShelfSlot slot)
    {
        if (slot == null)
            return 0;

        if (_resolvedSlotNumbers.Count == 0)
            RebuildResolvedSlotNumbers();

        if (_resolvedSlotNumbers.TryGetValue(slot, out int number))
            return number;

        return CardShelfCategories.ColumnToSlotNumber(slot.ColumnIndex, SlotsPerRow);
    }

    void RebuildResolvedSlotNumbers()
    {
        _resolvedSlotNumbers.Clear();
        if (_slots.Count == 0)
            return;

        var slotsByRow = new Dictionary<int, List<CardShelfSlot>>(8);
        for (int i = 0; i < _slots.Count; i++)
        {
            CardShelfSlot slot = _slots[i];
            if (slot == null || !slot.gameObject.activeInHierarchy)
                continue;

            if (!slotsByRow.TryGetValue(slot.RowIndex, out List<CardShelfSlot> rowSlots))
            {
                rowSlots = new List<CardShelfSlot>(8);
                slotsByRow.Add(slot.RowIndex, rowSlots);
            }

            rowSlots.Add(slot);
        }

        Vector3 customerView = -GetCustomerFacingDirection();
        customerView.y = 0f;
        if (customerView.sqrMagnitude < 0.0001f)
            customerView = Vector3.forward;
        else
            customerView.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, customerView);
        if (right.sqrMagnitude < 0.0001f)
            return;
        right.Normalize();

        int slotsPerRow = SlotsPerRow;
        foreach (KeyValuePair<int, List<CardShelfSlot>> entry in slotsByRow)
        {
            List<CardShelfSlot> rowSlots = entry.Value;
            rowSlots.Sort((a, b) =>
            {
                float aDot = Vector3.Dot(a.transform.position, right);
                float bDot = Vector3.Dot(b.transform.position, right);
                return aDot.CompareTo(bDot);
            });

            int numberedSlots = Mathf.Min(rowSlots.Count, slotsPerRow);
            for (int i = 0; i < numberedSlots; i++)
                _resolvedSlotNumbers[rowSlots[i]] = i + 1;
        }
    }

    /// <summary>Horizontal direction customers face when reading cards on this shelf.</summary>
    public Vector3 GetCustomerFacingDirection()
    {
        if (_slots.Count == 0)
            RefreshSlotCache();

        for (int i = 0; i < _slots.Count; i++)
        {
            CardShelfSlot slot = _slots[i];
            if (slot == null)
                continue;

            // Slot blue axis = card face toward the aisle.
            Vector3 face = slot.transform.forward;
            face.y = 0f;
            if (face.sqrMagnitude > 0.0001f)
                return face.normalized;
        }

        Transform root = placementRoot != null ? placementRoot : transform;
        Vector3 fallback = root.forward;
        fallback.y = 0f;
        if (fallback.sqrMagnitude < 0.0001f)
            return Vector3.forward;

        return fallback.normalized;
    }

    public string CategoryId =>
        categoryDefinition != null && !string.IsNullOrWhiteSpace(categoryDefinition.CategoryId)
            ? categoryDefinition.CategoryId
            : categoryId;

    public CardShelfCategoryDefinition CategoryDefinition => categoryDefinition;

    public string CategoryDisplayName => CardShelfCategories.GetDisplayName(CategoryId);

    public int SlotsPerRow =>
        categoryDefinition != null
            ? categoryDefinition.SlotsPerRow
            : CardShelfCategories.GetDefaultSlotsPerRow(categoryId);

    public bool AcceptsDefinition(CardDefinition definition)
    {
        return CardShelfRules.CanPlaceOnShelf(CategoryId, definition);
    }

    public bool CanPlaceCardInSlot(WorldCard card, CardShelfSlot slot)
    {
        return card != null && slot != null && slot.IsEmpty;
    }

    public bool IsCorrectPlacement(WorldCard card, CardShelfSlot slot)
    {
        if (card == null || slot == null)
            return false;

        if (_slots.Count == 0)
            RefreshSlotCache();

        return CardShelfRules.IsCorrectShelfPlacement(CategoryId, card.Definition, slot, _slots);
    }

    public void SetAimHit(RaycastHit hit)
    {
        _aimWorldPoint = hit.point;
        _hasAimPoint = true;
        RefreshPlacementPreview();
    }

    public void ClearAim()
    {
        _hasAimPoint = false;
        _previewSlot = null;
        HidePlacementOutline();
    }

    public string GetPromptText()
    {
        PlayerCardHand hand = FindHand();
        if (hand == null)
            return string.Empty;

        Vector3 aim = _hasAimPoint ? _aimWorldPoint : transform.position;
        bool aimOnOccupied = IsAimOnOccupiedSlot(aim);

        if (hand.HasSelectedHeldCard() && !aimOnOccupied)
        {
            WorldCard selectedCard = hand.SelectedHeldCard;
            if (selectedCard == null || selectedCard.UsesPsaSlab)
            {
                HidePlacementOutline();
                return string.Empty;
            }

            RefreshOccupancy();
            CardShelfSlot slot = FindAimTargetSlot();
            if (slot == null)
            {
                HidePlacementOutline();
                return string.Empty;
            }

            return InteractPrompt.Format(Localization.Get(LocalizationKeys.PromptPlaceCard));
        }

        HidePlacementOutline();
        return string.Empty;
    }

    public void Interact(GameObject interactor)
    {
        PlayerCardHand hand = ResolveHand(interactor);
        if (hand == null)
            return;

        Vector3 aim = _hasAimPoint ? _aimWorldPoint : transform.position;
        if (!hand.HasSelectedHeldCard() || IsAimOnOccupiedSlot(aim))
            return;

        // Resolve and validate the seat BEFORE the card leaves the hand. A taken card is still
        // HandState.Held, so the old rollback (TryPickup) always refused it and left the card
        // visually in hand but absent from every hand list — undroppable and unplaceable forever.
        // This fires whenever the prompt says "Shelf Full" or the aim sits between shelf levels.
        RefreshOccupancy();
        CardShelfSlot slot = FindAimTargetSlot();
        WorldCard selectedCard = hand.SelectedHeldCard;
        if (slot == null || selectedCard == null || selectedCard.UsesPsaSlab || !CanPlaceCardInSlot(selectedCard, slot))
            return;

        if (!hand.TryTakeSelectedHeldCard(out WorldCard card))
            return;

        if (card != selectedCard || !CanPlaceCardInSlot(card, slot))
        {
            hand.ReturnHeldCard(card);
            return;
        }

        bool isCorrect = IsCorrectPlacement(card, slot);
        HidePlacementOutline();
        slot.Occupy(card);
        BeginShelfFlight(card, slot, isCorrect);
        ClearAim();
    }

    public int CountPlaceableSlots()
    {
        RefreshSlotCache();
        return _slots.Count;
    }

    public int CountOccupiedSlots()
    {
        RefreshSlotCache();
        int count = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null && !_slots[i].IsEmpty)
                count++;
        }

        return count;
    }

    public int CountCorrectlyPlacedCards()
    {
        CollectHudProgress(out int placed, out _);
        return placed;
    }

    public bool IsComplete()
    {
        CollectHudProgress(out _, out bool complete);
        return complete;
    }

    /// <summary>Single slot scan for HUD / save progress.</summary>
    public void CollectHudProgress(out int correctlyPlaced, out bool complete)
    {
        RefreshSlotCache();
        correctlyPlaced = 0;
        complete = _slots.Count > 0;

        for (int i = 0; i < _slots.Count; i++)
        {
            CardShelfSlot slot = _slots[i];
            if (slot == null || slot.IsEmpty)
            {
                complete = false;
                continue;
            }

            WorldCard card = slot.OccupiedCard;
            if (card != null && !card.IsInHand && IsCorrectPlacement(card, slot))
                correctlyPlaced++;
            else
                complete = false;
        }
    }

    /// <summary>
    /// One row is full of correctly placed cards from the same series (3 on rare, 5 on uncommon,
    /// 10 on common). Does not require filling the whole cabinet.
    /// </summary>
    public bool HasCompletedSeriesRow()
    {
        RefreshSlotCache();
        int needed = SlotsPerRow;
        if (needed <= 0 || _slots.Count == 0)
            return false;

        for (int i = 0; i < _slots.Count; i++)
        {
            CardShelfSlot start = _slots[i];
            if (start == null)
                continue;

            int row = start.RowIndex;
            bool seen = false;
            for (int previous = 0; previous < i; previous++)
            {
                if (_slots[previous] != null && _slots[previous].RowIndex == row)
                {
                    seen = true;
                    break;
                }
            }

            if (seen)
                continue;

            if (IsSeriesRowComplete(row, needed))
                return true;
        }

        return false;
    }

    bool IsSeriesRowComplete(int rowIndex, int needed)
    {
        int correct = 0;
        string seriesId = null;
        for (int i = 0; i < _slots.Count; i++)
        {
            CardShelfSlot slot = _slots[i];
            if (slot == null || slot.RowIndex != rowIndex)
                continue;

            if (slot.IsEmpty)
                continue;

            WorldCard card = slot.OccupiedCard;
            if (card == null || card.IsInHand || !IsCorrectPlacement(card, slot))
                return false;

            if (!CardShelfSeries.TryGetSeriesId(card.Definition, out string cardSeries))
                return false;

            if (seriesId == null)
                seriesId = cardSeries;
            else if (!string.Equals(seriesId, cardSeries, System.StringComparison.Ordinal))
                return false;

            correct++;
        }

        return seriesId != null && correct >= needed;
    }

    void PlayRowCompleteFeedback(int rowIndex)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            CardShelfSlot slot = _slots[i];
            if (slot == null || slot.RowIndex != rowIndex)
                continue;

            WorldCard card = slot.OccupiedCard;
            if (card == null || card.IsInHand)
                continue;

            card.PlayShelfRowCompleteFeedback();
        }
    }

    public float SurfacePadding => surfacePadding;

    public bool TryRestoreCard(WorldCard card, int rowIndex, int columnIndex)
    {
        return TryRestoreCard(card, rowIndex, columnIndex, null, Vector3.zero);
    }

    public bool TryRestoreCard(
        WorldCard card,
        int rowIndex,
        int columnIndex,
        string slotName,
        Vector3 worldHint)
    {
        if (card == null)
            return false;

        CardShelfSlot slot = FindSlotForRestore(card, rowIndex, columnIndex, worldHint);
        if (slot == null)
            return false;

        return slot.RestoreOccupiedCard(card, surfacePadding, IsCorrectPlacement(card, slot));
    }

    public CardShelfSlot FindSlotByRelativePath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return null;

        Transform found = FindRelativeTransform(transform, relativePath);
        if (found == null)
            return null;

        return found.GetComponent<CardShelfSlot>();
    }

    static Transform FindRelativeTransform(Transform root, string relativePath)
    {
        if (root == null || string.IsNullOrEmpty(relativePath))
            return null;

        string[] parts = relativePath.Split('/');
        Transform current = root;
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i]))
                continue;

            current = FindDirectChild(current, parts[i]);
            if (current == null)
                return null;
        }

        return current;
    }

    static Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && child.name == childName)
                return child;
        }

        return null;
    }

    public CardShelfSlot FindSlotByAuthoredHierarchy(int rowIndex, int columnIndex)
    {
        string levelName = rowIndex <= 0 ? "ShelfSlots_Level" : "ShelfSlots_Level (" + rowIndex + ")";
        string slotName = CardShelfSlotNaming.BuildName(0, columnIndex);
        Transform level = FindDescendant(transform, levelName);
        if (level == null)
            return null;

        for (int i = 0; i < level.childCount; i++)
        {
            Transform child = level.GetChild(i);
            if (child == null || child.name != slotName || !child.gameObject.activeInHierarchy)
                continue;

            return child.GetComponent<CardShelfSlot>();
        }

        return null;
    }

    static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null || !root.gameObject.activeInHierarchy)
            return null;
        if (root.name == objectName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendant(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    public CardShelfSlot FindSlotForRestore(WorldCard card, int rowIndex, int columnIndex, Vector3 worldHint)
    {
        CardShelfSlot[] slots = GetComponentsInChildren<CardShelfSlot>(true);
        CardShelfSlot rowColMatch = null;
        CardShelfSlot nearest = null;
        float nearestSq = 3f * 3f;

        for (int i = 0; i < slots.Length; i++)
        {
            CardShelfSlot slot = slots[i];
            if (slot == null)
                continue;

            slot.SyncIndicesFromHierarchy();
            if (!slot.gameObject.activeInHierarchy)
                continue;
            if (!slot.IsEmpty && slot.OccupiedCard != card)
                continue;

            if (rowColMatch == null
                && slot.RowIndex == rowIndex
                && slot.ColumnIndex == columnIndex)
                rowColMatch = slot;

            float sq = (slot.transform.position - worldHint).sqrMagnitude;
            if (sq < nearestSq)
            {
                nearestSq = sq;
                nearest = slot;
            }
        }

        return rowColMatch != null ? rowColMatch : nearest;
    }

    public bool IsAimOnOccupiedSlot(Vector3 aim)
    {
        RefreshOccupancy();
        CardShelfSlot closest = FindClosestSlot(aim);
        return closest != null && !closest.IsEmpty;
    }

    void BeginShelfFlight(WorldCard card, CardShelfSlot slot, bool isCorrect)
    {
        if (card == null || slot == null)
            return;

        GameSoundEffects.Play(GameSoundEffects.Id.CardThrow);

        _shelfFlights.Add(new ShelfFlightEntry
        {
            Card = card,
            Slot = slot,
            IsCorrect = isCorrect,
        });

        card.BeginShelfFlight(
            slot.transform,
            CardDimensions.WorldCardScale,
            shelfFlightDuration,
            shelfFlightArcHeight,
            surfacePadding,
            () =>
            {
                RemoveShelfFlight(card);
                if (IsSeriesRowComplete(slot.RowIndex, SlotsPerRow))
                    PlayRowCompleteFeedback(slot.RowIndex);
                else
                    card.NotifyShelfPlacement(isCorrect);
                GameSoundEffects.Play(GameSoundEffects.Id.CardShelfPlace);
                GameSaveSignals.MarkDirty();
                if (IsComplete())
                    GameSaveSignals.NotifyMilestone();
                CabinetSignCompleteOverlay.Refresh(this);
            });
    }

    void RemoveShelfFlight(WorldCard card)
    {
        for (int i = _shelfFlights.Count - 1; i >= 0; i--)
        {
            if (_shelfFlights[i].Card == card)
                _shelfFlights.RemoveAt(i);
        }
    }

    void UpdateShelfFlights()
    {
        for (int i = 0; i < _shelfFlights.Count; i++)
        {
            ShelfFlightEntry flight = _shelfFlights[i];
            WorldCard card = flight.Card;
            CardShelfSlot slot = flight.Slot;
            if (card == null || slot == null || !card.IsFlyingToShelf)
                continue;

            GetSlotFlightPose(slot.transform, out Vector3 targetPos, out Quaternion targetRot);
            card.UpdateShelfFlight(targetPos, targetRot);
        }

        for (int i = _shelfFlights.Count - 1; i >= 0; i--)
        {
            WorldCard card = _shelfFlights[i].Card;
            if (card == null || !card.IsFlyingToShelf)
                _shelfFlights.RemoveAt(i);
        }
    }

    void GetSlotFlightPose(Transform slot, out Vector3 position, out Quaternion rotation)
    {
        float halfHeight = CardDimensions.Height * CardDimensions.WorldCardScale * 0.5f;
        rotation = slot.rotation;
        position = slot.position + slot.up * (halfHeight + surfacePadding);
    }

    void RefreshPlacementPreview()
    {
        PlayerCardHand hand = FindHand();
        if (hand == null || !hand.HasSelectedHeldCard() || !_hasAimPoint)
        {
            HidePlacementOutline();
            return;
        }

        if (IsAimOnOccupiedSlot(_aimWorldPoint))
        {
            HidePlacementOutline();
            return;
        }

        RefreshOccupancy();
        WorldCard selectedCard = hand.SelectedHeldCard;
        if (selectedCard == null)
        {
            HidePlacementOutline();
            return;
        }

        CardShelfSlot slot = FindAimTargetSlot();
        if (slot == null)
        {
            _previewSlot = null;
            HidePlacementOutline();
            return;
        }

        _previewSlot = slot;
        ShowPlacementOutline(slot);
    }

    CardShelfSlot FindAimTargetSlot()
    {
        if (!HasAnySlots())
            return null;

        Vector3 aim = _hasAimPoint ? _aimWorldPoint : transform.position;

        CardShelfSlot bestOnLevel = null;
        float bestOnLevelDist = float.MaxValue;

        for (int i = 0; i < _slots.Count; i++)
        {
            CardShelfSlot slot = _slots[i];
            if (slot == null || !slot.IsEmpty)
                continue;

            Vector3 slotPos = slot.transform.position;
            if (!_hasAimPoint || Mathf.Abs(slotPos.y - aim.y) > levelYTolerance)
                continue;

            float dist = HorizontalAimDistanceSq(slotPos, aim);
            if (dist > slotAimMaxDistance * slotAimMaxDistance)
                continue;
            if (dist < bestOnLevelDist)
            {
                bestOnLevelDist = dist;
                bestOnLevel = slot;
            }
        }

        return bestOnLevel;
    }

    static float HorizontalAimDistanceSq(Vector3 slotPos, Vector3 aim)
    {
        float dx = slotPos.x - aim.x;
        float dz = slotPos.z - aim.z;
        return dx * dx + dz * dz;
    }

    CardShelfSlot FindClosestSlot(Vector3 aim)
    {
        CardShelfSlot closest = null;
        float closestDist = float.MaxValue;

        for (int i = 0; i < _slots.Count; i++)
        {
            CardShelfSlot slot = _slots[i];
            if (slot == null)
                continue;

            float dist = (slot.transform.position - aim).sqrMagnitude;
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = slot;
            }
        }

        return closest;
    }

    void RefreshOccupancy()
    {
        if (_slots.Count == 0)
            RefreshSlotCache();

        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null)
                _slots[i].RefreshOccupancy();
        }
    }

    bool HasAnySlots()
    {
        if (_slots.Count == 0)
            RefreshSlotCache();
        return _slots.Count > 0;
    }

    void ShowPlacementOutline(CardShelfSlot slot)
    {
        EnsurePlacementOutline();
        CardArtLibrary.EnsureLoaded();

        float halfHeight = CardDimensions.Height * CardDimensions.WorldCardScale * 0.5f;
        Vector3 outlinePos = slot.transform.position + slot.transform.up * (halfHeight + surfacePadding);

        Transform t = _placementOutline.transform;
        t.SetPositionAndRotation(outlinePos, slot.transform.rotation);
        t.localScale = Vector3.one * CardDimensions.WorldCardScale;
        _placementOutline.SetActive(true);
    }

    void HidePlacementOutline()
    {
        if (_placementOutline != null)
            _placementOutline.SetActive(false);
    }

    void EnsurePlacementOutline()
    {
        if (_placementOutline != null)
            return;

        CardArtLibrary.EnsureLoaded();

        _placementOutline = new GameObject("ShelfPlacementOutline");
        _placementOutline.transform.SetParent(null, true);

        var meshFilter = _placementOutline.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CardVisualResources.InteractionBorderFrameMesh;

        var meshRenderer = _placementOutline.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = CardVisualResources.ShelfPlacementOutlineMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    void DestroyPlacementOutline()
    {
        if (_placementOutline == null)
            return;

        if (Application.isPlaying)
            Destroy(_placementOutline);
        else
            DestroyImmediate(_placementOutline);

        _placementOutline = null;
    }

    static PlayerCardHand FindHand() => PlayerCardHand.Instance;

    static PlayerCardHand ResolveHand(GameObject interactor) =>
        PlayerCardHandResolver.FromInteractorOrInstance(interactor);
}
