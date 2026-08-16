using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pickupable booster pack on the ground or held in the player's hand.
/// Physics/orientation follow an invisible <see cref="PackCardRef"/> child that mirrors
/// <see cref="WorldCard"/>; the imported 3D pack mesh is cosmetic only.
/// </summary>
public class WorldBoosterPack : MonoBehaviour, IInteractable, IInteractionHighlight
{
    public enum PackState
    {
        World,
        FlyingToHand,
        Held,
        Opening,
    }

    [SerializeField] BoosterPackDefinition packDefinition;
    [Tooltip("Optional imported pack model prefab. Parented on PackCardRef (invisible card proxy).")]
    [SerializeField] GameObject visualPrefab;

    const string CardRefChildName = "PackCardRef";
    const string PackModelChildName = "PackVisual";
    const string DefaultPackModelResourcePath = "Cards/BoosterPack/TradingCard_BoosterPack";
    static GameObject _defaultPackModelPrefab;
    const float PackWidthFitMultiplier = 1.06f;

    PackState _state = PackState.World;
    Transform _cardRef;
    Transform _packModel;
    Transform _handAnchor;
    Rigidbody _rigidbody;
    BoxCollider _collider;
    bool _interactionHighlighted;
    bool _groundShowsBack;
    int _packVariantIndex = 1;
    List<CardDefinition> _preRolledContents;
    float _packModelGroundOffsetYFaceUp;
    float _packModelGroundOffsetYFaceDown;
    Vector3 _packModelCenterOffset;
    Vector3 _visualBaseScale = Vector3.one;
    GameObject _outlineObject;
    GameObject _handSelectionOutlineObject;
    int _groundStackLayer;
    bool _scaleTransitionActive;
    float _scaleFrom;
    float _scaleTo;
    float _scaleTransitionDuration;
    float _scaleTransitionElapsed;
    Coroutine _scaleTransitionRoutine;

    float _flightDuration = 0.4f;
    float _flightElapsed;
    float _flightArcHeight = 0.22f;
    float _flightTargetScale = 1f;
    Vector3 _flightStartWorldPos;
    Quaternion _flightStartWorldRot;
    float _flightStartWorldScale;
    System.Action _onPickupFlightComplete;

    public PackState State => _state;
    public bool IsInHand => _state == PackState.Held || _state == PackState.Opening;
    public bool IsHeld => _state == PackState.Held;
    public bool HasActivePhysics => _rigidbody != null && !_rigidbody.isKinematic;
    public int GroundStackLayer => _groundStackLayer;
    public BoosterPackDefinition Definition => packDefinition;
    public int PackVariantIndex => _packVariantIndex;
    public string PackDisplayName => PackArtLibrary.GetVariantDisplayName(_packVariantIndex);
    public bool GroundShowsBack => _groundShowsBack;

    public void SetGroundStackLayer(int layer)
    {
        _groundStackLayer = Mathf.Max(0, layer);
    }

    public void SetGroundShowsBack(bool showsBack)
    {
        if (_groundShowsBack == showsBack)
            return;

        _groundShowsBack = showsBack;
        if (_state == PackState.World && !HasActivePhysics)
            ApplyWorldVisualOrientation(alignPackModelToGround: true);
    }

    public void Initialize(
        BoosterPackDefinition definition,
        int packVariantIndex = 1,
        IReadOnlyList<CardDefinition> preRolledContents = null)
    {
        packDefinition = definition;
        _packVariantIndex = Mathf.Clamp(packVariantIndex, 1, PackArtLibrary.PackVariantCount);
        _preRolledContents = preRolledContents != null && preRolledContents.Count > 0
            ? new List<CardDefinition>(preRolledContents)
            : null;
        EnsureVisual();
        RefreshPackModelLayout();
        ApplyWorldVisualOrientation();
        ApplyPackModelShadowSettings();
    }

    void Awake()
    {
        _collider = GetComponent<BoxCollider>();
        if (_collider == null)
        {
            _collider = gameObject.AddComponent<BoxCollider>();
            PackFactory.ApplyFlatPackCollider(_collider);
        }
    }

    void EnsureVisual()
    {
        EnsureCardProxyVisual();
        EnsurePackModelVisual();
    }

    void EnsureCardProxyVisual()
    {
        if (_cardRef != null)
            return;

        Transform existing = transform.Find(CardRefChildName);
        if (existing != null)
        {
            _cardRef = existing;
            return;
        }

        CardArtLibrary.EnsureLoaded();

        var cardGo = new GameObject(CardRefChildName);
        cardGo.transform.SetParent(transform, false);
        cardGo.transform.localPosition = Vector3.zero;
        cardGo.transform.localRotation = CardArtLibrary.WorldVisualRotation;
        cardGo.transform.localScale = CardArtLibrary.WorldVisualScale;

        var meshFilter = cardGo.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CardArtLibrary.CardMesh;

        var meshRenderer = cardGo.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.enabled = false;

        _cardRef = cardGo.transform;
    }

