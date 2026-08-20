using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// One PSA display seat. Slot numbers 7–10 label which PSA cards belong here.
/// Move <see cref="slotMarker"/> in the prefab (yellow editor preview) — placed cards fly there.
/// Forward (blue axis) = slab face direction when placed.
/// </summary>
[ExecuteAlways]
public class PsaCabinetSlot : MonoBehaviour, IInteractable
{
    const string LabelObjectName = "SlotNumberLabel";
    const string LabelTextObjectName = "Text";
    const string SlotMarkerName = "PsaSlotMarker";
    const string PreviewObjectName = "SlotCardPreview";
    const string AimColliderName = "PsaSlotAimCollider";
    const string DefaultHolderOutlineTargetName = "Tutucu2_Visual";

    static readonly Color PreviewFillColor = new Color(1f, 0.82f, 0.12f, 0.45f);
    static readonly Color PreviewEdgeColor = new Color(1f, 0.92f, 0.2f, 0.95f);

    static Mesh _sharedCardPlaneMesh;
    static Material _sharedFillMaterial;
    static Material _sharedEdgeMaterial;

    [SerializeField] int slotNumber = 7;
    [SerializeField] int defaultVariantIndex = 1;
    [SerializeField] WorldCard occupiedCard;

    [Header("Placement")]
    [Tooltip("Yellow editor preview + runtime target. Blue axis = card face.")]
    [SerializeField] Transform slotMarker;
    [Tooltip("Card parents here. Defaults to holder root.")]
    [SerializeField] Transform placementParent;
    [Tooltip("Extra size multiplier on top of the slot marker's scale. 1 = marker scale exactly.")]
    [SerializeField] float placementWorldScale = 1f;
    [Tooltip("Extra rotation for the placed slab. Set Y to 180 if the card back shows.")]
    [SerializeField] Vector3 placedCardLocalEuler = Vector3.zero;

    [Header("Placement flight")]
    [SerializeField] float placementFlightDuration = 0.4f;
    [SerializeField] float placementFlightArcHeight = 0.18f;

    [Header("Aim highlight")]
    [Tooltip("QuickOutline target while aiming with a matching held PSA card (same as ground PSA hover).")]
    [SerializeField] Transform holderOutlineTarget;

    [Header("Slot number label")]
    [SerializeField] bool showSlotLabel = true;
    [Tooltip("Optional anchor on the holder face. When empty, labelLocalOffset is used.")]
    [SerializeField] Transform labelAnchor;
    [SerializeField] Vector3 labelLocalOffset = new Vector3(0f, 0.05f, 0.015f);
    [SerializeField] Vector3 labelLocalEuler = new Vector3(0f, 180f, 0f);
    [SerializeField] float labelCanvasScale = 0.00035f;
    [SerializeField] int labelFontSize = 120;
    [SerializeField] Color labelColor = new Color(0.12f, 0.12f, 0.12f, 1f);

    RectTransform _labelCanvasRect;
    Text _labelText;
    Transform _previewRoot;
    MeshRenderer _previewFillRenderer;
    MeshRenderer _previewEdgeRenderer;
    bool _previewDeferred;
    Outline _holderOutline;
    bool _holderOutlineActive;
    BoxCollider _aimCollider;

    struct PlacementFlightEntry
    {
        public WorldCard Card;
    }

    readonly System.Collections.Generic.List<PlacementFlightEntry> _placementFlights =
        new System.Collections.Generic.List<PlacementFlightEntry>(2);

    public int SlotNumber => slotNumber;
    public int DefaultVariantIndex => Mathf.Max(1, defaultVariantIndex);
    public bool IsEmpty
    {
        get
        {
            if (occupiedCard == null)
                return true;

            if (occupiedCard.IsInHand)
            {
                occupiedCard = null;
                return true;
            }

            return false;
        }
    }
    public WorldCard OccupiedCard => occupiedCard;

