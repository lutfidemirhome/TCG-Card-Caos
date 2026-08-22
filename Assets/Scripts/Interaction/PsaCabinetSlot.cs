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
    const string DefaultTableVisualName = "Counter_attlsv";
    const string LegacyTableVisualName = "Table_Visual";
    const string HolderToonTemplatePath = "Assets/Art/Materials/CabinetBody.mat";
    const string TableToonTemplatePath = "Assets/Art/Materials/PsaCabinetTable.mat";
    const string TableToonFallbackPath = "Assets/Art/Materials/CabinetShelf.mat";

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

    [Header("Holder color")]
    [Tooltip("Tints the holder visuals in the Scene and while playing.")]
    [SerializeField] bool overrideHolderColor;
    [SerializeField] Color holderColor = Color.white;

    [Header("Table color")]
    [Tooltip("Table mesh under the holder. Auto-fills when empty.")]
    [SerializeField] Transform tableVisual;
    [Tooltip("Tints the assigned table visual in the Scene and while playing.")]
    [SerializeField] bool overrideTableColor;
    [SerializeField] Color tableColor = Color.white;

    [Header("Cabinet toon")]
    [Tooltip("Editor only. Off by default. When off, materials you assign on renderers in the Scene are kept unchanged in Play mode.")]
    [SerializeField] bool applyCabinetToon;
    [SerializeField] Material holderToonTemplate;
    [SerializeField] Material tableToonTemplate;

    [Header("Slot number label")]
    [SerializeField] bool showSlotLabel = true;
    [Tooltip("Optional anchor on the holder face. When empty, labelLocalOffset is used.")]
    [SerializeField] Transform labelAnchor;
    [SerializeField] Vector3 labelLocalOffset = new Vector3(0f, 0.05f, 0.015f);
    [SerializeField] Vector3 labelLocalEuler = new Vector3(0f, 180f, 0f);
    [SerializeField] float labelCanvasScale = 0.00035f;
    [SerializeField] int labelFontSize = 120;
    [Tooltip("Slot number text color. Applied to SlotNumberLabel/Text in the prefab and in Play mode.")]
    [SerializeField] Color labelColor = new Color(0.12f, 0.12f, 0.12f, 1f);

    RectTransform _labelCanvasRect;
    Text _labelText;
    Canvas _labelCanvas;
    CanvasRenderer[] _labelCanvasRenderers;
    bool _labelRefsResolved;
    bool _labelFound;
    bool _labelCanvasConfigured;
    bool _labelVisible = true;
    Transform _previewRoot;
    MeshRenderer _previewFillRenderer;
    MeshRenderer _previewEdgeRenderer;
    bool _previewDeferred;
    Outline _holderOutline;
    HolderOutlineMode _holderOutlineMode = HolderOutlineMode.Off;
    Coroutine _holderPlacementFlashRoutine;

    const int HolderPlacementFlashPulses = 2;
    const float HolderPlacementFlashOnSeconds = 0.12f;
    const float HolderPlacementFlashOffSeconds = 0.1f;

    enum HolderOutlineMode
    {
        Off,
        Hover,
        Correct,
        Incorrect,
    }
    BoxCollider _aimCollider;

    struct HolderTintEntry
    {
        public MeshRenderer Renderer;
        public int MaterialIndex;
        public bool HasBaseColor;
        public bool HasColor;
        public bool IsTable;
    }

    System.Collections.Generic.List<HolderTintEntry> _holderTintEntries;
    bool _holderTintStateValid;
    bool _appliedOverrideHolderColor;
    Color _appliedHolderColor;
    bool _appliedOverrideTableColor;
    Color _appliedTableColor;
    static MaterialPropertyBlock _sharedPropertyBlock;

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
        {
            HideEditorPreviewForPlayMode();
            ClearHolderPropertyBlocks();
            ApplyShadowFlagsOnly();
            return;
        }

        ApplyShadowFlagsOnly();
        ApplyEditorMaterialOverrides();
    }

    void Start()
    {
        EnsureLabelExists();
        RefreshLabel();
    }

    void OnDestroy()
    {
        ClearPlacementFeedback();
    }

    void OnDisable()
    {
        SetEditorPreviewActive(false);
        ClearPlacementFeedback();
    }

    void LateUpdate()
    {
        UpdatePlacementFlights();

        if (Application.isPlaying)
        {
            HideEditorPreviewForPlayMode();
            RefreshLabelCanvasForPlayView();
            return;
        }

#if UNITY_EDITOR
        RefreshLabelCanvasForEditorView();

        if (IsEditingPrefabAsset())
            return;
#endif

        EnsureEditorPreview();
        RefreshEditorPreviewVisibility();
    }

    /// <summary>
    /// Safety net for occupancy changes that did not route through <see cref="RefreshLabel"/>.
    /// Steady state costs one bool compare, so it stays allocation free.
    /// </summary>
    void RefreshLabelCanvasForPlayView()
    {
        if (!showSlotLabel)
            return;

        CacheLabelReferences();

        // A partially authored label would make RefreshLabel bail out before updating state,
        // which would otherwise retrigger it every frame.
        if (!_labelFound)
            return;

        bool visible = ShouldShowSlotLabel();
        if (visible != _labelVisible || _labelCanvasRect.gameObject.activeSelf != visible)
            RefreshLabel();
    }

