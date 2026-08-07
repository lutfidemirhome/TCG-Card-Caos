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

    public void RefreshSlotCache()
    {
        _slots.Clear();
        GetComponentsInChildren(true, _slots);
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
        if (hand == null || !hand.HasSelectedHeldCard())
        {
            HidePlacementOutline();
            return string.Empty;
        }

        RefreshOccupancy();
        if (FindTargetSlot() == null)
        {
            HidePlacementOutline();
            return HasAnySlots() ? "Shelf Full" : "No Shelf Slots";
        }

        return "Press [E] To Place Card";
    }

    public void Interact(GameObject interactor)
    {
        PlayerCardHand hand = ResolveHand(interactor);
        if (hand == null || !hand.TryTakeSelectedHeldCard(out WorldCard card))
            return;

        RefreshOccupancy();
        CardShelfSlot slot = FindTargetSlot();
        if (slot == null)
        {
            // Put the card back into the world near aim so it is not lost.
            Vector3 fallback = _hasAimPoint ? _aimWorldPoint : transform.position;
            card.PlaceUprightOnShelf(placementRoot, fallback, GetFaceDirection(fallback));
            ClearAim();
            return;
        }

        PlaceCardInSlot(card, slot);
        ClearAim();
    }

    public void PlaceCardInSlot(WorldCard card, CardShelfSlot slot)
    {
        if (card == null || slot == null)
            return;

        Vector3 surfacePoint = slot.transform.position + slot.transform.up * surfacePadding;
        Vector3 face = slot.transform.forward;
        face.y = 0f;
        if (face.sqrMagnitude < 0.0001f)
            face = GetFaceDirection(surfacePoint);

        card.PlaceUprightOnShelf(slot.transform, surfacePoint, face.normalized);
        slot.Occupy(card);
    }

    void RefreshPlacementPreview()
    {
        PlayerCardHand hand = FindHand();
        if (hand == null || !hand.HasSelectedHeldCard() || !_hasAimPoint)
        {
            HidePlacementOutline();
            return;
        }

        RefreshOccupancy();
        CardShelfSlot slot = FindTargetSlot();
        if (slot == null)
        {
            HidePlacementOutline();
            return;
        }

        _previewSlot = slot;
        ShowPlacementOutline(slot);
    }

    CardShelfSlot FindTargetSlot()
    {
        if (!HasAnySlots())
            return null;

        // 1) Prefer empty slots on the aimed shelf level (similar Y).
        CardShelfSlot bestOnLevel = null;
        float bestOnLevelDist = float.MaxValue;
        CardShelfSlot bestAny = null;
        float bestAnyDist = float.MaxValue;

        Vector3 aim = _hasAimPoint ? _aimWorldPoint : transform.position;

        for (int i = 0; i < _slots.Count; i++)
        {
            CardShelfSlot slot = _slots[i];
            if (slot == null || !slot.IsEmpty)
                continue;

            Vector3 slotPos = slot.transform.position;
            float dist = (slotPos - aim).sqrMagnitude;
            if (dist < bestAnyDist)
            {
                bestAnyDist = dist;
                bestAny = slot;
            }

            if (!_hasAimPoint || Mathf.Abs(slotPos.y - aim.y) > levelYTolerance)
                continue;

            if (dist < bestOnLevelDist)
            {
                bestOnLevelDist = dist;
                bestOnLevel = slot;
            }
        }

        // Nearest empty seat on the looked-at board; otherwise nearest empty anywhere.
        if (bestOnLevel != null)
            return bestOnLevel;
        return bestAny;
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
        Vector3 face = slot.transform.forward;
        face.y = 0f;
        if (face.sqrMagnitude < 0.0001f)
            face = GetFaceDirection(slot.transform.position);
        Quaternion upright = Quaternion.LookRotation(face.normalized, Vector3.up);
        Vector3 outlinePos = slot.transform.position + Vector3.up * (halfHeight + surfacePadding);

        Transform t = _placementOutline.transform;
        t.SetPositionAndRotation(outlinePos, upright);
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
