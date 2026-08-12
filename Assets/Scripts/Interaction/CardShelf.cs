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
    [SerializeField] float surfacePadding = 0.003f;

    [Header("Cabinet category")]
    [Tooltip("Only cards with the same Shelf Category Id can be placed here.")]
    [SerializeField] string categoryId = CardShelfCategories.NormalCommon;

    readonly List<CardShelfSlot> _slots = new List<CardShelfSlot>(32);

    Vector3 _aimWorldPoint;
    bool _hasAimPoint;
    GameObject _placementOutline;
    CardShelfSlot _previewSlot;

    void Awake()
    {
        if (placementRoot == null)
            placementRoot = transform;
        RefreshSlotCache();
    }

    void OnDestroy()
    {
        DestroyPlacementOutline();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        GetComponent<CardShelfCategoryLabel>()?.ScheduleRebuild();
    }
#endif

    public void RefreshSlotCache()
    {
        _slots.Clear();
        GetComponentsInChildren(true, _slots);
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

    public string CategoryId => categoryId;

    public string CategoryDisplayName => CardShelfCategories.GetDisplayName(categoryId);

    public bool AcceptsDefinition(CardDefinition definition)
    {
        return CardShelfRules.CanPlaceOnShelf(categoryId, definition);
    }

    public bool AcceptsCard(WorldCard card)
    {
        return card != null && card.HasShelfRules && AcceptsDefinition(card.Definition);
    }

    public bool CanPlaceCardInSlot(WorldCard card, CardShelfSlot slot)
    {
        return card != null && slot != null && slot.IsEmpty;
    }

    public bool IsCorrectPlacement(WorldCard card, CardShelfSlot slot)
    {
        if (card == null || slot == null)
            return false;

        return CardShelfRules.IsCorrectShelfPlacement(categoryId, card.Definition, slot);
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
            if (selectedCard == null)
            {
                HidePlacementOutline();
                return string.Empty;
            }

            RefreshOccupancy();
            CardShelfSlot slot = FindAimTargetSlot();
            if (slot == null)
            {
                HidePlacementOutline();
                return HasAnySlots() ? "Shelf Full" : "No Shelf Slots";
            }

            return "Press [E] To Place Card";
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
        bool aimOnOccupied = IsAimOnOccupiedSlot(aim);

        if (hand.HasSelectedHeldCard() && !aimOnOccupied)
        {
            if (!hand.TryTakeSelectedHeldCard(out WorldCard card))
                return;

            RefreshOccupancy();
            CardShelfSlot slot = FindAimTargetSlot();
            if (slot == null || !CanPlaceCardInSlot(card, slot))
            {
                hand.TryPickup(card);
                return;
            }

            PlaceCardInSlot(card, slot);
            ClearAim();
            return;
        }
    }

    public bool IsAimOnOccupiedSlot(Vector3 aim)
    {
        RefreshOccupancy();
        CardShelfSlot closest = FindClosestSlot(aim);
        return closest != null && !closest.IsEmpty;
    }

    public void PlaceCardInSlot(WorldCard card, CardShelfSlot slot)
    {
        if (card == null || slot == null)
            return;

        card.PlaceOnShelfSlot(slot.transform, surfacePadding);
        slot.Occupy(card);
        card.NotifyShelfPlacement(IsCorrectPlacement(card, slot));
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

            float dist = (slotPos - aim).sqrMagnitude;
            if (dist < bestOnLevelDist)
            {
                bestOnLevelDist = dist;
                bestOnLevel = slot;
            }
        }

        return bestOnLevel;
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
        meshRenderer.sharedMaterial = CardVisualResources.InteractionOutlineMaterial;
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

    Vector3 GetFaceDirection(Vector3 fromPoint)
    {
        Camera camera = Camera.main;
        if (camera != null)
        {
            Vector3 toCamera = camera.transform.position - fromPoint;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.0001f)
                return toCamera.normalized;
        }

        Vector3 outward = -placementRoot.forward;
        outward.y = 0f;
        if (outward.sqrMagnitude > 0.0001f)
            return outward.normalized;

        return Vector3.forward;
    }

    /// <summary>Editor helper: keep API for old menu; now just refreshes marker cache.</summary>
    public void RebuildLevelsFromColliders()
    {
        if (placementRoot == null)
            placementRoot = transform;
        RefreshSlotCache();
    }

    static PlayerCardHand FindHand()
    {
        return FindFirstObjectByType<PlayerCardHand>();
    }

    static PlayerCardHand ResolveHand(GameObject interactor)
    {
        if (interactor == null)
            return FindHand();

        PlayerCardHand hand = interactor.GetComponent<PlayerCardHand>();
        if (hand != null)
            return hand;

        hand = interactor.GetComponentInChildren<PlayerCardHand>();
        if (hand != null)
            return hand;

        return FindHand();
    }
}