    void EnsurePackModelVisual()
    {
        if (_packModel != null)
            return;

        EnsureCardProxyVisual();

        Transform existing = _cardRef.Find(PackModelChildName);
        if (existing == null)
            existing = transform.Find(PackModelChildName);

        if (existing != null)
        {
            existing.SetParent(_cardRef, false);
            existing.name = PackModelChildName;
            _packModel = existing;
            ConfigurePackModelFromExisting();
            return;
        }

        GameObject prefab = ResolveVisualPrefab();
        if (prefab != null)
        {
            var instance = Instantiate(prefab, _cardRef, false);
            instance.name = PackModelChildName;
            _packModel = instance.transform;
            _visualBaseScale = Vector3.one;

            PackArtLibrary.ApplyPackMaterials(_packModel, _packVariantIndex);
            StripVisualColliders(_packModel);
            ApplyPackModelShadowSettings();
            return;
        }

        CreatePlaceholderPackModel();
    }

    void ConfigurePackModelFromExisting()
    {
        _visualBaseScale = Vector3.one;

        PackArtLibrary.ApplyPackMaterials(_packModel, _packVariantIndex);
        StripVisualColliders(_packModel);
        ApplyPackModelShadowSettings();
    }

    void ApplyWorldVisualOrientation(bool alignPackModelToGround = true)
    {
        EnsureVisual();
        if (_cardRef == null)
            return;

        bool applyGroundPose = alignPackModelToGround && !HasActivePhysics;
        _cardRef.localPosition = applyGroundPose
            ? Vector3.up * GetPackModelGroundOffsetY()
            : Vector3.zero;
        _cardRef.localScale = CardArtLibrary.WorldVisualScale;
        SetCardRefMesh(CardArtLibrary.CardMesh);
        ApplyCardRefWorldRotation(alignPackModelToGround);
        ApplyPackModelLocalTransform();
    }

    void ApplyHandVisualOrientation()
    {
        EnsureVisual();
        if (_cardRef == null)
            return;

        _cardRef.localPosition = Vector3.zero;
        _cardRef.localRotation = GetPackHandVisualLocalRotation();
        _cardRef.localScale = CardArtLibrary.HandVisualScale;
        SetCardRefMesh(CardArtLibrary.HandCardMesh);
        ApplyPackModelLocalTransform();
    }

    // --- Pack orientation (imported mesh front/back are opposite the card-proxy basis) ---
    //
    // Ground spawn: flat root yaw + GetPackWorldGroundLocalRotation(showsBack) on the proxy.
    // Hand / reveal: GetPackHandVisualLocalRotation() — front always toward the player.
    // Physics drop: bake landed proxy world rotation into root; proxy local stays at
    // GetPackPhysicsWorldLocalRotation() for the whole tumble and settle (WorldCard pattern).

    /// <summary>Local proxy rotation while the pack tumbles under physics (fixed basis, like WorldCard).</summary>
    static Quaternion GetPackPhysicsWorldLocalRotation()
    {
        return CardArtLibrary.WorldVisualRotation;
    }

    /// <summary>Ground-rest proxy rotation; <paramref name="showsBack"/> true = back artwork visible.</summary>
    static Quaternion GetPackWorldGroundLocalRotation(bool showsBack)
    {
        Quaternion rotation = CardArtLibrary.WorldVisualRotation;
        if (!showsBack)
            rotation *= Quaternion.Euler(180f, 0f, 0f);
        return rotation;
    }

    /// <summary>Hand and reveal: pitch pack front toward the camera.</summary>
    static Quaternion GetPackHandVisualLocalRotation()
    {
        return CardArtLibrary.HandVisualRotation * Quaternion.Euler(180f, 0f, 0f);
    }

    void ApplyCardRefWorldRotation(bool alignPackModelToGround)
    {
        _cardRef.localRotation = alignPackModelToGround && !HasActivePhysics
            ? GetPackWorldGroundLocalRotation(_groundShowsBack)
            : GetPackPhysicsWorldLocalRotation();
    }

    void ApplyPackModelLocalTransform()
    {
        if (_packModel == null)
            return;

        _packModel.localPosition = _packModelCenterOffset;
        _packModel.localRotation = Quaternion.identity;
        _packModel.localScale = _visualBaseScale;
    }

