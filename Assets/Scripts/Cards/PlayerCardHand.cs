using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds up to 10 cards in a bottom-center fan. Newest pickup (card or pack) goes to the right.
/// Each held booster pack uses one fan slot (scroll-selectable, not shelf-placeable).
/// </summary>
public class PlayerCardHand : MonoBehaviour
{
    [Header("Screen placement")]
    [SerializeField] float handDistance = 0.42f;
    [Tooltip("Fraction of each card hidden below the bottom screen edge.")]
    [SerializeField] [Range(0f, 0.65f)] float bottomClipPercent = 0.41f;
    [SerializeField] float handDownwardOffset = 0.025f;

    [Header("Fan layout (VoodooDeck-style arc)")]
    [SerializeField] float minFanAngle = 9f;
    [SerializeField] float maxFanAngle = 36f;
    [SerializeField] float fanAngleRampCardSpan = 8f;
    [SerializeField] float fanAngleHardCap = 40f;
    [SerializeField] float radius = 0.252f;
    [SerializeField] float fanPivotY = -0.036f;
    [SerializeField] float verticalCurve = 0.018f;
    [SerializeField] float cardPitchDegrees = 0f;
    [SerializeField] float heldCardScale = 1.036f;
    [Tooltip("Hand cards render this much smaller than heldCardScale (layout uses the reduced size too).")]
    [SerializeField] [Range(0f, 0.5f)] float handScaleReductionPercent = 0.1536f;
    [SerializeField] float cardDepthStep = 0.0025f;
    [SerializeField] float cardVisualOffsetY = 0f;

    [Header("Fan width budget")]
    [SerializeField] float maxWidth = 0.462f;
    [SerializeField] float extraMaxWidthPerCard = 0.0126f;
    [SerializeField] float maxWidthClamp = 0.882f;
    [SerializeField] float minCardSpacing = 0.0252f;
    [SerializeField] float maxCardSpacing = 0.084f;

    [Header("Hand selection")]
    [Tooltip("Screen-up lift for the selected card as a fraction of held card height.")]
    [SerializeField] [Range(0f, 0.5f)] float selectedLiftPercent = 0.15f;
    [Tooltip("Extra pull toward the camera so the selected card clears every other hand card.")]
    [SerializeField] float selectedForwardMargin = 0.006f;

    [Header("Pickup flight")]
    [SerializeField] float pickupFlightDuration = 0.4f;
    [SerializeField] float pickupFlightArcHeight = 0.22f;

    [Header("Throw")]
    [SerializeField] float dropScaleTransitionDuration = 0.12f;
    [SerializeField] float throwSpeed = 4.5f;
    [SerializeField] float throwAimFallbackDistance = 8f;
    [SerializeField] KeyCode dropKey = KeyCode.Q;

    [Header("Booster pack")]
    [SerializeField] float openRevealDistance = 0.48f;
    [Tooltip("Pack-only vertical offset during open reveal. Lower = pack sits lower; card row stays fixed.")]
    [SerializeField] float openRevealHeight = -0.06f;
    [SerializeField] float openSequenceDuration = 1.35f;
    [SerializeField] float openPackAnticipationHold = 1f;
    [SerializeField] KeyCode openPackKey = KeyCode.E;

    static readonly RaycastHit[] ThrowAimHits = new RaycastHit[8];

    Transform _handAnchor;
    Camera _camera;
    int _selectedIndex;
    readonly List<WorldCard> _cards = new List<WorldCard>();
    readonly List<WorldBoosterPack> _heldPacks = new List<WorldBoosterPack>();
    readonly List<HandFanEntry> _handFanOrder = new List<HandFanEntry>();
    bool _handInputLocked;
    bool _isOpeningPack;
    bool _awaitingRevealCollect;
    bool _revealCollectRequested;
    bool _packOpenMovementLocked;
    Coroutine _openPackRoutine;