    void Awake()
    {
        ResolveReferences();
        if (Application.isPlaying)
            HideEditorPreviewForPlayMode();
    }

    void OnDestroy()
    {
        SetHolderOutlineActive(false);
    }

    void OnDisable()
    {
        SetEditorPreviewActive(false);
        SetHolderOutlineActive(false);
    }

    void LateUpdate()
    {
        UpdatePlacementFlights();

        if (Application.isPlaying)
        {
            HideEditorPreviewForPlayMode();
            return;
        }

#if UNITY_EDITOR
        if (IsEditingPrefabAsset())
            return;
#endif

        EnsureEditorPreview();
        RefreshEditorPreviewVisibility();
    }

    void ResolveReferences()
    {
        if (slotMarker == null)
            slotMarker = transform.Find(SlotMarkerName);

        if (placementParent == null)
            placementParent = transform;

        if (holderOutlineTarget == null)
            holderOutlineTarget = transform.Find(DefaultHolderOutlineTargetName);
    }

    Transform GetSlotMarker()
    {
        ResolveReferences();
        if (slotMarker != null)
            return slotMarker;

        return transform;
    }

    Transform GetPlacementParent()
    {
        ResolveReferences();
        return placementParent != null ? placementParent : transform;
    }

    /// <summary>
    /// Slab size is driven by the marker's own scale, so resizing the marker in the scene resizes
    /// both the preview and the placed card. The marker may be scaled non-uniformly while tuning,
    /// so the slab takes the geometric mean and never inherits the stretch.
    /// </summary>
    float ResolveUniformWorldScale(Transform marker)
    {
        Vector3 markerScale = marker != null ? marker.lossyScale : Vector3.one;
        float x = Mathf.Max(0.0001f, Mathf.Abs(markerScale.x));
        float y = Mathf.Max(0.0001f, Mathf.Abs(markerScale.y));
        float z = Mathf.Max(0.0001f, Mathf.Abs(markerScale.z));
        float multiplier = placementWorldScale > 0f ? placementWorldScale : 1f;
        return Mathf.Pow(x * y * z, 1f / 3f) * multiplier;
    }

    Vector3 ResolvePlacementLocalScale(Transform marker)
    {
        float uniform = ResolveUniformWorldScale(marker);
        Vector3 markerScale = marker != null ? marker.lossyScale : Vector3.one;
        return new Vector3(
            uniform / Mathf.Max(0.0001f, Mathf.Abs(markerScale.x)),
            uniform / Mathf.Max(0.0001f, Mathf.Abs(markerScale.y)),
            uniform / Mathf.Max(0.0001f, Mathf.Abs(markerScale.z)));
    }

    void BuildPlacementLocalPose(
        out Transform parent,
        out Vector3 localPosition,
        out Quaternion localRotation,
        out Vector3 localScale,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        Transform marker = GetSlotMarker();
        parent = marker;

        localPosition = Vector3.zero;
        localRotation = Quaternion.Euler(placedCardLocalEuler);
        localScale = ResolvePlacementLocalScale(marker);

        worldPosition = marker.position;
        worldRotation = marker.rotation * localRotation;
    }

    void GetPlacementFlightPose(out Vector3 worldPosition, out Quaternion worldRotation)
    {
        Transform marker = GetSlotMarker();
        worldPosition = marker.position;
        worldRotation = marker.rotation * Quaternion.Euler(placedCardLocalEuler);
    }