    /// <summary>
    /// Centers the 3D pack on PackCardRef (same pivot as the invisible card), then computes
    /// the Y shift that rests the lowest pack point on the flat card collider bottom.
    /// </summary>
    void RefreshPackModelLayout()
    {
        EnsureVisual();
        if (_cardRef == null || _packModel == null)
            return;

        ApplyCardRefWorldPose(faceDown: false);

        _packModel.localPosition = Vector3.zero;
        _packModel.localRotation = Quaternion.identity;
        _packModel.localScale = Vector3.one;

        if (!TryMeasureMeshBoundsInLocalSpace(_cardRef, _packModel, out Vector3 nativeMin, out Vector3 nativeMax))
            return;

        Vector3 nativeSize = nativeMax - nativeMin;
        Vector3 cardSize = GetCardMeshSizeInCardRefLocalSpace();
        _visualBaseScale = ComputeCardFootprintScale(nativeSize, cardSize);

        _packModel.localScale = _visualBaseScale;

        if (TryMeasureMeshBoundsInLocalSpace(_cardRef, _packModel, out Vector3 min, out Vector3 max))
        {
            Vector3 center = (min + max) * 0.5f;
            _packModelCenterOffset = -center;
        }
        else
        {
            _packModelCenterOffset = Vector3.zero;
        }

        RefreshPackModelGroundOffset(faceDown: false, out _packModelGroundOffsetYFaceUp);
        RefreshPackModelGroundOffset(faceDown: true, out _packModelGroundOffsetYFaceDown);
    }

    Vector3 GetCardMeshSizeInCardRefLocalSpace()
    {
        if (_cardRef != null
            && TryMeasureSingleMeshBoundsInLocalSpace(_cardRef, _cardRef, out Vector3 min, out Vector3 max))
        {
            return max - min;
        }

        CardArtLibrary.EnsureLoaded();
        return CardArtLibrary.MeshBounds.size;
    }

    /// <summary>
    /// Non-uniform scale that maps pack width, height, and thickness onto the card footprint
    /// even when the imported model's axes do not line up with the card mesh axes.
    /// </summary>
    static Vector3 ComputeCardFootprintScale(Vector3 nativeSize, Vector3 cardSize)
    {
        GetFootprintAxes(nativeSize, out int nativeThickness, out int nativeWidth, out int nativeHeight);
        GetFootprintAxes(cardSize, out int cardThickness, out int cardWidth, out int cardHeight);

        var scale = Vector3.one;
        scale[nativeThickness] = cardSize[cardThickness] / Mathf.Max(nativeSize[nativeThickness], 0.00001f);
        scale[nativeWidth] = (cardSize[cardWidth] * PackWidthFitMultiplier) / Mathf.Max(nativeSize[nativeWidth], 0.00001f);
        scale[nativeHeight] = cardSize[cardHeight] / Mathf.Max(nativeSize[nativeHeight], 0.00001f);
        return scale;
    }

    static void GetFootprintAxes(Vector3 size, out int thicknessAxis, out int widthAxis, out int heightAxis)
    {
        thicknessAxis = SmallestAxis(size);

        int faceA = (thicknessAxis + 1) % 3;
        int faceB = (thicknessAxis + 2) % 3;
        if (size[faceA] <= size[faceB])
        {
            widthAxis = faceA;
            heightAxis = faceB;
        }
        else
        {
            widthAxis = faceB;
            heightAxis = faceA;
        }
    }

    static int SmallestAxis(Vector3 size)
    {
        int axis = 0;
        if (size.y < size[axis])
            axis = 1;
        if (size.z < size[axis])
            axis = 2;
        return axis;
    }

    float GetPackModelGroundOffsetY()
    {
        return _groundShowsBack ? _packModelGroundOffsetYFaceDown : _packModelGroundOffsetYFaceUp;
    }

    void RefreshPackModelGroundOffset(bool faceDown, out float groundOffsetY)
    {
        groundOffsetY = 0f;
        EnsureVisual();
        if (_cardRef == null || _packModel == null)
            return;

        ApplyCardRefWorldPose(faceDown);
        _cardRef.localPosition = Vector3.zero;
        ApplyPackModelProbePose();

        if (!TryMeasureRendererBoundsInLocalSpace(transform, _packModel, out Vector3 min, out Vector3 max))
            return;

        groundOffsetY = (-CardDimensions.Thickness * 0.5f) - min.y;
    }

    static bool IsOutlineObject(GameObject gameObject)
    {
        if (gameObject == null)
            return false;

        string objectName = gameObject.name;
        return objectName == "InteractionOutline" || objectName == "HandSelectionOutline";
    }

