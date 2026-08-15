using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds up to 10 cards in a bottom-center fan. Newest card goes to the right.
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

    static readonly RaycastHit[] ThrowAimHits = new RaycastHit[8];

    Transform _handAnchor;
    Camera _camera;
    int _selectedIndex;
    readonly List<WorldCard> _cards = new List<WorldCard>();

    public int Count => _cards.Count;
    public static PlayerCardHand Instance { get; private set; }
    public bool IsFull => _cards.Count >= CardDimensions.MaxHandSize;
    public int AvailableSlots => CardDimensions.MaxHandSize - _cards.Count;
    public int SelectedIndex => _selectedIndex;
    public float EffectiveHeldScale => heldCardScale * (1f - handScaleReductionPercent);

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

        HandleScrollSelection();

        if (Input.GetKeyDown(dropKey))
            TryDropSelectedCard();
    }

    void LateUpdate()
    {
        UpdateHandAnchorTransform();
        UpdatePickupFlights();

        if (_cards.Count > 0)
            ApplyFanLayout();
    }

    void HandleScrollSelection()
    {
        if (_cards.Count <= 1)
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
        if (_cards.Count == 0 || direction == 0)
            return;

        int count = _cards.Count;
        int start = _selectedIndex;

        for (int step = 0; step < count; step++)
        {
            _selectedIndex = (_selectedIndex + direction) % count;
            if (_selectedIndex < 0)
                _selectedIndex += count;

            if (_cards[_selectedIndex].IsHeld)
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
        if (card == null || card.IsInHand || card.IsFlyingToShelf || IsFull)
            return false;

        EnsureHandAnchor();
        UpdateHandAnchorTransform();

        _cards.Add(card);
        int newCardIndex = _cards.Count - 1;

        if (CountHeldCards() == 0)
            _selectedIndex = newCardIndex;

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

        _selectedIndex = cardIndex;
    }

    public bool HasSelectedHeldCard()
    {
        return GetSelectedHeldCard() != null;
    }

    public WorldCard SelectedHeldCard => GetSelectedHeldCard();

    /// <summary>
    /// Removes the selected held card from the hand without throwing it.
    /// Used for placing onto shelves / surfaces.
    /// </summary>
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

    public bool TryDropSelectedCard()
    {
        if (_cards.Count == 0)
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
        if (_cards.Count == 0)
        {
            _selectedIndex = 0;
            return;
        }

        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _cards.Count - 1);
    }

    void UpdatePickupFlights()
    {
        if (_cards.Count == 0 || _handAnchor == null)
            return;

        HandFanLayoutSettings layout = BuildLayoutSettings();

        for (int i = 0; i < _cards.Count; i++)
        {
            WorldCard card = _cards[i];
            if (!card.IsFlyingToHand)
                continue;

            // Flying cards never use selected styling until they land.
            HandCardPose targetPose = HandFanLayout.GetPose(i, i + 1, layout, false);
            Vector3 targetWorldPos = _handAnchor.TransformPoint(targetPose.LocalPosition);
            Quaternion targetWorldRot = _handAnchor.rotation * targetPose.LocalRotation;
            card.UpdatePickupFlight(targetWorldPos, targetWorldRot);
        }
    }

    void ApplyFanLayout()
    {
        if (_cards.Count == 0 || _handAnchor == null)
            return;

        ClampSelectionIndex();

        int heldCount = CountHeldCards();
        if (heldCount == 0)
            return;

        HandFanLayoutSettings layout = BuildLayoutSettings();
        int heldFanIndex = 0;
        WorldCard selectedCard = GetSelectedHeldCard();

        for (int i = 0; i < _cards.Count; i++)
        {
            WorldCard card = _cards[i];
            if (card.IsFlyingToHand)
                continue;

            bool isSelected = selectedCard != null && card == selectedCard;
            card.ApplyFanPose(heldFanIndex, heldCount, layout, isSelected);
            card.transform.SetSiblingIndex(heldFanIndex);
            heldFanIndex++;
        }

        if (selectedCard != null)
            selectedCard.transform.SetAsLastSibling();
    }

    WorldCard GetSelectedHeldCard()
    {
        if (_cards.Count == 0)
            return null;

        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _cards.Count - 1);
        if (_cards[_selectedIndex].IsHeld)
            return _cards[_selectedIndex];

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