    /// <summary>
    /// Slab box in marker-local space, matching exactly where the placed card mesh ends up.
    /// Preview, gizmo and aim collider all read from here so they can never drift apart.
    /// </summary>
    void GetSlabLocalBox(
        out Vector3 markerCenter,
        out Vector3 markerFaceBottomCenter,
        out Vector3 slabSize,
        out Vector3 slabScale)
    {
        Transform marker = GetSlotMarker();
        slabScale = ResolvePlacementLocalScale(marker);
        Quaternion rotation = Quaternion.Euler(placedCardLocalEuler);

        Vector3 center;
        if (PsaSlabLayoutUtility.TryGetCabinetRootBounds(out Vector3 min, out Vector3 max, out Vector3 boundsSize))
        {
            center = (min + max) * 0.5f;
            slabSize = Vector3.Scale(boundsSize, slabScale);
            markerFaceBottomCenter = rotation * Vector3.Scale(
                PsaSlabLayoutUtility.GetCabinetFaceBottomCenterLocal(),
                slabScale);
        }
        else
        {
            slabSize = Vector3.Scale(
                new Vector3(
                    PsaSlabLayoutUtility.GetCabinetFaceWidth(),
                    PsaSlabLayoutUtility.GetCabinetFaceHeight(),
                    PsaSlabLayoutUtility.GetCabinetThickness()),
                slabScale);
            center = new Vector3(0f, PsaSlabLayoutUtility.GetCabinetFaceHeight() * 0.5f, 0f);
            markerFaceBottomCenter = Vector3.zero;
        }

        markerCenter = rotation * Vector3.Scale(center, slabScale);
        slabSize = new Vector3(Mathf.Abs(slabSize.x), Mathf.Abs(slabSize.y), Mathf.Abs(slabSize.z));
    }

    public void ClearIfMatches(WorldCard card)
    {
        if (occupiedCard != card)
            return;

        occupiedCard = null;
        RefreshEditorPreviewVisibility();
    }

    public bool IsAimCollider(Collider collider)
    {
        if (collider == null)
            return false;

        ResolveReferences();

        if (holderOutlineTarget != null && IsTransformUnder(collider.transform, holderOutlineTarget))
            return true;

        // Deliberately not the holder root: the table visual under it is decoration only.
        return slotMarker != null && IsTransformUnder(collider.transform, slotMarker);
    }

    /// <summary>
    /// Aim target for the crosshair. The holder frame and stand ship without colliders and the
    /// table visual's own box is far too wide, so the slot owns a slab-sized box of its own.
    /// </summary>
    void EnsureAimCollider()
    {
        ResolveReferences();
        Transform marker = slotMarker;
        if (marker == null)
            return;

        if (_aimCollider == null)
        {
            Transform holder = marker.Find(AimColliderName);
            if (holder == null)
            {
                holder = new GameObject(AimColliderName).transform;
                holder.SetParent(marker, false);
            }

            holder.localPosition = Vector3.zero;
            holder.localRotation = Quaternion.identity;
            holder.localScale = Vector3.one;

            _aimCollider = holder.GetComponent<BoxCollider>();
            if (_aimCollider == null)
                _aimCollider = holder.gameObject.AddComponent<BoxCollider>();
        }

        GetSlabLocalBox(out Vector3 center, out _, out Vector3 slabSize, out _);

        Vector3 size = Quaternion.Euler(placedCardLocalEuler) * slabSize;
        size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

        // Thin slabs are hard to hit with the crosshair, so give the box a usable depth.
        size.z = Mathf.Max(size.z, 0.02f);

        _aimCollider.isTrigger = false;
        _aimCollider.center = center;
        _aimCollider.size = size;
    }

    static bool IsTransformUnder(Transform target, Transform root) =>
        target == root || target.IsChildOf(root);

    public void SetSlotNumber(int number)
    {
        slotNumber = PsaArtLibrary.ClampCabinetSlotNumber(number);
        RefreshLabel();
    }

    public void Occupy(WorldCard card)
    {
        occupiedCard = card;
        RefreshEditorPreviewVisibility();
    }

    public void ClearOccupant()
    {
        occupiedCard = null;
        RefreshEditorPreviewVisibility();
    }

    public Vector3 GetSpawnPosition()
    {
        return transform.position;
    }

    public Quaternion GetSpawnRotation()
    {
        return transform.rotation;
    }