    public int Count => _cards.Count;
    public static PlayerCardHand Instance { get; private set; }
    public bool IsFull => OccupiedHandSlots >= CardDimensions.MaxHandSize;
    public int OccupiedHandSlots => CountHeldCards() + CountOccupiedPackSlots();
    public int AvailableSlots => CardDimensions.MaxHandSize - OccupiedHandSlots;
    public int SelectedIndex => _selectedIndex;
    public float EffectiveHeldScale => heldCardScale * (1f - handScaleReductionPercent);
    public bool HasHeldPack => CountOccupiedPackSlots() > 0;
    public bool IsPackSelected => GetSelectedHeldPack() != null;
    public bool IsOpeningPack => _isOpeningPack;
    public bool IsAwaitingRevealCollect => _awaitingRevealCollect;
    public bool IsHandInputLocked => _handInputLocked || _isOpeningPack;
    public bool IsPackOpenMovementLocked => _packOpenMovementLocked;
    public float OpenRevealDistance => openRevealDistance;
    public float OpenRevealHeight => openRevealHeight;
    public float OpenSequenceDuration => openSequenceDuration;
    public float OpenPackAnticipationHold => openPackAnticipationHold;
    public float PickupFlightArcHeight => pickupFlightArcHeight;

    public bool CanPickUpPack =>
        !IsHandInputLocked
        && AvailableSlots >= CardDimensions.HandSlotsPerBoosterPack;

    /// <summary>Free slots after the selected pack leaves the hand (it occupies one slot until open starts).</summary>
    public int AvailableSlotsAfterOpeningSelectedPack =>
        AvailableSlots + CardDimensions.HandSlotsPerBoosterPack;

    public bool CanOpenSelectedPack =>
        IsPackSelected
        && !IsHandInputLocked
        && AvailableSlotsAfterOpeningSelectedPack >= CardDimensions.CardsPerBoosterPack;

    void Awake()
    {
        Instance = this;
        _camera = Camera.main;
        EnsureHandAnchor();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        if (_awaitingRevealCollect && Input.GetKeyDown(openPackKey))
        {
            RequestRevealCollect();
            return;
        }

        if (IsHandInputLocked)
            return;

        HandleScrollSelection();

        if (Input.GetKeyDown(dropKey))
        {
            if (IsPackSelected)
                TryDropHeldPack();
            else
                TryDropSelectedCard();
        }
    }

    void LateUpdate()
    {
        UpdateHandAnchorTransform();
        UpdatePickupFlights();
        UpdatePackPickupFlight();

        if (GetHandFanCount() > 0)
            ApplyFanLayout();
    }