#if UNITY_EDITOR
    void RefreshLabelCanvasForEditorView()
    {
        RefreshLabelCanvasForPlayView();

        // The Scene view camera can change between repaints, so keep re-applying it in the editor.
        if (_labelCanvasRect != null && _labelCanvasRect.gameObject.activeSelf)
            ConfigureLabelCanvas();
    }
#endif

    void ResolveReferences()
    {
        if (slotMarker == null)
            slotMarker = transform.Find(SlotMarkerName);

        if (placementParent == null)
            placementParent = transform;

        if (holderOutlineTarget == null)
            holderOutlineTarget = transform.Find(DefaultHolderOutlineTargetName);

        if (tableVisual == null)
        {
            tableVisual = transform.Find(DefaultTableVisualName);
            if (tableVisual == null)
                tableVisual = transform.Find(LegacyTableVisualName);
        }
    }

    void InvalidateHolderTintCache()
    {
        _holderTintEntries = null;
        _holderTintStateValid = false;
    }

    void ClearHolderPropertyBlocks()
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer meshRenderer = renderers[i];
            if (meshRenderer == null || ShouldSkipHolderShadowDisable(meshRenderer))
                continue;

            int materialCount = meshRenderer.sharedMaterials.Length;
            for (int m = 0; m < materialCount; m++)
                meshRenderer.SetPropertyBlock(null, m);
        }

        InvalidateHolderTintCache();
    }

    void ApplyShadowFlagsOnly()
    {
        ResolveReferences();
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer meshRenderer = renderers[i];
            if (meshRenderer == null || ShouldSkipHolderShadowDisable(meshRenderer))
                continue;

            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
#if UNITY_EDITOR
            meshRenderer.receiveGI = ReceiveGI.LightProbes;
#endif
        }
    }

    void ApplyEditorMaterialOverrides()
    {
        if (Application.isPlaying)
            return;

        if (applyCabinetToon)
            ApplyCabinetToonToAll();

        RefreshHolderColor();
    }

    void ApplyCabinetToonToAll()
    {
        if (Application.isPlaying)
            return;

        ResolveReferences();
        Material holderTemplate = ResolveHolderToonTemplate();
        Material tableTemplate = ResolveTableToonTemplate();
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer meshRenderer = renderers[i];
            if (meshRenderer == null || ShouldSkipHolderShadowDisable(meshRenderer))
                continue;

            bool isTable = IsTableRenderer(meshRenderer.transform);
            Material template = isTable ? tableTemplate : holderTemplate;
            ApplyCabinetToon(meshRenderer, template, isTable);
        }
    }

    static void ApplyCabinetToon(MeshRenderer meshRenderer, Material template, bool isTable)
    {
        if (meshRenderer == null || template == null)
            return;

        if (isTable)
        {
            meshRenderer.sharedMaterial = template;
            return;
        }

        Material current = meshRenderer.sharedMaterial;
        if (current != null && current.shader == template.shader)
            return;

        meshRenderer.sharedMaterial = template;
    }

    Material ResolveHolderToonTemplate()
    {
        if (holderToonTemplate != null)
            return holderToonTemplate;

#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(HolderToonTemplatePath);
#else
        return null;
#endif
    }

    Material ResolveTableToonTemplate()
    {
        if (tableToonTemplate != null)
            return tableToonTemplate;

#if UNITY_EDITOR
        Material tableMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(TableToonTemplatePath);
        if (tableMaterial != null)
            return tableMaterial;

        return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(TableToonFallbackPath);
#else
        return null;
#endif
    }

    /// <summary>
    /// Tints holder visuals. Pass <c>null</c> to restore the model colors.
    /// </summary>
    public void SetHolderColor(Color? color)
    {
        overrideHolderColor = color.HasValue;
        if (color.HasValue)
            holderColor = color.Value;

        InvalidateHolderTintCache();
        RefreshHolderColor();
    }

    /// <summary>
    /// Tints the table visual. Pass <c>null</c> to restore the model colors.
    /// </summary>
    public void SetTableColor(Color? color)
    {
        overrideTableColor = color.HasValue;
        if (color.HasValue)
            tableColor = color.Value;

        InvalidateHolderTintCache();
        RefreshHolderColor();
    }

    void RefreshHolderColor()
    {
        if (Application.isPlaying)
            return;

        if (!overrideHolderColor && !overrideTableColor)
            return;

        ResolveReferences();

        if (_holderTintStateValid
            && _appliedOverrideHolderColor == overrideHolderColor
            && _appliedOverrideTableColor == overrideTableColor
            && _appliedHolderColor == holderColor
            && _appliedTableColor == tableColor)
        {
            return;
        }

        EnsureHolderTintEntries();
        if (_holderTintEntries == null)
            return;

        MaterialPropertyBlock block = SharedPropertyBlock;
        for (int i = 0; i < _holderTintEntries.Count; i++)
        {
            HolderTintEntry entry = _holderTintEntries[i];
            if (entry.Renderer == null)
                continue;

            bool tint = entry.IsTable ? overrideTableColor : overrideHolderColor;
            if (!tint)
            {
                entry.Renderer.SetPropertyBlock(null, entry.MaterialIndex);
                continue;
            }

            Color color = entry.IsTable ? tableColor : holderColor;
            block.Clear();
            if (entry.HasBaseColor)
                block.SetColor("_BaseColor", color);
            if (entry.HasColor)
                block.SetColor("_Color", color);

            entry.Renderer.SetPropertyBlock(block, entry.MaterialIndex);
        }

        _appliedOverrideHolderColor = overrideHolderColor;
        _appliedOverrideTableColor = overrideTableColor;
        _appliedHolderColor = holderColor;
        _appliedTableColor = tableColor;
        _holderTintStateValid = true;
    }

    void EnsureHolderTintEntries()
    {
        if (_holderTintEntries != null)
            return;

        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length == 0)
            return;

        _holderTintEntries = new System.Collections.Generic.List<HolderTintEntry>(renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer meshRenderer = renderers[i];
            if (meshRenderer == null || ShouldSkipHolderShadowDisable(meshRenderer))
                continue;

            bool isTable = IsTableRenderer(meshRenderer.transform);
            Material[] materials = meshRenderer.sharedMaterials;
            for (int m = 0; m < materials.Length; m++)
            {
                Material material = materials[m];
                if (material == null)
                    continue;

                bool hasBaseColor = material.HasProperty("_BaseColor");
                bool hasColor = material.HasProperty("_Color");
                if (!hasBaseColor && !hasColor)
                    continue;

                _holderTintEntries.Add(new HolderTintEntry
                {
                    Renderer = meshRenderer,
                    MaterialIndex = m,
                    HasBaseColor = hasBaseColor,
                    HasColor = hasColor,
                    IsTable = isTable,
                });
            }
        }
    }

    bool IsTableRenderer(Transform target)
    {
        if (tableVisual == null)
            return false;

        return target == tableVisual || target.IsChildOf(tableVisual);
    }

    static MaterialPropertyBlock SharedPropertyBlock =>
        _sharedPropertyBlock ??= new MaterialPropertyBlock();

    static void ClearMaterialOcclusion(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_OcclusionMap"))
            material.SetTexture("_OcclusionMap", null);

        if (material.HasProperty("_OcclusionStrength"))
            material.SetFloat("_OcclusionStrength", 0f);
    }

    static bool ShouldSkipHolderShadowDisable(Renderer renderer)
    {
        Transform transform = renderer.transform;
        if (transform.GetComponentInParent<Canvas>() != null)
            return true;

        string objectName = transform.name;
        if (objectName == PreviewObjectName
            || objectName == AimColliderName
            || objectName == LabelObjectName
            || objectName == LabelTextObjectName)
        {
            return true;
        }

        Transform marker = transform;
        while (marker != null)
        {
            if (marker.name == SlotMarkerName)
                return true;
            marker = marker.parent;
        }

        return false;
    }

    static void ApplyNoShadowMaterialSettings(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_ReceiveShadows"))
            material.SetFloat("_ReceiveShadows", 0f);

        material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
        material.SetShaderPassEnabled("ShadowCaster", false);
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
        CardGroundQuery.UntrackShelfCard(card);
        RefreshEditorPreviewVisibility();
        RefreshLabel();
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
        // Sorting order is derived from the slot number, so the canvas has to be re-applied.
        _labelCanvasConfigured = false;
        RefreshLabel();
    }

    public bool RestoreOccupiedCard(WorldCard card)
    {
        if (card == null)
            return false;

        Occupy(card);
        BuildPlacementLocalPose(
            out Transform parent,
            out Vector3 localPosition,
            out Quaternion localRotation,
            out Vector3 localScale,
            out _,
            out _);
        card.PlaceOnPsaCabinetSlot(parent, localPosition, localRotation, localScale);
        card.NotifyShelfPlacement(IsCorrectPlacement(card));
        return true;
    }

    public void Occupy(WorldCard card)
    {
        occupiedCard = card;
        RefreshEditorPreviewVisibility();
        RefreshLabel();
    }

    public void ClearOccupant()
    {
        occupiedCard = null;
        RefreshEditorPreviewVisibility();
        RefreshLabel();
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
        && PsaArtLibrary.IsCabinetSlotNumber(card.PsaSlotNumber);

    public bool IsCorrectPlacement(WorldCard card) =>
        AcceptsPsaCard(card) && card.PsaSlotNumber == SlotNumber;

    public bool CanPlaceHeldCard(WorldCard card) =>
        AcceptsPsaCard(card) && IsEmpty;

    public void RefreshOccupancy()
    {
        if (occupiedCard != null && occupiedCard.IsInHand)
            occupiedCard = null;
        RefreshEditorPreviewVisibility();
        RefreshLabel();
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
                card.NotifyShelfPlacement(IsCorrectPlacement(card));
                GameSoundEffects.Play(GameSoundEffects.Id.CardShelfPlace);
                GameSaveSignals.MarkDirty();
                PsaCabinet cabinet = GetComponentInParent<PsaCabinet>();
                if (cabinet != null && cabinet.IsComplete())
                    GameSaveSignals.NotifyMilestone();
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

    /// <summary>Green/red placement pulse on Tutucu2_Visual instead of the placed card mesh.</summary>
    public void NotifyPlacementFeedback(bool isCorrect)
    {
        if (_holderPlacementFlashRoutine != null)
        {
            StopCoroutine(_holderPlacementFlashRoutine);
            _holderPlacementFlashRoutine = null;
        }

        _holderPlacementFlashRoutine = StartCoroutine(HolderPlacementFlashRoutine(isCorrect));
    }

    public void ClearPlacementFeedback()
    {
        if (_holderPlacementFlashRoutine != null)
        {
            StopCoroutine(_holderPlacementFlashRoutine);
            _holderPlacementFlashRoutine = null;
        }

        SetHolderOutlineMode(HolderOutlineMode.Off);
    }

    /// <summary>Yellow hover while the crosshair is on a card seated in this holder.</summary>
    public void SetOccupiedCardAimOutline(bool active)
    {
        if (_holderPlacementFlashRoutine != null)
            return;

        SetHolderOutlineMode(active ? HolderOutlineMode.Hover : HolderOutlineMode.Off);
    }

    System.Collections.IEnumerator HolderPlacementFlashRoutine(bool isCorrect)
    {
        HolderOutlineMode flashMode = isCorrect
            ? HolderOutlineMode.Correct
            : HolderOutlineMode.Incorrect;

        for (int pulse = 0; pulse < HolderPlacementFlashPulses; pulse++)
        {
            SetHolderOutlineMode(flashMode);
            yield return new WaitForSeconds(HolderPlacementFlashOnSeconds);
            SetHolderOutlineMode(HolderOutlineMode.Off);
            yield return new WaitForSeconds(HolderPlacementFlashOffSeconds);
        }

        _holderPlacementFlashRoutine = null;
        SetHolderOutlineMode(HolderOutlineMode.Off);
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

    void SetHolderOutlineMode(HolderOutlineMode mode)
    {
        if (_holderOutlineMode == mode)
            return;

        _holderOutlineMode = mode;
        if (mode == HolderOutlineMode.Off)
        {
            if (_holderOutline != null)
                _holderOutline.enabled = false;
            return;
        }

        EnsureHolderOutline();
        if (_holderOutline == null)
            return;

        CardOutlineSettings.Palette palette = CardOutlineSettings.GetPaletteOrDefaults();
        _holderOutline.OutlineColor = mode switch
        {
            HolderOutlineMode.Correct => palette.shelfCorrect,
            HolderOutlineMode.Incorrect => palette.shelfIncorrect,
            _ => palette.cardHover,
        };
        _holderOutline.enabled = true;
    }

    void SetHolderOutlineActive(bool active) =>
        SetHolderOutlineMode(active ? HolderOutlineMode.Hover : HolderOutlineMode.Off);

    void OnEnable()
    {
        InvalidateLabelCache();
        EnsureSlotMarkerExists();
        if (Application.isPlaying)
            EnsureAimCollider();
        else
            ApplyEditorMaterialOverrides();

        EnsureLabelExists();
        RefreshLabel();
        EnsureEditorPreview();
#if UNITY_EDITOR
        if (!Application.isPlaying && !IsEditingPrefabAsset())
            RefreshEditorPreviewVisibility();
#endif
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying)
            return;

        slotNumber = PsaArtLibrary.ClampCabinetSlotNumber(slotNumber);
        defaultVariantIndex = Mathf.Max(1, defaultVariantIndex);
        ApplyShadowFlagsOnly();
        ApplyEditorMaterialOverrides();
        // labelAnchor or the label hierarchy may have been re-authored in the inspector.
        InvalidateLabelCache();
        RefreshLabel();
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
            _labelVisible = false;
            if (_labelCanvasRect != null)
                _labelCanvasRect.gameObject.SetActive(false);
            return;
        }

        if (_labelText == null)
            return;

        bool visible = ShouldShowSlotLabel();
        _labelVisible = visible;
        if (_labelCanvasRect != null)
            _labelCanvasRect.gameObject.SetActive(visible);

        if (!visible)
            return;

        ConfigureLabelCanvas();

        // When labelAnchor is assigned in the prefab/scene, keep its authored transform.
        if (labelAnchor == null)
            ApplyLabelTransform();

        ApplyLabelVisuals();
    }

    bool ShouldShowSlotLabel()
    {
        if (occupiedCard == null)
            return true;

        return occupiedCard.IsInHand;
    }

    public void EnsureLabelExists()
    {
        CacheLabelReferences();
        if (_labelText != null)
            return;

        // Re-check against the live hierarchy before building a second label.
        ResolveLabelReferences();
        if (_labelText != null)
            return;

        CreateLabelHierarchy();
    }

    /// <summary>
    /// Canvas setup is static for the life of the label, so it is applied once per cache refresh.
    /// Only the camera hookup keeps polling: in play until <see cref="Camera.main"/> exists, and in
    /// the editor because the active Scene view can change.
    /// </summary>
    void ConfigureLabelCanvas()
    {
        if (_labelCanvas == null)
            return;

        if (!_labelCanvasConfigured)
        {
            _labelCanvas.renderMode = RenderMode.WorldSpace;
            _labelCanvas.enabled = true;
            _labelCanvas.overrideSorting = true;
            _labelCanvas.sortingOrder = slotNumber;

            if (_labelCanvasRenderers != null)
            {
                for (int i = 0; i < _labelCanvasRenderers.Length; i++)
                {
                    if (_labelCanvasRenderers[i] != null)
                        _labelCanvasRenderers[i].cullTransparentMesh = false;
                }
            }

            _labelCanvasConfigured = true;
        }

        if (Application.isPlaying)
        {
            if (_labelCanvas.worldCamera == null)
                _labelCanvas.worldCamera = Camera.main;

            return;
        }

#if UNITY_EDITOR
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null && sceneView.camera != null)
            _labelCanvas.worldCamera = sceneView.camera;
#endif
    }

    void ApplyLabelVisuals()
    {
        if (_labelText == null)
            return;

        _labelText.text = slotNumber.ToString();
        _labelText.color = labelColor;

        if (Application.isPlaying)
            return;

        _labelText.fontSize = labelFontSize;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Copies the authored Text color into <see cref="labelColor"/> so prefab edits on the child
    /// Text component can be pulled onto the holder in one step.
    /// </summary>
    public void AdoptLabelColorFromText()
    {
        CacheLabelReferences();
        if (_labelText == null)
            return;

        labelColor = _labelText.color;
        RefreshLabel();
    }
#endif

    /// <summary>
    /// The label hierarchy never changes while playing, so the lookup runs once and is only
    /// repeated when the cache is invalidated or the cached objects were destroyed.
    /// </summary>
    void CacheLabelReferences()
    {
        if (_labelRefsResolved && (!_labelFound || (_labelCanvasRect != null && _labelText != null)))
            return;

        ResolveLabelReferences();
    }

    void ResolveLabelReferences()
    {
        _labelCanvasRect = null;
        _labelText = null;
        _labelCanvas = null;
        _labelCanvasRenderers = null;
        _labelCanvasConfigured = false;
        _labelRefsResolved = true;
        _labelFound = false;

        Transform anchor = labelAnchor != null ? labelAnchor : transform;
        Transform existing = anchor.Find(LabelObjectName);
        if (existing == null)
            return;

        _labelCanvasRect = existing as RectTransform;
        if (_labelCanvasRect == null)
            _labelCanvasRect = existing.GetComponent<RectTransform>();

        _labelCanvas = existing.GetComponent<Canvas>();
        _labelCanvasRenderers = existing.GetComponentsInChildren<CanvasRenderer>(true);

        Transform textTransform = existing.Find(LabelTextObjectName);
        if (textTransform != null)
            _labelText = textTransform.GetComponent<Text>();

        _labelFound = _labelCanvasRect != null && _labelText != null;
    }

    void InvalidateLabelCache()
    {
        _labelRefsResolved = false;
        _labelFound = false;
        _labelCanvasConfigured = false;
        _labelCanvasRect = null;
        _labelText = null;
        _labelCanvas = null;
        _labelCanvasRenderers = null;
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

        _labelCanvas = canvas;
        _labelCanvasRenderers = canvasGo.GetComponentsInChildren<CanvasRenderer>(true);
        _labelRefsResolved = true;
        _labelFound = true;
        _labelCanvasConfigured = false;
    }

    void RemoveExistingLabel(Transform anchor)
    {
        Transform existing = anchor.Find(LabelObjectName);
        if (existing == null)
            return;

        InvalidateLabelCache();

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