    void ApplyCardRefWorldPose(bool faceDown = false)
    {
        _cardRef.localPosition = Vector3.zero;
        _cardRef.localScale = CardArtLibrary.WorldVisualScale;
        _cardRef.localRotation = GetPackWorldGroundLocalRotation(faceDown);
    }

    void ApplyPackModelProbePose()
    {
        if (_packModel == null)
            return;

        _packModel.localPosition = _packModelCenterOffset;
        _packModel.localRotation = Quaternion.identity;
        _packModel.localScale = _visualBaseScale;
    }

    static bool TryMeasureMeshBoundsInLocalSpace(
        Transform localSpace,
        Transform meshRoot,
        out Vector3 min,
        out Vector3 max)
    {
        min = Vector3.positiveInfinity;
        max = Vector3.negativeInfinity;
        if (localSpace == null || meshRoot == null)
            return false;

        MeshFilter[] meshFilters = meshRoot.GetComponentsInChildren<MeshFilter>(true);
        if (meshFilters.Length == 0)
            return false;

        bool hasBounds = false;
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            if (meshFilter == null || meshFilter.sharedMesh == null || IsOutlineObject(meshFilter.gameObject))
                continue;

            EncapsulateMeshBounds(localSpace, meshFilter.transform, meshFilter.sharedMesh.bounds, ref min, ref max);
            hasBounds = true;
        }