    public bool AcceptsPsaCard(WorldCard card) =>
        card != null
        && card.UsesPsaSlab
        && card.PsaSlotNumber == SlotNumber;

    public bool CanPlaceHeldCard(WorldCard card) =>
        AcceptsPsaCard(card) && IsEmpty;

    public void RefreshOccupancy()
    {
        if (occupiedCard != null && occupiedCard.IsInHand)
            occupiedCard = null;
        RefreshEditorPreviewVisibility();
    }

    public Transform GetPlacementAnchor() => GetPlacementParent();

    public void GetPlacementPose(out Vector3 worldPosition, out Quaternion worldRotation)
    {
        BuildPlacementLocalPose(
            out _,
            out _,
            out _,
            out _,
            out worldPosition,
            out worldRotation);
    }

    public void SetAimHit(RaycastHit hit)
    {
        RefreshAimPreview();
    }

    public void ClearAim()
    {
        SetHolderOutlineActive(false);
    }

    public string GetPromptText()
    {
        PlayerCardHand hand = PlayerCardHand.Instance;
        if (hand == null || !hand.HasSelectedHeldCard())
        {
            SetHolderOutlineActive(false);
            return string.Empty;
        }

        WorldCard selectedCard = hand.SelectedHeldCard;
        if (selectedCard == null || !AcceptsPsaCard(selectedCard))
        {
            SetHolderOutlineActive(false);
            return string.Empty;
        }

        RefreshOccupancy();
        if (!IsEmpty)
        {
            SetHolderOutlineActive(false);
            return string.Empty;
        }

        SetHolderOutlineActive(true);
        return InteractPrompt.Format("Place PSA Card");
    }

    public void Interact(GameObject interactor)
    {
        PlayerCardHand hand = PlayerCardHandResolver.FromInteractorOrInstance(interactor);
        if (hand == null || !hand.HasSelectedHeldCard())
            return;

        RefreshOccupancy();
        WorldCard selectedCard = hand.SelectedHeldCard;
        if (selectedCard == null || !CanPlaceHeldCard(selectedCard))
            return;

        if (!hand.TryTakeSelectedHeldCard(out WorldCard card))
            return;

        if (card != selectedCard || !CanPlaceHeldCard(card))
        {
            hand.ReturnHeldCard(card);
            return;
        }

        SetHolderOutlineActive(false);
        Occupy(card);
        BeginPlacementFlight(card);
        ClearAim();
    }

    void RefreshAimPreview()
    {
        PlayerCardHand hand = PlayerCardHand.Instance;
        if (hand == null || !hand.HasSelectedHeldCard())
        {
            SetHolderOutlineActive(false);
            return;
        }

        WorldCard selectedCard = hand.SelectedHeldCard;
        if (selectedCard == null || !CanPlaceHeldCard(selectedCard))
        {
            SetHolderOutlineActive(false);
            return;
        }

        SetHolderOutlineActive(true);
    }

    void BeginPlacementFlight(WorldCard card)
    {
        if (card == null)
            return;

        GameSoundEffects.Play(GameSoundEffects.Id.CardThrow);

        _placementFlights.Add(new PlacementFlightEntry { Card = card });

        BuildPlacementLocalPose(
            out Transform parent,
            out Vector3 localPosition,
            out Quaternion localRotation,
            out Vector3 localScale,
            out _,
            out _);

        card.BeginPsaCabinetPlacementFlight(
            parent,
            localPosition,
            localRotation,
            localScale,
            ResolveUniformWorldScale(parent),
            placementFlightDuration,
            placementFlightArcHeight,
            () =>
            {
                RemovePlacementFlight(card);
                card.NotifyShelfPlacement(isCorrect: true);
                GameSoundEffects.Play(GameSoundEffects.Id.CardShelfPlace);
            });
    }

