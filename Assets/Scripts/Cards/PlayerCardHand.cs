using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds up to 10 cards in a bottom-center fan. Newest card goes to the right.
/// A held booster pack occupies the rightmost fan slot (scroll-selectable, not shelf-placeable).
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
    [SerializeField] float openRevealHeight = 0.02f;
    [SerializeField] float openSequenceDuration = 1.35f;
    [SerializeField] float openPackAnticipationHold = 1f;
    [SerializeField] KeyCode openPackKey = KeyCode.Return;

    static readonly RaycastHit[] ThrowAimHits = new RaycastHit[8];

    Transform _handAnchor;
    Camera _camera;
    int _selectedIndex;
    readonly List<WorldCard> _cards = new List<WorldCard>();
    WorldBoosterPack _heldPack;
    bool _handInputLocked;
    bool _isOpeningPack;
    bool _awaitingRevealCollect;
    bool _revealCollectRequested;
    Coroutine _openPackRoutine;

    public int Count => _cards.Count;
    public static PlayerCardHand Instance { get; private set; }
    public bool IsFull => _cards.Count >= CardDimensions.MaxHandSize;
    public int AvailableSlots => CardDimensions.MaxHandSize - _cards.Count;
    public int SelectedIndex => _selectedIndex;
    public float EffectiveHeldScale => heldCardScale * (1f - handScaleReductionPercent);
    public bool HasHeldPack =>
        _heldPack != null
        && (_heldPack.IsHeld || _heldPack.State == WorldBoosterPack.PackState.FlyingToHand);
    public bool IsPackSelected =>
        HasHeldPack && _heldPack.IsHeld && _selectedIndex == GetPackFanIndex();
    public bool IsOpeningPack => _isOpeningPack;
    public bool IsAwaitingRevealCollect => _awaitingRevealCollect;
    public bool IsHandInputLocked => _handInputLocked || _isOpeningPack;
    public float OpenRevealDistance => openRevealDistance;
    public float OpenRevealHeight => openRevealHeight;
    public float OpenSequenceDuration => openSequenceDuration;
    public float OpenPackAnticipationHold => openPackAnticipationHold;
    public float PickupFlightArcHeight => pickupFlightArcHeight;

    public bool CanPickUpPack =>
        !IsHandInputLocked
        && _heldPack == null
        && AvailableSlots >= CardDimensions.CardsPerBoosterPack;

    public bool CanOpenSelectedPack =>
        IsPackSelected
        && !IsHandInputLocked
        && AvailableSlots >= CardDimensions.CardsPerBoosterPack;

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

        if (Input.GetKeyDown(openPackKey) && IsPackSelected)
            TryOpenSelectedPack();

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
        if (fanIndex == GetPackFanIndex())
            return _heldPack != null && _heldPack.IsHeld;

        return GetHeldCardAtFanIndex(fanIndex) != null;
    }

    WorldCard GetHeldCardAtFanIndex(int fanIndex)
    {
        int currentFan = 0;
        for (int i = 0; i < _cards.Count; i++)
        {
            if (!_cards[i].IsHeld)
                continue;

            if (currentFan == fanIndex)
                return _cards[i];

            currentFan++;
        }

        return null;
    }

    int GetHandFanCount()
    {
        int count = CountHeldCards();
        if (_heldPack != null
            && (_heldPack.IsHeld || _heldPack.State == WorldBoosterPack.PackState.FlyingToHand))
            count++;

        return count;
    }

    int GetPackFanIndex()
    {
        return CountHeldCards();
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

        if (_heldPack != null && _cards.Count >= CardDimensions.MaxCardsWhileHoldingPack)
            return false;

        EnsureHandAnchor();
        UpdateHandAnchorTransform();

        _cards.Add(card);
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

        if (!HasHeldPack)
            _selectedIndex = GetFanIndexForCard(_cards[cardIndex]);
    }

    int GetFanIndexForCard(WorldCard card)
    {
        int fanIndex = 0;
        for (int i = 0; i < _cards.Count; i++)
        {
            if (!_cards[i].IsHeld)
                continue;

            if (_cards[i] == card)
                return fanIndex;

            fanIndex++;
        }

        return 0;
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
        _heldPack = pack;

        pack.BeginPickupFlight(
            _handAnchor,
            EffectiveHeldScale,
            pickupFlightDuration,
            pickupFlightArcHeight,
            OnPackPickupFlightComplete);
        GameSoundEffects.Play(GameSoundEffects.Id.CardPickup);
        return true;
    }

    void OnPackPickupFlightComplete()
    {
        if (_heldPack == null || !_heldPack.IsHeld)
            return;

        _selectedIndex = GetPackFanIndex();
    }

    public bool TryOpenSelectedPack()
    {
        if (!CanOpenSelectedPack || _openPackRoutine != null)
            return false;

        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null)
            return false;

        _isOpeningPack = true;
        _openPackRoutine = StartCoroutine(OpenHeldPackRoutine());
        return true;
    }

    System.Collections.IEnumerator OpenHeldPackRoutine()
    {
        WorldBoosterPack pack = _heldPack;
        yield return PackOpenSequence.Run(this, pack, _camera);
        _isOpeningPack = false;
        _openPackRoutine = null;
    }

    public void ClearHeldPackReference()
    {
        _heldPack = null;
        ClampSelectionIndex();
    }

    public void SetHandInputLocked(bool locked)
    {
        _handInputLocked = locked;
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
        return _awaitingRevealCollect ? "Press [Enter] To Collect Cards" : string.Empty;
    }

    public void AddRevealedCard(WorldCard card, float duration, float arcHeight)
    {
        if (card == null)
            return;

        EnsureHandAnchor();
        UpdateHandAnchorTransform();

        _cards.Add(card);
        int newCardIndex = _cards.Count - 1;
        _selectedIndex = newCardIndex;

        card.BeginPickupFlight(
            _handAnchor,
            EffectiveHeldScale,
            duration,
            arcHeight,
            () => OnCardPickupFlightComplete(newCardIndex));
    }

    public bool TryDropHeldPack()
    {
        if (!IsPackSelected || IsHandInputLocked)
            return false;

        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null)
            return false;

        WorldBoosterPack pack = _heldPack;
        _heldPack = null;
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
        if (!IsPackSelected)
            return string.Empty;

        if (CanOpenSelectedPack)
            return "Press [Enter] To Open Pack";

        return "Need "
            + CardDimensions.CardsPerBoosterPack
            + " free hand slots to open pack";
    }

    void UpdatePackPickupFlight()
    {
        if (_heldPack == null
            || _heldPack.State != WorldBoosterPack.PackState.FlyingToHand
            || _handAnchor == null)
            return;

        HandFanLayoutSettings layout = BuildLayoutSettings();
        int fanCount = Mathf.Max(1, GetHandFanCount());
        HandCardPose targetPose = HandFanLayout.GetPose(GetPackFanIndex(), fanCount, layout, false);
        Vector3 targetWorldPos = _handAnchor.TransformPoint(targetPose.LocalPosition);
        Quaternion targetWorldRot = _handAnchor.rotation * targetPose.LocalRotation;
        _heldPack.UpdatePickupFlight(targetWorldPos, targetWorldRot);
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

            HandCardPose targetPose = HandFanLayout.GetPose(i, fanCount, layout, false);
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
        bool packSelected = IsPackSelected;
        WorldCard selectedCard = packSelected ? null : GetSelectedHeldCard();
        int cardFanIndex = 0;

        for (int i = 0; i < _cards.Count; i++)
        {
            WorldCard card = _cards[i];
            if (card.IsFlyingToHand)
                continue;

            bool isSelected = !packSelected && selectedCard != null && card == selectedCard;
            card.ApplyFanPose(cardFanIndex, fanCount, layout, isSelected);
            card.SetHandSelected(isSelected);
            card.transform.SetSiblingIndex(cardFanIndex);
            cardFanIndex++;
        }

        if (_heldPack != null && _heldPack.IsHeld && !IsOpeningPack)
        {
            HandCardPose packPose = HandFanLayout.GetPose(GetPackFanIndex(), fanCount, layout, packSelected);
            _heldPack.ApplyHeldPose(packPose.LocalPosition, packPose.LocalRotation, packPose.Scale);
            _heldPack.SetHandSelected(packSelected);
            _heldPack.transform.SetAsLastSibling();
        }
        else if (_heldPack != null)
        {
            _heldPack.SetHandSelected(false);
        }

        if (selectedCard != null)
            selectedCard.transform.SetAsLastSibling();
    }

    WorldCard GetSelectedHeldCard()
    {
        if (IsPackSelected || _cards.Count == 0)
            return null;

        WorldCard selected = GetHeldCardAtFanIndex(_selectedIndex);
        if (selected != null && selected.IsHeld)
            return selected;

        for (int i = 0; i < _cards.Count; i++)
        {
            if (_cards[i].IsHeld)
                return _cards[i];
        }

        return null;
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
}