    void HandleScrollSelection()
    {
        if (GetHandFanCount() <= 1)
            return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scroll, 0f))
            return;

        int direction = scroll > 0f ? 1 : -1;
        int steps = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(scroll)));
        for (int i = 0; i < steps; i++)
            MoveSelection(direction);
    }

    void MoveSelection(int direction)
    {
        int fanCount = GetHandFanCount();
        if (fanCount <= 1 || direction == 0)
            return;

        int start = _selectedIndex;
        for (int step = 0; step < fanCount; step++)
        {
            _selectedIndex = (_selectedIndex + direction) % fanCount;
            if (_selectedIndex < 0)
                _selectedIndex += fanCount;

            if (IsHandFanIndexSelectable(_selectedIndex))
            {
                if (_selectedIndex != start)
                    GameSoundEffects.Play(
                        GameSoundEffects.Id.CardHandScroll,
                        GameSoundEffects.CardHandScrollVolume);
                return;
            }
        }

        _selectedIndex = start;
    }

    bool IsHandFanIndexSelectable(int fanIndex)
    {
        if (!TryGetEntryAtFanIndex(fanIndex, out HandFanEntry entry))
            return false;

        if (entry.Card != null)
            return entry.Card.IsHeld;

        return entry.Pack != null && entry.Pack.IsHeld;
    }

    bool TryGetEntryAtFanIndex(int fanIndex, out HandFanEntry entry)
    {
        int currentFan = 0;
        for (int i = 0; i < _handFanOrder.Count; i++)
        {
            if (!EntryOccupiesFanSlot(_handFanOrder[i]))
                continue;

            if (currentFan == fanIndex)
            {
                entry = _handFanOrder[i];
                return true;
            }

            currentFan++;
        }

        entry = default;
        return false;
    }

    static bool EntryOccupiesFanSlot(in HandFanEntry entry)
    {
        if (entry.Card != null)
            return entry.Card.IsHeld || entry.Card.IsFlyingToHand;

        if (entry.Pack == null)
            return false;

        WorldBoosterPack pack = entry.Pack;
        return pack.IsHeld
            || pack.State == WorldBoosterPack.PackState.FlyingToHand
            || pack.State == WorldBoosterPack.PackState.Opening;
    }

    void AddHandFanEntry(in HandFanEntry entry)
    {
        _handFanOrder.Add(entry);
    }

    void RemoveHandFanEntry(WorldCard card)
    {
        if (card == null)
            return;

        for (int i = _handFanOrder.Count - 1; i >= 0; i--)
        {
            if (_handFanOrder[i].Card == card)
            {
                _handFanOrder.RemoveAt(i);
                return;
            }
        }
    }

    void RemoveHandFanEntry(WorldBoosterPack pack)
    {
        if (pack == null)
            return;

        for (int i = _handFanOrder.Count - 1; i >= 0; i--)
        {
            if (_handFanOrder[i].Pack == pack)
            {
                _handFanOrder.RemoveAt(i);
                return;
            }
        }
    }

    int GetFanIndexForCard(WorldCard card)
    {
        int fanIndex = 0;
        for (int i = 0; i < _handFanOrder.Count; i++)
        {
            HandFanEntry entry = _handFanOrder[i];
            if (!EntryOccupiesFanSlot(entry))
                continue;

            if (entry.Card == card)
                return fanIndex;

            fanIndex++;
        }

        return 0;
    }

    int GetFanIndexForPack(WorldBoosterPack pack)
    {
        int fanIndex = 0;
        for (int i = 0; i < _handFanOrder.Count; i++)
        {
            HandFanEntry entry = _handFanOrder[i];
            if (!EntryOccupiesFanSlot(entry))
                continue;

            if (entry.Pack == pack)
                return fanIndex;

            fanIndex++;
        }

        return 0;
    }

    int GetHandFanCount()
    {
        int count = 0;
        for (int i = 0; i < _handFanOrder.Count; i++)
        {
            if (EntryOccupiesFanSlot(_handFanOrder[i]))
                count++;
        }

        return count;
    }

    WorldBoosterPack GetSelectedHeldPack()
    {
        if (!TryGetEntryAtFanIndex(_selectedIndex, out HandFanEntry entry))
            return null;

        WorldBoosterPack pack = entry.Pack;
        return pack != null && pack.IsHeld ? pack : null;
    }

    WorldBoosterPack ResolveHeldPackToOpen()
    {
        WorldBoosterPack selected = GetSelectedHeldPack();
        if (selected != null)
            return selected;

        for (int i = _heldPacks.Count - 1; i >= 0; i--)
        {
            WorldBoosterPack pack = _heldPacks[i];
            if (pack.IsHeld)
                return pack;
        }

        return null;
    }

    bool CanOpenHeldPack(WorldBoosterPack pack)
    {
        return pack != null
            && pack.IsHeld
            && !IsHandInputLocked
            && AvailableSlotsAfterOpeningSelectedPack >= CardDimensions.CardsPerBoosterPack;
    }

    int CountOccupiedPackSlots()
    {
        int count = 0;
        for (int i = 0; i < _heldPacks.Count; i++)
        {
            WorldBoosterPack pack = _heldPacks[i];
            if (pack.IsHeld
                || pack.State == WorldBoosterPack.PackState.FlyingToHand
                || pack.State == WorldBoosterPack.PackState.Opening)
            {
                count++;
            }
        }

        return count;
    }

    void EnsureHandAnchor()
    {
        if (_handAnchor != null)
            return;

        var anchorGo = new GameObject("HandFanAnchor");
        _handAnchor = anchorGo.transform;
        UpdateHandAnchorTransform();
    }

    void UpdateHandAnchorTransform()
    {
        if (_handAnchor == null)
            return;

        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null)
            return;

        if (_handAnchor.parent != _camera.transform)
            _handAnchor.SetParent(_camera.transform, false);

        float halfFovRad = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float frustumHeight = 2f * handDistance * Mathf.Tan(halfFovRad);
        float cardViewportHeight = GetCardViewportHeight(frustumHeight);

        float centerViewportY = cardViewportHeight * (0.5f - bottomClipPercent);
        float localY = (centerViewportY - 0.5f) * frustumHeight - handDownwardOffset;

        _handAnchor.localPosition = new Vector3(0f, localY, handDistance);
        _handAnchor.localRotation = Quaternion.identity;
    }

    float GetCardViewportHeight(float frustumHeight)
    {
        float cardWorldHeight = CardDimensions.Height * EffectiveHeldScale;
        return cardWorldHeight / frustumHeight;
    }

    public bool TryPickup(WorldCard card)
    {
        if (card == null || card.IsInHand || card.IsFlyingToShelf || IsHandInputLocked)
            return false;

        if (IsFull)
            return false;

        EnsureHandAnchor();
        UpdateHandAnchorTransform();

        _cards.Add(card);
        AddHandFanEntry(new HandFanEntry { Card = card });
        int newCardIndex = _cards.Count - 1;

        if (GetHandFanCount() == 1)
            _selectedIndex = 0;

        card.BeginPickupFlight(
            _handAnchor,
            EffectiveHeldScale,
            pickupFlightDuration,
            pickupFlightArcHeight,
            () => OnCardPickupFlightComplete(newCardIndex));
        GameSoundEffects.Play(GameSoundEffects.Id.CardPickup);
        return true;
    }

    void OnCardPickupFlightComplete(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= _cards.Count)
            return;

        if (!_cards[cardIndex].IsHeld)
            return;

        _selectedIndex = GetFanIndexForCard(_cards[cardIndex]);
    }

    public bool HasSelectedHeldCard()
    {
        return GetSelectedHeldCard() != null;
    }

    public WorldCard SelectedHeldCard => GetSelectedHeldCard();

    public bool TryTakeSelectedHeldCard(out WorldCard card)
    {
        card = GetSelectedHeldCard();
        if (card == null)
            return false;

        _cards.Remove(card);
        RemoveHandFanEntry(card);
        ClampSelectionIndex();
        ApplyFanLayout();
        return true;
    }

    public bool TryPickupPack(WorldBoosterPack pack)
    {
        if (pack == null || pack.IsInHand || !CanPickUpPack)
            return false;

        EnsureHandAnchor();
        UpdateHandAnchorTransform();
        _heldPacks.Add(pack);
        AddHandFanEntry(new HandFanEntry { Pack = pack });
        int packListIndex = _heldPacks.Count - 1;

        pack.BeginPickupFlight(
            _handAnchor,
            EffectiveHeldScale,
            pickupFlightDuration,
            pickupFlightArcHeight,
            () => OnPackPickupFlightComplete(packListIndex));
        GameSoundEffects.Play(GameSoundEffects.Id.CardPickup);
        return true;
    }

    void OnPackPickupFlightComplete(int packListIndex)
    {
        if (packListIndex < 0 || packListIndex >= _heldPacks.Count)
            return;

        WorldBoosterPack pack = _heldPacks[packListIndex];
        if (!pack.IsHeld)
            return;

        _selectedIndex = GetFanIndexForPack(pack);
    }

    public bool TryOpenHeldPackFromInput()
    {
        if (HasHeldPack && !IsPackSelected)
            SelectLastHeldPack();

        WorldBoosterPack pack = ResolveHeldPackToOpen();
        if (!CanOpenHeldPack(pack) || _openPackRoutine != null)
            return false;

        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null)
            return false;

        _isOpeningPack = true;
        _openPackRoutine = StartCoroutine(OpenHeldPackRoutine(pack));
        return true;
    }

    public bool TryOpenSelectedPack()
    {
        WorldBoosterPack pack = ResolveHeldPackToOpen();
        if (!CanOpenHeldPack(pack) || _openPackRoutine != null)
            return false;

        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null)
            return false;

        _isOpeningPack = true;
        _openPackRoutine = StartCoroutine(OpenHeldPackRoutine(pack));
        return true;
    }

    System.Collections.IEnumerator OpenHeldPackRoutine(WorldBoosterPack pack)
    {
        if (pack == null || !pack.IsHeld)
        {
            _isOpeningPack = false;
            _openPackRoutine = null;
            yield break;
        }

        SetPackOpenMovementLocked(true);
        _heldPacks.Remove(pack);
        RemoveHandFanEntry(pack);
        yield return PackOpenSequence.Run(this, pack, _camera);
        if (_packOpenMovementLocked)
            SetPackOpenMovementLocked(false);
        _isOpeningPack = false;
        _openPackRoutine = null;
    }

    public void ClearHeldPackReference(WorldBoosterPack pack = null)
    {
        if (pack != null)
        {
            _heldPacks.Remove(pack);
            RemoveHandFanEntry(pack);
        }
        ClampSelectionIndex();
    }

    public void SetHandInputLocked(bool locked)
    {
        _handInputLocked = locked;
    }

    public void SetPackOpenMovementLocked(bool locked)
    {
        _packOpenMovementLocked = locked;
    }

    public void SetAwaitingRevealCollect(bool awaiting)
    {
        _awaitingRevealCollect = awaiting;
        if (!awaiting)
            _revealCollectRequested = false;
    }

    public void RequestRevealCollect()
    {
        if (_awaitingRevealCollect)
            _revealCollectRequested = true;
    }

    public bool ConsumeRevealCollectRequest()
    {
        if (!_revealCollectRequested)
            return false;

        _revealCollectRequested = false;
        return true;
    }

    public string GetRevealCollectPromptText()
    {
        return _awaitingRevealCollect ? "Press [E] To Collect Cards" : string.Empty;
    }

    public void AddRevealedCard(WorldCard card, float duration, float arcHeight)
    {
        if (card == null)
            return;

        EnsureHandAnchor();
        UpdateHandAnchorTransform();

        _cards.Add(card);
        AddHandFanEntry(new HandFanEntry { Card = card });
        int newCardIndex = _cards.Count - 1;

        card.BeginPickupFlight(
            _handAnchor,
            EffectiveHeldScale,
            duration,
            arcHeight,
            () =>
            {
                OnCardPickupFlightComplete(newCardIndex);
                GameSoundEffects.PlayPack(GameSoundEffects.PackId.WhileGathering);
            });
    }

    public bool TryDropHeldPack()
    {
        if (!IsPackSelected || IsHandInputLocked)
            return false;

        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null)
            return false;

        WorldBoosterPack pack = GetSelectedHeldPack();
        if (pack == null)
            return false;

        _heldPacks.Remove(pack);
        RemoveHandFanEntry(pack);
        pack.SetHandSelected(false);
        ClampSelectionIndex();

        Vector3 handPos = pack.transform.position;
        Ray aimRay = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 aimPoint = GetReticleAimPoint(aimRay);
        Vector3 throwDirection = aimPoint - handPos;
        if (throwDirection.sqrMagnitude < 0.0001f)
            throwDirection = aimRay.direction;
        else
            throwDirection.Normalize();

        pack.DropWithPhysics(throwDirection * throwSpeed, dropScaleTransitionDuration);
        GameSoundEffects.Play(GameSoundEffects.Id.CardThrow);
        return true;
    }

    public string GetSelectedPackPromptText()
    {
        if (!HasHeldPack)
            return string.Empty;

        if (!IsHandInputLocked
            && AvailableSlotsAfterOpeningSelectedPack >= CardDimensions.CardsPerBoosterPack)
            return "Press [E] To Open Pack";

        int slotsNeeded = CardDimensions.CardsPerBoosterPack - CardDimensions.HandSlotsPerBoosterPack;
        return "Need "
            + slotsNeeded
            + " free hand slots to open pack";
    }

    public bool IsSelectedPackOpenBlocked()
    {
        if (!HasHeldPack)
            return false;

        return IsHandInputLocked
            || AvailableSlotsAfterOpeningSelectedPack < CardDimensions.CardsPerBoosterPack;
    }

    void SelectLastHeldPack()
    {
        for (int i = _handFanOrder.Count - 1; i >= 0; i--)
        {
            HandFanEntry entry = _handFanOrder[i];
            if (entry.Pack != null && entry.Pack.IsHeld)
            {
                _selectedIndex = GetFanIndexForPack(entry.Pack);
                return;
            }
        }
    }

    void UpdatePackPickupFlight()
    {
        if (_handFanOrder.Count == 0 || _handAnchor == null)
            return;

        HandFanLayoutSettings layout = BuildLayoutSettings();
        int fanCount = Mathf.Max(1, GetHandFanCount());

        for (int i = 0; i < _handFanOrder.Count; i++)
        {
            WorldBoosterPack pack = _handFanOrder[i].Pack;
            if (pack == null || pack.State != WorldBoosterPack.PackState.FlyingToHand)
                continue;

            int fanIndex = GetFanIndexForPack(pack);
            HandCardPose targetPose = HandFanLayout.GetPose(fanIndex, fanCount, layout, false);
            Vector3 targetWorldPos = _handAnchor.TransformPoint(targetPose.LocalPosition);
            Quaternion targetWorldRot = _handAnchor.rotation * targetPose.LocalRotation;
            pack.UpdatePickupFlight(targetWorldPos, targetWorldRot);
        }
    }

    public bool TryDropSelectedCard()
    {
        if (_cards.Count == 0 || IsHandInputLocked || IsPackSelected)
            return false;

        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null)
            return false;

        if (!TryTakeSelectedHeldCard(out WorldCard selectedCard))
            return false;

        Vector3 handPos = selectedCard.transform.position;
        Ray aimRay = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 aimPoint = GetReticleAimPoint(aimRay);
        Vector3 throwDirection = aimPoint - handPos;
        if (throwDirection.sqrMagnitude < 0.0001f)
            throwDirection = aimRay.direction;
        else
            throwDirection.Normalize();

        selectedCard.DropWithPhysics(throwDirection * throwSpeed, dropScaleTransitionDuration);
        GameSoundEffects.Play(GameSoundEffects.Id.CardThrow);
        return true;
    }

    Vector3 GetReticleAimPoint(Ray aimRay)
    {
        Vector3 aimPoint = aimRay.GetPoint(throwAimFallbackDistance);

        CardLayers.EnsureInitialized();
        int mask = ~CardLayers.WorldCardMask;
        int hitCount = Physics.RaycastNonAlloc(
            aimRay,
            ThrowAimHits,
            throwAimFallbackDistance,
            mask,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = ThrowAimHits[i];
            if (hit.collider == null)
                continue;
            if (hit.collider.GetComponentInParent<FirstPersonController>() != null)
                continue;
            if (hit.distance >= bestDistance)
                continue;

            bestDistance = hit.distance;
            aimPoint = hit.point;
        }

        return aimPoint;
    }

    void ClampSelectionIndex()
    {
        int fanCount = GetHandFanCount();
        if (fanCount == 0)
        {
            _selectedIndex = 0;
            return;
        }

        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, fanCount - 1);

        if (!IsHandFanIndexSelectable(_selectedIndex))
        {
            for (int i = 0; i < fanCount; i++)
            {
                if (IsHandFanIndexSelectable(i))
                {
                    _selectedIndex = i;
                    return;
                }
            }
        }
    }

    void UpdatePickupFlights()
    {
        if (_cards.Count == 0 || _handAnchor == null)
            return;

        HandFanLayoutSettings layout = BuildLayoutSettings();
        int fanCount = Mathf.Max(1, GetHandFanCount());

        for (int i = 0; i < _cards.Count; i++)
        {
            WorldCard card = _cards[i];
            if (!card.IsFlyingToHand)
                continue;

            HandCardPose targetPose = HandFanLayout.GetPose(GetFanIndexForCard(card), fanCount, layout, false);
            Vector3 targetWorldPos = _handAnchor.TransformPoint(targetPose.LocalPosition);
            Quaternion targetWorldRot = _handAnchor.rotation * targetPose.LocalRotation;
            card.UpdatePickupFlight(targetWorldPos, targetWorldRot);
        }
    }

    void ApplyFanLayout()
    {
        if (_handAnchor == null)
            return;

        ClampSelectionIndex();

        int fanCount = GetHandFanCount();
        if (fanCount == 0)
            return;

        HandFanLayoutSettings layout = BuildLayoutSettings();
        WorldBoosterPack selectedPack = GetSelectedHeldPack();
        bool packSelected = selectedPack != null;
        WorldCard selectedCard = packSelected ? null : GetSelectedHeldCard();
        int fanIndex = 0;

        for (int i = 0; i < _handFanOrder.Count; i++)
        {
            HandFanEntry entry = _handFanOrder[i];
            if (!EntryOccupiesFanSlot(entry))
                continue;

            if (entry.Card != null)
            {
                WorldCard card = entry.Card;
                if (card.IsFlyingToHand)
                    continue;

                bool isSelected = !packSelected && selectedCard != null && card == selectedCard;
                card.ApplyFanPose(fanIndex, fanCount, layout, isSelected);
                card.SetHandSelected(isSelected);
                card.transform.SetSiblingIndex(fanIndex);
                fanIndex++;
                continue;
            }

            WorldBoosterPack pack = entry.Pack;
            if (pack == null || !pack.IsHeld)
            {
                pack?.SetHandSelected(false);
                continue;
            }

            bool isPackSelected = packSelected && pack == selectedPack;
            HandCardPose packPose = HandFanLayout.GetPose(fanIndex, fanCount, layout, isPackSelected);
            pack.ApplyHeldPose(packPose.LocalPosition, packPose.LocalRotation, packPose.Scale);
            pack.SetHandSelected(isPackSelected);
            pack.transform.SetSiblingIndex(fanIndex);
            fanIndex++;
        }

        if (selectedCard != null)
            selectedCard.transform.SetAsLastSibling();
        else if (selectedPack != null)
            selectedPack.transform.SetAsLastSibling();
    }

    WorldCard GetSelectedHeldCard()
    {
        if (IsPackSelected || _cards.Count == 0)
            return null;

        if (!TryGetEntryAtFanIndex(_selectedIndex, out HandFanEntry entry))
            return null;

        WorldCard card = entry.Card;
        return card != null && card.IsHeld ? card : null;
    }

    int CountHeldCards()
    {
        int heldCount = 0;
        for (int i = 0; i < _cards.Count; i++)
        {
            if (_cards[i].IsHeld)
                heldCount++;
        }

        return heldCount;
    }

    HandFanLayoutSettings BuildLayoutSettings()
    {
        return new HandFanLayoutSettings
        {
            HeldScale = EffectiveHeldScale,
            CardPitchDegrees = cardPitchDegrees,
            CardDepthStep = cardDepthStep,
            CardVisualOffsetY = cardVisualOffsetY,
            MinFanAngle = minFanAngle,
            MaxFanAngle = maxFanAngle,
            FanAngleRampCardSpan = fanAngleRampCardSpan,
            FanAngleHardCap = fanAngleHardCap,
            Radius = radius,
            FanPivotY = fanPivotY,
            VerticalCurve = verticalCurve,
            MaxWidth = maxWidth,
            ExtraMaxWidthPerCard = extraMaxWidthPerCard,
            MaxWidthClamp = maxWidthClamp,
            MinCardSpacing = minCardSpacing,
            MaxCardSpacing = maxCardSpacing,
            SelectedLift = CardDimensions.Height * EffectiveHeldScale * selectedLiftPercent,
            SelectedForwardMargin = selectedForwardMargin,
        };
    }

    struct HandFanEntry
    {
        public WorldCard Card;
        public WorldBoosterPack Pack;
    }
}