    void UpdatePlacementFlights()
    {
        for (int i = 0; i < _placementFlights.Count; i++)
        {
            PlacementFlightEntry flight = _placementFlights[i];
            WorldCard card = flight.Card;
            if (card == null || !card.IsFlyingToShelf)
                continue;

            GetPlacementFlightPose(out Vector3 targetPos, out Quaternion targetRot);
            card.UpdateShelfFlight(targetPos, targetRot);
        }

        for (int i = _placementFlights.Count - 1; i >= 0; i--)
        {
            if (_placementFlights[i].Card == null || !_placementFlights[i].Card.IsFlyingToShelf)
                _placementFlights.RemoveAt(i);
        }
    }

    void RemovePlacementFlight(WorldCard card)
    {
        for (int i = _placementFlights.Count - 1; i >= 0; i--)
        {
            if (_placementFlights[i].Card == card)
                _placementFlights.RemoveAt(i);
        }
    }

    void EnsureHolderOutline()
    {
        ResolveReferences();
        if (holderOutlineTarget == null || _holderOutline != null)
            return;

        _holderOutline = holderOutlineTarget.GetComponent<Outline>();
        if (_holderOutline == null)
            _holderOutline = holderOutlineTarget.gameObject.AddComponent<Outline>();

        _holderOutline.OutlineMode = Outline.Mode.OutlineAll;
        _holderOutline.OutlineWidth = PackVisualSettings.GetQuickOutlineWidthOrDefault();
        _holderOutline.enabled = false;
    }

    void SetHolderOutlineActive(bool active)
    {
        if (_holderOutlineActive == active)
            return;

        _holderOutlineActive = active;
        if (!active)
        {
            if (_holderOutline != null)
                _holderOutline.enabled = false;
            return;
        }

        EnsureHolderOutline();
        if (_holderOutline == null)
            return;

        CardOutlineSettings.Palette palette = CardOutlineSettings.GetPaletteOrDefaults();
        _holderOutline.OutlineColor = palette.cardHover;
        _holderOutline.enabled = true;
    }

    void OnEnable()
    {
        EnsureSlotMarkerExists();
        if (Application.isPlaying)
            EnsureAimCollider();
        EnsureLabelExists();
        RefreshLabel();
        EnsureEditorPreview();
#if UNITY_EDITOR
        if (!Application.isPlaying && !IsEditingPrefabAsset())
            RefreshEditorPreviewVisibility();
#endif
    }

    void Start()
    {
        EnsureLabelExists();
        RefreshLabel();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        slotNumber = PsaArtLibrary.ClampCabinetSlotNumber(slotNumber);
        defaultVariantIndex = Mathf.Max(1, defaultVariantIndex);
        if (IsEditingPrefabAsset())
            return;
        SchedulePreviewRefresh();
    }

    void SchedulePreviewRefresh()
    {
        if (Application.isPlaying || _previewDeferred)
            return;

        _previewDeferred = true;
        EditorApplication.delayCall += ApplyDeferredPreview;
    }

    void ApplyDeferredPreview()
    {
        _previewDeferred = false;
        if (this == null)
            return;

        if (IsEditingPrefabAsset())
            return;

        EnsureSlotMarkerExists();
        EnsureEditorPreview();
        RefreshEditorPreviewVisibility();
    }

    bool IsEditingPrefabAsset() => PrefabUtility.IsPartOfPrefabAsset(gameObject);
#endif