        return hasBounds;
    }

    static bool TryMeasureSingleMeshBoundsInLocalSpace(
        Transform localSpace,
        Transform meshTransform,
        out Vector3 min,
        out Vector3 max)
    {
        min = Vector3.positiveInfinity;
        max = Vector3.negativeInfinity;
        if (localSpace == null || meshTransform == null)
            return false;

        MeshFilter meshFilter = meshTransform.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null || IsOutlineObject(meshFilter.gameObject))
            return false;

        EncapsulateMeshBounds(localSpace, meshTransform, meshFilter.sharedMesh.bounds, ref min, ref max);
        return min.x <= max.x;
    }

    static void EncapsulateMeshBounds(
        Transform localSpace,
        Transform meshTransform,
        Bounds meshBounds,
        ref Vector3 min,
        ref Vector3 max)
    {
        Vector3 center = meshBounds.center;
        Vector3 extents = meshBounds.extents;
        for (int xi = -1; xi <= 1; xi += 2)
        {
            for (int yi = -1; yi <= 1; yi += 2)
            {
                for (int zi = -1; zi <= 1; zi += 2)
                {
                    Vector3 corner = center + new Vector3(
                        extents.x * xi,
                        extents.y * yi,
                        extents.z * zi);
                    Vector3 local = localSpace.InverseTransformPoint(meshTransform.TransformPoint(corner));
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }
        }
    }

    static bool TryMeasureRendererBoundsInLocalSpace(
        Transform localSpace,
        Transform rendererRoot,
        out Vector3 min,
        out Vector3 max)
    {
        min = Vector3.positiveInfinity;
        max = Vector3.negativeInfinity;
        if (localSpace == null || rendererRoot == null)
            return false;

        Renderer[] renderers = rendererRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return false;

        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || IsOutlineRenderer(renderer))
                continue;

            Bounds bounds = renderer.bounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            for (int xi = -1; xi <= 1; xi += 2)
            {
                for (int yi = -1; yi <= 1; yi += 2)
                {
                    for (int zi = -1; zi <= 1; zi += 2)
                    {
                        Vector3 corner = center + new Vector3(
                            extents.x * xi,
                            extents.y * yi,
                            extents.z * zi);
                        Vector3 local = localSpace.InverseTransformPoint(corner);
                        min = Vector3.Min(min, local);
                        max = Vector3.Max(max, local);
                        hasBounds = true;
                    }
                }
            }
        }

        return hasBounds;
    }

    static bool IsOutlineRenderer(Renderer renderer)
    {
        return renderer != null && IsOutlineObject(renderer.gameObject);
    }

    void SetCardRefMesh(Mesh mesh)
    {
        if (_cardRef == null || mesh == null)
            return;

        var meshFilter = _cardRef.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != mesh)
            meshFilter.sharedMesh = mesh;
    }

    GameObject ResolveVisualPrefab()
    {
        if (visualPrefab != null)
            return visualPrefab;

        if (_defaultPackModelPrefab == null)
            _defaultPackModelPrefab = Resources.Load<GameObject>(DefaultPackModelResourcePath);

        return _defaultPackModelPrefab;
    }

    static void StripVisualColliders(Transform visualRoot)
    {
        Collider[] colliders = visualRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                Destroy(colliders[i]);
        }
    }

    void CreatePlaceholderPackModel()
    {
        var visualGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visualGo.name = PackModelChildName;
        visualGo.transform.SetParent(_cardRef, false);
        _visualBaseScale = new Vector3(
            CardDimensions.Width,
            CardDimensions.Height,
            CardDimensions.Thickness * 2.5f);
        visualGo.transform.localScale = _visualBaseScale;
        visualGo.transform.localRotation = Quaternion.identity;

        var renderer = visualGo.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = CreatePlaceholderMaterial();
        }

        StripVisualColliders(visualGo.transform);
        _packModel = visualGo.transform;
        ApplyPackModelShadowSettings();
    }

    void ApplyPackModelShadowSettings()
    {
        if (_packModel == null)
            return;

        var renderers = _packModel.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (_state == PackState.World)
                PackArtLibrary.ApplyPackMaterials(renderer, _packVariantIndex);
        }
    }

    Material CreatePlaceholderMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader);
        material.color = new Color(0.72f, 0.58f, 0.18f, 1f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.65f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0.35f);
        CardArtLibrary.ConfigureGroundWorldMaterial(material);
        return material;
    }

    public string GetPromptText()
    {
        if (IsInHand)
            return string.Empty;

        PlayerCardHand hand = PlayerCardHand.Instance;
        if (hand == null)
            return "Press [E] To Pick Up " + PackDisplayName;

        if (!hand.CanPickUpPack)
        {
            if (hand.AvailableSlots <= 0)
                return "Hand Full (" + CardDimensions.MaxHandSize + "/" + CardDimensions.MaxHandSize + ")";

            return "Hand Full";
        }

        return "Press [E] To Pick Up " + PackDisplayName;
    }

    public void Interact(GameObject interactor)
    {
        if (IsInHand)
            return;

        PlayerCardHand hand = PlayerCardHandResolver.FromInteractorOrInstance(interactor);
        if (hand == null)
            return;

        hand.TryPickupPack(this);
    }

    public void SetInteractionHighlight(bool highlighted)
    {
        _interactionHighlighted = highlighted && _state == PackState.World;
        RefreshOutlineVisuals();
    }

    public void SetHandSelected(bool selected)
    {
        if (selected)
            EnsureHandSelectionOutlineRenderer();
        else if (_handSelectionOutlineObject != null)
            _handSelectionOutlineObject.SetActive(false);

        if (_handSelectionOutlineObject != null)
            _handSelectionOutlineObject.SetActive(selected && IsHeld);
    }

    void RefreshOutlineVisuals()
    {
        if (_state != PackState.World)
        {
            ReleaseInteractionOutline();
            return;
        }

        if (_interactionHighlighted)
        {
            EnsureInteractionOutlineRenderer();
            if (_outlineObject != null)
                _outlineObject.SetActive(true);
            return;
        }

        ReleaseInteractionOutline();
    }

    Transform GetOutlineParent()
    {
        EnsureVisual();
        return _cardRef != null ? _cardRef : transform;
    }

    void EnsureInteractionOutlineRenderer()
    {
        if (_outlineObject != null)
            return;

        _ = CardVisualResources.InteractionOutlineMaterial;
        _outlineObject = new GameObject("InteractionOutline");
        _outlineObject.transform.SetParent(GetOutlineParent(), false);

        var meshFilter = _outlineObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CardVisualResources.InteractionBorderFrameMesh;

        var meshRenderer = _outlineObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = CardVisualResources.InteractionOutlineMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    void EnsureHandSelectionOutlineRenderer()
    {
        if (_handSelectionOutlineObject == null)
        {
            _ = CardVisualResources.HandSelectionOutlineMaterial;
            _handSelectionOutlineObject = new GameObject("HandSelectionOutline");

            var meshFilter = _handSelectionOutlineObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = CardVisualResources.HandSelectionBorderFrameMesh;

            var meshRenderer = _handSelectionOutlineObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = CardVisualResources.HandSelectionOutlineMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        _handSelectionOutlineObject.transform.SetParent(GetOutlineParent(), false);
        ApplyHandSelectionOutlineLocalPose();
    }

    /// <summary>
    /// Shared card border mesh assumes <see cref="CardArtLibrary.HandVisualRotation"/> on the parent.
    /// Pack hand adds 180° X so front faces the player — cancel that for the outline only.
    /// </summary>
    void ApplyHandSelectionOutlineLocalPose()
    {
        if (_handSelectionOutlineObject == null)
            return;

        _handSelectionOutlineObject.transform.localPosition = Vector3.zero;
        _handSelectionOutlineObject.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
    }

    void ReleaseInteractionOutline()
    {
        if (_outlineObject == null)
            return;

        if (Application.isPlaying)
            Destroy(_outlineObject);
        else
            DestroyImmediate(_outlineObject);

        _outlineObject = null;
    }

    void ReleaseHandSelectionOutline()
    {
        if (_handSelectionOutlineObject == null)
            return;

        if (Application.isPlaying)
            Destroy(_handSelectionOutlineObject);
        else
            DestroyImmediate(_handSelectionOutlineObject);

        _handSelectionOutlineObject = null;
    }

    public void BeginPickupFlight(
        Transform handAnchor,
        float targetHandScale,
        float duration,
        float arcHeight,
        System.Action onComplete = null)
    {
        _state = PackState.FlyingToHand;
        _handAnchor = handAnchor;
        _flightTargetScale = targetHandScale;
        _flightDuration = Mathf.Max(0.05f, duration);
        _flightElapsed = 0f;
        _flightArcHeight = arcHeight;
        _onPickupFlightComplete = onComplete;

        SetInteractionHighlight(false);
        SetHandSelected(false);
        _groundShowsBack = false;
        CardGroundStack.UntrackPack(this);
        RemovePhysics();

        if (_collider != null)
            _collider.enabled = false;

        transform.SetParent(null, true);
        EnsureVisual();
        ApplyHandVisualOrientation();
        AlignRootRotationForHandPickup();
        _flightStartWorldPos = transform.position;
        _flightStartWorldRot = transform.rotation;
        _flightStartWorldScale = transform.localScale.x;
    }

    /// <summary>
    /// Bakes hand-front visual into the root so pickup flight does not inherit ground face-up/down.
    /// </summary>
    void AlignRootRotationForHandPickup()
    {
        if (_cardRef == null)
            return;

        Quaternion handLocal = GetPackHandVisualLocalRotation();
        transform.rotation = _cardRef.rotation * Quaternion.Inverse(handLocal);
        ApplyHandVisualOrientation();
    }

    public void UpdatePickupFlight(Vector3 targetWorldPos, Quaternion targetWorldRot)
    {
        if (_state != PackState.FlyingToHand)
            return;

        AdvanceFlightToward(targetWorldPos, targetWorldRot);

        if (_flightElapsed >= _flightDuration)
            CompletePickupFlight();
    }

    void CompletePickupFlight()
    {
        _state = PackState.Held;
        transform.SetParent(_handAnchor, false);
        EnsureVisual();
        ApplyHandVisualOrientation();

        System.Action callback = _onPickupFlightComplete;
        _onPickupFlightComplete = null;
        callback?.Invoke();
    }

    public void ApplyHeldPose(Vector3 localPosition, Quaternion localRotation, float scale)
    {
        if (_state != PackState.Held && _state != PackState.Opening)
            return;

        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
        transform.localScale = Vector3.one * scale;
        ApplyHandVisualOrientation();
    }

    public void BeginOpening()
    {
        _state = PackState.Opening;
        SetHandSelected(false);
        ReleaseInteractionOutline();
        transform.SetParent(null, true);
    }

    public void ApplyRevealOpenPose(Quaternion revealRootLocalRotation)
    {
        EnsureVisual();
        transform.localRotation = revealRootLocalRotation;
        ApplyHandVisualOrientation();
    }

    public void DropWithPhysics(Vector3 velocity, float worldScaleTransitionDuration = 0.12f)
    {
        _state = PackState.World;
        SetHandSelected(false);
        EnsureVisual();
        ConvertHandVisualToWorldRoot();
        transform.SetParent(null, true);

        ApplyFlatWorldCollider();

        if (_collider is BoxCollider boxCollider)
        {
            boxCollider.isTrigger = false;
            _collider.enabled = true;
        }
        else if (_collider != null)
        {
            _collider.enabled = true;
        }

        IgnorePlayerCollision();

        BeginScaleTransition(transform.localScale.x, CardDimensions.GroundCardScale, worldScaleTransitionDuration);
        ApplyPackModelShadowSettings();

        EnsureRigidbody();
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = true;
        _rigidbody.constraints = RigidbodyConstraints.None;

        if (!_rigidbody.isKinematic)
        {
            _rigidbody.linearVelocity = velocity;
            _rigidbody.angularVelocity = new Vector3(
                Random.Range(-0.2f, 0.2f),
                Random.Range(-0.35f, 0.35f),
                Random.Range(-0.2f, 0.2f));
        }

        CardGroundStack.EnableLandingCollidersNear(transform.position, 2.5f);
        StartCoroutine(SettleDroppedPackRoutine());
    }

    void ConvertHandVisualToWorldRoot()
    {
        EnsureVisual();
        if (_cardRef == null)
            return;

        // Bake the hand pose into root rotation; proxy uses the fixed physics basis during the drop.
        transform.rotation = _cardRef.rotation * Quaternion.Inverse(GetPackPhysicsWorldLocalRotation());
        ApplyWorldVisualOrientation(alignPackModelToGround: false);
    }

    void BeginScaleTransition(float fromScale, float toScale, float duration)
    {
        _scaleFrom = fromScale;
        _scaleTo = toScale;
        _scaleTransitionDuration = Mathf.Max(0.01f, duration);
        _scaleTransitionElapsed = 0f;
        _scaleTransitionActive = true;
        transform.localScale = Vector3.one * fromScale;
        if (_scaleTransitionRoutine != null)
            StopCoroutine(_scaleTransitionRoutine);
        _scaleTransitionRoutine = StartCoroutine(ScaleTransitionRoutine());
    }

    IEnumerator ScaleTransitionRoutine()
    {
        while (_scaleTransitionActive && _state == PackState.World)
        {
            _scaleTransitionElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_scaleTransitionElapsed / _scaleTransitionDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            transform.localScale = Vector3.one * Mathf.Lerp(_scaleFrom, _scaleTo, smoothT);

            if (t >= 1f)
            {
                _scaleTransitionActive = false;
                _scaleTransitionRoutine = null;
                yield break;
            }

            yield return null;
        }

        _scaleTransitionActive = false;
        _scaleTransitionRoutine = null;
    }

    IEnumerator SettleDroppedPackRoutine()
    {
        float groundedTime = 0f;
        float elapsed = 0f;
        const float settleAfterGrounded = 0.18f;
        const float forceSettleAfterGrounded = 0.55f;
        const float maxFlightTime = 4f;

        try
        {
            while (_state == PackState.World && _rigidbody != null)
            {
                elapsed += Time.deltaTime;

                bool scaleDone = !_scaleTransitionActive;
                float groundY = CardFactory.GroundHeightOffset();
                float maxSettleY = groundY + CardGroundStack.StackStep * 64f + 0.25f;
                bool nearGround = transform.position.y <= maxSettleY;
                bool fallingOrResting = _rigidbody.linearVelocity.y <= 0.35f;
                bool slowEnough = _rigidbody.linearVelocity.sqrMagnitude < 0.35f;

                if (nearGround && fallingOrResting)
                    _rigidbody.angularVelocity *= 0.85f;

                if (scaleDone && nearGround && fallingOrResting && slowEnough)
                {
                    groundedTime += Time.deltaTime;
                    ResolveWorldPenetration(_rigidbody);
                }
                else if (!nearGround)
                {
                    groundedTime = 0f;
                }

                float horizontalSpeedSq =
                    _rigidbody.linearVelocity.x * _rigidbody.linearVelocity.x
                    + _rigidbody.linearVelocity.z * _rigidbody.linearVelocity.z;

                bool slowSlide = horizontalSpeedSq < 2.5f;
                if (groundedTime >= settleAfterGrounded && slowSlide)
                    break;
                if (groundedTime >= forceSettleAfterGrounded)
                    break;
                if (elapsed >= maxFlightTime)
                    break;

                yield return null;
            }

            if (_state != PackState.World || _rigidbody == null)
                yield break;

            SetInteractionHighlight(false);
            CardGroundStack.EnableLandingCollidersNear(transform.position, 1.75f);
            RemovePhysics();
            FlattenAndSnapToGround();
            RefreshOutlineVisuals();
        }
        finally
        {
            CardGroundStack.RestoreLandingColliders();
        }
    }

    /// <summary>
    /// After a physics drop the proxy keeps <see cref="GetPackPhysicsWorldLocalRotation"/> local.
    /// Back-up lands with root.up ≈ +Y; front-up lands inverted (root.up ≈ −Y). Proxy forward.y
    /// alone cannot split the two — it is ≥ 0 for both inverted-mesh landings.
    /// </summary>
    static bool ReadGroundShowsBackFromLandedProxy(Transform cardRef, Transform root)
    {
        if (root != null && Mathf.Abs(root.up.y) > 0.35f)
            return root.up.y >= 0f;

        return cardRef != null && cardRef.forward.y >= 0f;
    }

    static Vector3 ReadGroundSettleHeading(Transform cardRef, Transform root, bool groundShowsBack)
    {
        Vector3 headingSource = !groundShowsBack && cardRef != null
            ? cardRef.up
            : root != null ? root.forward : Vector3.forward;

        Vector3 heading = Vector3.ProjectOnPlane(headingSource, Vector3.up);
        if (heading.sqrMagnitude < 0.0001f && cardRef != null)
            heading = Vector3.ProjectOnPlane(cardRef.forward, Vector3.up);
        if (heading.sqrMagnitude < 0.0001f && root != null)
            heading = Vector3.ProjectOnPlane(root.right, Vector3.up);
        if (heading.sqrMagnitude < 0.0001f)
            heading = Vector3.forward;
        heading.Normalize();
        return heading;
    }

    /// <summary>
    /// Lays the pack flat on the floor (yaw-only root) while keeping the landed front/back face.
    /// Face is sampled before flattening so tumble pitch does not skew the result.
    /// </summary>
    void FlattenAndSnapToGround()
    {
        EnsureVisual();
        ApplyFlatWorldCollider();

        _groundShowsBack = ReadGroundShowsBackFromLandedProxy(_cardRef, transform);
        Vector3 heading = ReadGroundSettleHeading(_cardRef, transform, _groundShowsBack);

        transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
        ApplyWorldVisualOrientation(alignPackModelToGround: true);

        ApplyPackModelShadowSettings();
        CardGroundStack.ApplyStackHeight(this, placeOnTop: true);

        if (_collider != null)
        {
            _collider.isTrigger = false;
            _collider.enabled = true;
        }
    }

    void ResolveWorldPenetration(Rigidbody body = null)
    {
        if (_collider is not BoxCollider boxCollider)
            return;

        CardCollisionUtility.ResolveStaticPenetration(transform, boxCollider, null, body);
    }

    public IReadOnlyList<CardDefinition> RollContents(int count)
    {
        if (_preRolledContents != null && _preRolledContents.Count > 0)
        {
            int take = Mathf.Min(count, _preRolledContents.Count);
            return _preRolledContents.GetRange(0, take);
        }

        var results = new List<CardDefinition>(count);
        IReadOnlyList<CardDefinition> pool = packDefinition != null
            ? packDefinition.BuildCardPool()
            : BuildDefaultPool();

        if (pool.Count == 0)
        {
            Debug.LogWarning("WorldBoosterPack: No card definitions available for pack contents.");
            return results;
        }

        for (int i = 0; i < count; i++)
            results.Add(pool[Random.Range(0, pool.Count)]);

        return results;
    }

    static IReadOnlyList<CardDefinition> BuildDefaultPool()
    {
        CardCatalog.Reload();
        return CardCatalog.All;
    }

    void AdvanceFlightToward(Vector3 targetWorldPos, Quaternion targetWorldRot)
    {
        _flightElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_flightElapsed / _flightDuration);
        float smoothT = Mathf.SmoothStep(0f, 1f, t);

        Vector3 pos = Vector3.Lerp(_flightStartWorldPos, targetWorldPos, smoothT);
        pos += Vector3.up * (Mathf.Sin(smoothT * Mathf.PI) * _flightArcHeight);

        transform.SetPositionAndRotation(pos, Quaternion.Slerp(_flightStartWorldRot, targetWorldRot, smoothT));
        float scale = Mathf.Lerp(_flightStartWorldScale, _flightTargetScale, smoothT);
        transform.localScale = Vector3.one * scale;
    }

    void ApplyFlatWorldCollider()
    {
        if (_collider is BoxCollider boxCollider)
            CardCollisionUtility.ApplyFlatWorldSize(boxCollider);
    }

    void EnsureRigidbody()
    {
        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody>();

        if (_rigidbody != null)
            return;

        _rigidbody = gameObject.AddComponent<Rigidbody>();
        _rigidbody.mass = 0.05f;
        _rigidbody.linearDamping = 0.4f;
        _rigidbody.angularDamping = 0.8f;
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void RemovePhysics()
    {
        Rigidbody rb = _rigidbody != null ? _rigidbody : GetComponent<Rigidbody>();
        if (rb == null)
        {
            _rigidbody = null;
            return;
        }

        DestroyImmediate(rb);
        _rigidbody = null;
    }

    static FirstPersonController _cachedPlayer;

    void IgnorePlayerCollision()
    {
        if (_collider == null)
            return;

        if (_cachedPlayer == null)
            _cachedPlayer = FindFirstObjectByType<FirstPersonController>();
        if (_cachedPlayer == null)
            return;

        Collider[] playerColliders = _cachedPlayer.GetComponentsInChildren<Collider>();
        for (int i = 0; i < playerColliders.Length; i++)
        {
            if (playerColliders[i] != null)
                Physics.IgnoreCollision(_collider, playerColliders[i], true);
        }
    }
}
