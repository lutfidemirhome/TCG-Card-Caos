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
    [SerializeField] [Range(0f, 0.65f)] float bottomClipPercent = 0.36f;
    [SerializeField] float handDownwardOffset = 0.019f;

    [Header("Fan layout (VoodooDeck-style arc)")]
    [SerializeField] float minFanAngle = 9f;
    [SerializeField] float maxFanAngle = 36f;
    [SerializeField] float fanAngleRampCardSpan = 8f;
    [SerializeField] float fanAngleHardCap = 40f;
    [SerializeField] float radius = 0.252f;
    [SerializeField] float fanPivotY = -0.032f;
    [SerializeField] float verticalCurve = 0.018f;
    [SerializeField] float cardPitchDegrees = 0f;
    [SerializeField] float heldCardScale = 1.85f;
    [Tooltip("Hand cards render this much smaller than heldCardScale (layout uses the reduced size too).")]
    [SerializeField] [Range(0f, 0.5f)] float handScaleReductionPercent = 0.1f;
    [SerializeField] float cardDepthStep = 0.0025f;
    [SerializeField] float cardVisualOffsetY = 0f;

    [Header("Fan width budget")]
    [SerializeField] float maxWidth = 0.231f;
    [SerializeField] float extraMaxWidthPerCard = 0.0063f;
    [SerializeField] float maxWidthClamp = 0.441f;
    [SerializeField] float minCardSpacing = 0.0126f;
    [SerializeField] float maxCardSpacing = 0.042f;

    [Header("Hand selection")]
    [Tooltip("Screen-up lift for the selected card as a fraction of held card height.")]
    [SerializeField] [Range(0f, 0.5f)] float selectedLiftPercent = 0.25f;
    [SerializeField] float selectedForwardOffset = 0.008f;

    [Header("Pickup flight")]
    [SerializeField] float pickupFlightDuration = 0.4f;
    [SerializeField] float pickupFlightArcHeight = 0.22f;

    [Header("Throw")]
    [SerializeField] float throwSpeed = 4.5f;
    [SerializeField] float throwUpBoost = 0.2f;
    [SerializeField] KeyCode dropKey = KeyCode.Q;

    Transform _handAnchor;
    Camera _camera;
    int _selectedIndex;
    readonly List<WorldCard> _cards = new List<WorldCard>();

    public int Count => _cards.Count;
    public bool IsFull => _cards.Count >= CardDimensions.MaxHandSize;
    public int SelectedIndex => _selectedIndex;
    public float EffectiveHeldScale => heldCardScale * (1f - handScaleReductionPercent);

    void Awake()
    {
        _camera = Camera.main;
        EnsureHandAnchor();
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
        if (_cards.Count == 0)
            return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scroll, 0f))
            return;

        int direction = scroll > 0f ? 1 : -1;
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
                return;
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
        if (card == null || card.IsInHand || IsFull)
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

    public bool TryDropSelectedCard()
    {
        if (_cards.Count == 0)
            return false;

        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null)
            return false;

        WorldCard selectedCard = GetSelectedHeldCard();
        if (selectedCard == null)
            return false;

        _cards.Remove(selectedCard);

        Vector3 throwDirection = (_camera.transform.forward + _camera.transform.up * throwUpBoost).normalized;
        selectedCard.DropWithPhysics(throwDirection * throwSpeed);

        ClampSelectionIndex();
        ApplyFanLayout();
        return true;
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
            SelectedForwardOffset = selectedForwardOffset,
        };
    }
}