    public void EnsureSlotMarkerExists()
    {
        ResolveReferences();
        if (slotMarker != null)
            return;

        Transform existing = transform.Find(SlotMarkerName);
        if (existing != null)
        {
            slotMarker = existing;
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            var markerGo = new GameObject(SlotMarkerName);
            Undo.RegisterCreatedObjectUndo(markerGo, "Create PSA Slot Marker");
            markerGo.transform.SetParent(transform, false);
            if (labelAnchor != null)
            {
                markerGo.transform.localPosition = labelAnchor.localPosition;
                markerGo.transform.localRotation = labelAnchor.localRotation;
            }
            slotMarker = markerGo.transform;
            EditorUtility.SetDirty(this);
            return;
        }
#endif

        var runtimeMarker = new GameObject(SlotMarkerName);
        runtimeMarker.transform.SetParent(transform, false);
        slotMarker = runtimeMarker.transform;
    }

#if UNITY_EDITOR
    public void TryCreateSlotMarker()
    {
        Undo.SetCurrentGroupName("Create PSA Slot Marker");
        int undoGroup = Undo.GetCurrentGroup();

        Transform existing = transform.Find(SlotMarkerName);
        if (existing != null)
        {
            // Marker placement is hand tuned per holder, so refreshing must never reset it.
            slotMarker = existing;
            EnsureEditorPreview();
            RefreshEditorPreviewVisibility();
            Undo.CollapseUndoOperations(undoGroup);
            EditorUtility.SetDirty(this);
            return;
        }

        var markerGo = new GameObject(SlotMarkerName);
        Undo.RegisterCreatedObjectUndo(markerGo, "Create PSA Slot Marker");
        markerGo.transform.SetParent(transform, false);
        if (labelAnchor != null)
        {
            markerGo.transform.localPosition = labelAnchor.localPosition;
            markerGo.transform.localRotation = labelAnchor.localRotation;
        }

        slotMarker = markerGo.transform;
        EnsureEditorPreview();
        RefreshEditorPreviewVisibility();
        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.SetDirty(this);
    }
#endif

    void RefreshEditorPreviewVisibility()
    {
        bool show = !Application.isPlaying && IsEmpty;
        SetEditorPreviewActive(show);
    }

    void SetEditorPreviewActive(bool active)
    {
        EnsureEditorPreview();
        if (_previewRoot == null)
            return;

        if (Application.isPlaying)
        {
            HideEditorPreviewForPlayMode();
            return;
        }

#if UNITY_EDITOR
        if (IsEditingPrefabAsset())
            return;
#endif

        SetEditorPreviewRenderersEnabled(active);
    }

    void HideEditorPreviewForPlayMode() => SetEditorPreviewRenderersEnabled(false);

    void SetEditorPreviewRenderersEnabled(bool enabled)
    {
#if UNITY_EDITOR
        if (IsEditingPrefabAsset())
            return;
#endif

        if (_previewFillRenderer != null)
            _previewFillRenderer.enabled = enabled;
        if (_previewEdgeRenderer != null)
            _previewEdgeRenderer.enabled = enabled;
    }

    void EnsureEditorPreview()
    {
        EnsureSlotMarkerExists();
        Transform marker = GetSlotMarker();
        if (marker == null)
            return;

        if (_previewRoot == null)
        {
            Transform existing = marker.Find(PreviewObjectName);
            if (existing != null)
                _previewRoot = existing;
        }

        if (_previewRoot == null)
        {
            var go = new GameObject(PreviewObjectName);
            go.transform.SetParent(marker, false);
#if UNITY_EDITOR
            if (!IsEditingPrefabAsset())
                go.hideFlags = HideFlags.DontSave;
#endif
            _previewRoot = go.transform;
        }
        else if (!_previewRoot.gameObject.activeSelf)
        {
#if UNITY_EDITOR
            if (!IsEditingPrefabAsset())
                _previewRoot.gameObject.SetActive(true);
#else
            _previewRoot.gameObject.SetActive(true);
#endif
        }

        _previewRoot.localPosition = Vector3.zero;
        _previewRoot.localRotation = Quaternion.identity;
        _previewRoot.localScale = Vector3.one;

        EnsureSharedPreviewAssets();

        if (_previewFillRenderer == null)
            _previewFillRenderer = EnsureChildRenderer(_previewRoot, "Fill", _sharedCardPlaneMesh, _sharedFillMaterial);
        if (_previewEdgeRenderer == null)
            _previewEdgeRenderer = EnsureChildRenderer(
                _previewRoot,
                "Edge",
                CardVisualResources.InteractionBorderFrameMesh,
                _sharedEdgeMaterial);

        Quaternion previewRotation = Quaternion.Euler(placedCardLocalEuler);
        GetSlabLocalBox(
            out Vector3 boxCenter,
            out Vector3 faceBottomCenter,
            out Vector3 slabSize,
            out Vector3 slabScale);

        Transform fillTransform = _previewFillRenderer.transform;
        fillTransform.localPosition = faceBottomCenter;
        fillTransform.localRotation = previewRotation;
        fillTransform.localScale = slabScale;

        Transform edgeTransform = _previewEdgeRenderer.transform;
        edgeTransform.localPosition = boxCenter;
        edgeTransform.localRotation = previewRotation;
        edgeTransform.localScale = slabSize;
    }

    static MeshRenderer EnsureChildRenderer(Transform parent, string childName, Mesh mesh, Material material)
    {
        Transform child = parent.Find(childName);
        GameObject go = child != null ? child.gameObject : new GameObject(childName);
        if (child == null)
            go.transform.SetParent(parent, false);

        var filter = go.GetComponent<MeshFilter>();
        if (filter == null)
            filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    static void EnsureSharedPreviewAssets()
    {
        CardArtLibrary.EnsureLoaded();

        if (_sharedCardPlaneMesh == null)
            _sharedCardPlaneMesh = BuildPsaSlabPreviewPlaneMesh();

        if (_sharedFillMaterial == null)
        {
            _sharedFillMaterial = RuntimeMaterialUtility.CreateUnlitMaterial(
                PreviewFillColor,
                enableInstancing: false,
                renderQueue: (int)RenderQueue.Transparent);
            SetPreviewMaterialTransparent(_sharedFillMaterial, PreviewFillColor);
        }

        if (_sharedEdgeMaterial == null)
        {
            _sharedEdgeMaterial = RuntimeMaterialUtility.CreateUnlitMaterial(
                PreviewEdgeColor,
                enableInstancing: false,
                renderQueue: (int)RenderQueue.Transparent + 1);
            SetPreviewMaterialTransparent(_sharedEdgeMaterial, PreviewEdgeColor);
        }
    }

    static void SetPreviewMaterialTransparent(Material material, Color color)
    {
        if (material == null)
            return;

        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    static float PreviewFaceWidth()
    {
        float width = PsaSlabLayoutUtility.GetCabinetFaceWidth();
        return width > 0.001f ? width : 0.126f;
    }

    static float PreviewFaceHeight()
    {
        float height = PsaSlabLayoutUtility.GetCabinetFaceHeight();
        return height > 0.001f ? height : 0.176f;
    }

    static Mesh BuildPsaSlabPreviewPlaneMesh()
    {
        float halfW = PreviewFaceWidth() * 0.5f;
        float height = PreviewFaceHeight();
        const float z = 0.001f;

        var mesh = new Mesh { name = "PsaSlabPreviewPlane" };
        mesh.vertices = new[]
        {
            new Vector3(-halfW, 0f, z),
            new Vector3(halfW, 0f, z),
            new Vector3(halfW, height, z),
            new Vector3(-halfW, height, z),
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public void RefreshLabel()
    {
        CacheLabelReferences();

        if (!showSlotLabel)
        {
            if (_labelCanvasRect != null)
                _labelCanvasRect.gameObject.SetActive(false);
            return;
        }

        if (_labelText == null)
            return;

        if (_labelCanvasRect != null)
            _labelCanvasRect.gameObject.SetActive(true);

        ApplyLabelTransform();
        ApplyLabelVisuals();
    }

    public void EnsureLabelExists()
    {
        CacheLabelReferences();
        if (_labelText != null)
            return;

        CreateLabelHierarchy();
        CacheLabelReferences();
    }

    void ApplyLabelVisuals()
    {
        if (_labelText == null)
            return;

        _labelText.text = slotNumber.ToString();
        _labelText.fontSize = labelFontSize;
        _labelText.color = labelColor;
    }

    void CacheLabelReferences()
    {
        _labelCanvasRect = null;
        _labelText = null;

        Transform anchor = labelAnchor != null ? labelAnchor : transform;
        Transform existing = anchor.Find(LabelObjectName);
        if (existing == null)
            return;

        _labelCanvasRect = existing as RectTransform;
        if (_labelCanvasRect == null)
            _labelCanvasRect = existing.GetComponent<RectTransform>();

        Transform textTransform = existing.Find(LabelTextObjectName);
        if (textTransform != null)
            _labelText = textTransform.GetComponent<Text>();
    }

    void ApplyLabelTransform()
    {
        if (_labelCanvasRect == null)
            return;

        if (labelAnchor == null)
        {
            _labelCanvasRect.localPosition = labelLocalOffset;
            _labelCanvasRect.localRotation = Quaternion.Euler(labelLocalEuler);
        }
        else
        {
            _labelCanvasRect.localPosition = Vector3.zero;
            _labelCanvasRect.localRotation = Quaternion.Euler(labelLocalEuler);
        }

        _labelCanvasRect.localScale = Vector3.one * labelCanvasScale;
    }

    void CreateLabelHierarchy()
    {
        Transform anchor = labelAnchor != null ? labelAnchor : transform;
        RemoveExistingLabel(anchor);

        var canvasGo = new GameObject(LabelObjectName, typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(anchor, false);

        _labelCanvasRect = canvasGo.GetComponent<RectTransform>();
        _labelCanvasRect.sizeDelta = new Vector2(256f, 256f);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = null;

        var textGo = new GameObject(LabelTextObjectName, typeof(RectTransform));
        textGo.transform.SetParent(canvasGo.transform, false);

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        _labelText = textGo.AddComponent<Text>();
        _labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _labelText.fontSize = labelFontSize;
        _labelText.fontStyle = FontStyle.Bold;
        _labelText.alignment = TextAnchor.MiddleCenter;
        _labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _labelText.verticalOverflow = VerticalWrapMode.Overflow;
        _labelText.raycastTarget = false;
        _labelText.color = labelColor;
        _labelText.text = slotNumber.ToString();

        ApplyLabelTransform();
    }

    void RemoveExistingLabel(Transform anchor)
    {
        Transform existing = anchor.Find(LabelObjectName);
        if (existing == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(existing.gameObject);
        else
#endif
            Destroy(existing.gameObject);
    }

#if UNITY_EDITOR
    public void TryCreateLabelObject()
    {
        UnityEditor.Undo.SetCurrentGroupName("Create PSA Slot Label");
        int undoGroup = UnityEditor.Undo.GetCurrentGroup();

        Transform anchor = labelAnchor != null ? labelAnchor : transform;
        Transform existing = anchor.Find(LabelObjectName);
        if (existing != null)
            UnityEditor.Undo.DestroyObjectImmediate(existing.gameObject);

        CreateLabelHierarchy();
        UnityEditor.Undo.RegisterCreatedObjectUndo(_labelCanvasRect.gameObject, "Create PSA Slot Label");
        UnityEditor.Undo.CollapseUndoOperations(undoGroup);

        RefreshLabel();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    void OnDrawGizmosSelected()
    {
        Transform marker = GetSlotMarker();
        GetSlabLocalBox(out Vector3 boxCenter, out _, out Vector3 slabSize, out _);

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.85f);
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = marker.localToWorldMatrix
            * Matrix4x4.TRS(boxCenter, Quaternion.Euler(placedCardLocalEuler), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, slabSize);
        Gizmos.matrix = previous;

#if UNITY_EDITOR
        Handles.color = Color.white;
        Handles.Label(marker.position + marker.up * 0.04f, $"PSA Slot {slotNumber}");
#endif
    }
}
