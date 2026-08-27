using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pickupable booster pack on the ground or held in the player's hand.
/// Physics/orientation follow an invisible <see cref="PackCardRef"/> child that mirrors
/// <see cref="WorldCard"/>; the imported 3D pack mesh is cosmetic only.
/// </summary>
[SelectionBase]
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
    [SerializeField] int packVariantIndex = 1;

    const string CardRefChildName = "PackCardRef";
    const string CardProxyMeshChildName = "CardProxyMesh";
    const string PackModelChildName = "PackVisual";
    const string DefaultPackModelResourcePath = "Cards/BoosterPack/TradingCard_BoosterPack";
    static GameObject _defaultPackModelPrefab;
    const float PackWidthFitMultiplier = 1.06f;

    PackState _state = PackState.World;
    Transform _cardRef;
    Transform _cardProxyMesh;
    Transform _packModel;
    Transform _handAnchor;
    Rigidbody _rigidbody;
    BoxCollider _collider;
    bool _interactionHighlighted;
    bool _handSelected;
    bool _groundShowsBack;
    List<CardDefinition> _preRolledContents;
    float _packModelGroundOffsetYFaceUp;
    float _packModelGroundOffsetYFaceDown;
    float _packBodyThickness;
    Vector3 _packModelCenterOffset;
    Vector3 _visualBaseScale = Vector3.one;
    int _packNativeThicknessAxis;
    bool _hasPackNativeThicknessAxis;
    Vector3 _packOutlineSize;
    Vector3 _packOutlineCenterLocal;
    bool _hasPackOutlineBounds;
    Outline _packOutline;
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
    Renderer[] _packRenderers;
    bool _groundModelRenderersVisible = true;
    bool _authoredLevelItem;
    readonly Dictionary<int, Material[]> _liveHandMaterialsByRenderer = new Dictionary<int, Material[]>();

    public PackState State => _state;
    public bool IsInHand => _state == PackState.Held || _state == PackState.Opening;
    public bool IsHeld => _state == PackState.Held;
    public bool IsAuthoredLevelItem => _authoredLevelItem;

    /// <summary>
    /// True once the pack has been thrown, meaning it stands in the tumbled pose the solver left it in
    /// rather than the flattened ground pose. Visuals, renderer swaps and ray proxies all key off this.
    /// </summary>
    public bool HasActivePhysics => _rigidbody != null;

    /// <summary>
    /// True only while the solver is still moving the pack. A settled pack keeps a frozen (kinematic)
    /// body as its solid surface, so <see cref="HasActivePhysics"/> alone cannot tell "still flying"
    /// from "already at rest" — stack layering needs this distinction.
    /// </summary>
    public bool IsPhysicsSimulating => _rigidbody != null && !_rigidbody.isKinematic;
    public int GroundStackLayer => _groundStackLayer;
    public BoosterPackDefinition Definition => packDefinition;
    public int PackVariantIndex => packVariantIndex;
    public string PackDisplayName => PackArtLibrary.GetVariantDisplayName(packVariantIndex);
    public bool GroundShowsBack => _groundShowsBack;
    public Transform PackVisualRoot => _packModel;
    internal BoxCollider PhysCollider => _collider;
    internal Rigidbody PhysicsBody => _rigidbody;

    public Bounds GetCullBounds()
    {
        float scale = Mathf.Max(transform.lossyScale.x, 0.01f);
        Vector3 size = new Vector3(
            CardDimensions.Width * scale * PackWidthFitMultiplier,
            CardDimensions.Thickness * scale * 6f,
            CardDimensions.Height * scale);
        Vector3 center = transform.position;
        return new Bounds(center, size);
    }

    public void SetGroundModelVisible(bool visible)
    {
        if (_groundModelRenderersVisible == visible)
            return;

        _groundModelRenderersVisible = visible;
        ApplyPackRendererVisibility();
    }

    public void SetGroundStackLayer(int layer)
    {
        _groundStackLayer = Mathf.Max(0, layer);
    }

    /// <summary>
    /// Re-applies a saved world pose without the flat-floor visual lift that would shift a leaning
    /// pack (e.g. against glass) along its local up and push it through walls on load.
    /// </summary>
    public void RestoreSavedWorldPose(bool faceDown, int stackLayer)
    {
        _state = PackState.World;
        _groundShowsBack = faceDown;
        SetGroundStackLayer(stackLayer);

        // Flat packs match scatter/spawn (ground mesh lift). Tilted packs match post-throw settle
        // (physics visual, mesh centred on root) — see CardSettlePlacement.FlatUpDot.
        bool upright = CardSettlePlacement.IsFlatOnFloor(transform);
        ApplyWorldVisualOrientation(alignPackModelToGround: upright);

        if (_collider != null)
        {
            _collider.enabled = true;
            if (_collider is BoxCollider boxCollider)
                boxCollider.isTrigger = false;
        }

        ApplyPackModelShadowSettings();
    }

    /// <summary>
    /// Play-mode hook for Grabbit-authored packs: keep the scene pose, show the model, track for aim.
    /// </summary>
    public void RegisterForAuthoredGround()
    {
        if (IsInHand)
            return;

        ResolvePackVariantFromScene();
        EnsureVisual();
        CaptureExistingVisualLayout();
        ApplyPackBodyCollider();
        LiftMeshAboveFloor();
        EnsureContentsPreRolled();
        CardGroundStack.TrackPack(this);
        SetGroundModelVisible(true);
    }

    void ResolvePackVariantFromScene()
    {
        int fromName = InferVariantIndexFromObjectName();
        if (fromName > 0)
            packVariantIndex = fromName;

        packVariantIndex = Mathf.Clamp(packVariantIndex, 1, PackArtLibrary.PackVariantCount);
    }

    int InferVariantIndexFromObjectName()
    {
        const string prefix = "BoosterPack_";
        string objectName = gameObject.name;
        if (!objectName.StartsWith(prefix))
            return 0;

        string suffix = objectName.Substring(prefix.Length);
        // Mix packs are BoosterPack_M3_9 — the trailing index is spawn order, not art variant.
        if (suffix.Length == 0 || suffix[0] == 'M')
            return 0;

        if (!int.TryParse(suffix, out int index))
            return 0;

        if (index < 1 || index > PackArtLibrary.PackVariantCount)
            return 0;

        return index;
    }

    /// <summary>
    /// Moves the pack and drags its physics body along — see <see cref="WorldCard.SetGroundRestPosition"/>
    /// for why the transform alone is not enough with Auto Sync Transforms off.
    /// </summary>
    public void SetGroundRestPosition(Vector3 position)
    {
        transform.position = position;
        if (_rigidbody != null)
            _rigidbody.position = position;
    }

    public void SetGroundShowsBack(bool showsBack)
    {
        if (_groundShowsBack == showsBack)
            return;

        _groundShowsBack = showsBack;
        if (_state == PackState.World && !IsPhysicsSimulating)
            ApplyWorldVisualOrientation(alignPackModelToGround: true);
    }

    public void Initialize(
        BoosterPackDefinition definition,
        int packVariantIndex = 1,
        IReadOnlyList<CardDefinition> preRolledContents = null)
    {
        packDefinition = definition;
        this.packVariantIndex = Mathf.Clamp(packVariantIndex, 1, PackArtLibrary.PackVariantCount);
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
        CardLayers.ApplyToGameObject(gameObject);
        _authoredLevelItem = GetComponent<PhysicsLevelItem>() != null
            || GetComponentInParent<PhysicsLevelLayout>() != null;
        ResolvePackVariantFromScene();
        _collider = GetComponent<BoxCollider>();
        if (_collider == null)
        {
            _collider = gameObject.AddComponent<BoxCollider>();
            PackFactory.ApplyFlatPackCollider(_collider);
        }

        if (Application.isPlaying)
            EnsureVisual();
    }

    public void PrepareEditorPhysicsPlacement()
    {
        EnsureVisual();
        CaptureExistingVisualLayout();
        if (_cardRef != null)
        {
            _cardRef.localPosition = Vector3.zero;
            ApplyCardRefWorldRotation(alignPackModelToGround: false);
            ApplyPackModelLocalTransform();
        }

        if (_collider == null)
            _collider = GetComponent<BoxCollider>();
        if (_collider == null)
            return;

        ApplyPackAuthoringCollider();
        _collider.isTrigger = false;
        _collider.enabled = true;
    }

    /// <summary>
    /// Grabbit must collide with the 3D pack mesh, not a card-thin box. The thin authoring
    /// collider stood packs on edge and left the mesh floating above the green gizmo.
    /// Does not move or rotate the pack — baked Mix 1 poses stay put.
    /// </summary>
    void ApplyPackAuthoringCollider()
    {
        // Tight mesh box in root space. Centering a thick box on the card-proxy lift left the
        // collider under the mesh, so Grabbit sat the box on the pile and the pack floated.
        if (_packModel != null
            && TryMeasureMeshBoundsInLocalSpace(transform, _packModel, out Vector3 min, out Vector3 max))
        {
            Vector3 size = max - min;
            float maxAxis = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float maxAllowed = Mathf.Max(CardDimensions.Width, CardDimensions.Height) * 2.5f;
            if (maxAxis > 0.001f && maxAxis <= maxAllowed)
            {
                const float minSize = 0.012f;
                size.x = Mathf.Max(size.x, minSize);
                size.y = Mathf.Max(size.y, minSize);
                size.z = Mathf.Max(size.z, minSize);
                _collider.center = (min + max) * 0.5f;
                _collider.size = size;
                CardCollisionUtility.ApplyToCollider(_collider);
                _packBodyThickness = Mathf.Max(CardDimensions.Thickness, size.y);
                return;
            }
        }

        ApplyPackBodyCollider();
    }

    /// <summary>
    /// If the visible pack sits above its collider after a fall, lower the root so the mesh
    /// rests where physics did. Mix 5+ bake only; does not run on already-baked Mix 1–4.
    /// </summary>
    public void SnapAuthoredVisualOntoCollider()
    {
        if (_collider == null)
            return;

        CachePackRenderers();
        if (_packRenderers == null || _packRenderers.Length == 0)
            return;

        float meshMinY = float.PositiveInfinity;
        bool any = false;
        for (int i = 0; i < _packRenderers.Length; i++)
        {
            Renderer renderer = _packRenderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            meshMinY = Mathf.Min(meshMinY, renderer.bounds.min.y);
            any = true;
        }

        if (!any || float.IsInfinity(meshMinY))
            return;

        float delta = meshMinY - _collider.bounds.min.y;
        if (delta < 0.0015f || delta > 0.12f)
            return;

        Vector3 position = transform.position;
        position.y -= delta;
        transform.position = position;
        if (_rigidbody != null)
            _rigidbody.position = position;
    }

    /// <summary>
    /// Grabbit rests a card-thin collider on the floor while the 3D pack mesh is thicker,
    /// so the model hangs under the shop floor. Lift until the mesh underside clears it.
    /// </summary>
    public void LiftMeshAboveFloor()
    {
        EnsureVisual();
        if (_packModel == null)
            return;

        CachePackRenderers();
        if (_packRenderers == null || _packRenderers.Length == 0)
            return;

        Vector3 rootPosition = transform.position;
        float lowest = float.PositiveInfinity;
        bool any = false;
        for (int i = 0; i < _packRenderers.Length; i++)
        {
            Renderer renderer = _packRenderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            Bounds bounds = renderer.bounds;
            if (bounds.size.sqrMagnitude < 0.0001f)
                continue;
            // After MenuScene → MainScene load, renderer bounds can still be at the origin
            // for a frame. Using those would launch authored packs into the air.
            if ((bounds.center - rootPosition).sqrMagnitude > 4f)
                continue;

            lowest = Mathf.Min(lowest, bounds.min.y);
            any = true;
        }

        if (!any || float.IsInfinity(lowest))
            return;

        float floorY = CardFactory.GroundSurfaceY();
        const float skin = 0.003f;
        float lift = floorY + skin - lowest;
        if (lift <= 0.0005f || lift > 0.12f)
            return;

        Vector3 position = rootPosition;
        position.y += lift;
        transform.position = position;
        if (_rigidbody != null)
            _rigidbody.position = position;
    }

    public void StripEditorRigidbody()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
            DestroyImmediate(body);
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
            EnsureCardProxyMesh();
            return;
        }

        CardArtLibrary.EnsureLoaded();

        var cardGo = new GameObject(CardRefChildName);
        cardGo.transform.SetParent(transform, false);
        cardGo.transform.localPosition = Vector3.zero;
        cardGo.transform.localRotation = CardArtLibrary.WorldVisualRotation;
        cardGo.transform.localScale = CardArtLibrary.WorldVisualScale;

        _cardRef = cardGo.transform;
        EnsureCardProxyMesh();
    }

    void EnsureCardProxyMesh()
    {
        if (_cardRef == null)
            return;

        if (_cardProxyMesh != null)
            return;

        Transform existing = _cardRef.Find(CardProxyMeshChildName);
        if (existing != null)
        {
            _cardProxyMesh = existing;
            return;
        }

        CardArtLibrary.EnsureLoaded();

        var meshGo = new GameObject(CardProxyMeshChildName);
        meshGo.transform.SetParent(_cardRef, false);
        meshGo.transform.localPosition = Vector3.zero;
        meshGo.transform.localRotation = Quaternion.identity;
        meshGo.transform.localScale = Vector3.one;

        var meshFilter = meshGo.AddComponent<MeshFilter>();
        var meshRenderer = meshGo.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.enabled = false;

        MeshFilter rootFilter = _cardRef.GetComponent<MeshFilter>();
        MeshRenderer rootRenderer = _cardRef.GetComponent<MeshRenderer>();
        if (rootFilter != null)
        {
            meshFilter.sharedMesh = rootFilter.sharedMesh;
            if (rootRenderer != null)
            {
                meshRenderer.enabled = rootRenderer.enabled;
                if (Application.isPlaying)
                {
                    Destroy(rootRenderer);
                    Destroy(rootFilter);
                }
                else
                {
                    DestroyImmediate(rootRenderer);
                    DestroyImmediate(rootFilter);
                }
            }
            else if (Application.isPlaying)
                Destroy(rootFilter);
            else
                DestroyImmediate(rootFilter);
        }
        else
        {
            meshFilter.sharedMesh = CardArtLibrary.CardMesh;
        }

        _cardProxyMesh = meshGo.transform;
    }

    void RefreshCardProxyCenterOnPack()
    {
        EnsureCardProxyMesh();
        if (_cardProxyMesh == null)
            return;

        if (!_hasPackOutlineBounds)
        {
            _cardProxyMesh.localPosition = Vector3.zero;
            return;
        }

        if (!TryMeasureSingleMeshBoundsInLocalSpace(_cardRef, _cardProxyMesh, out Vector3 cardMin, out Vector3 cardMax))
            return;

        Vector3 cardCenter = (cardMin + cardMax) * 0.5f;
        _cardProxyMesh.localPosition = _packOutlineCenterLocal - cardCenter;
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

            PackArtLibrary.ApplyPackMaterials(_packModel, packVariantIndex);
            StripVisualColliders(_packModel);
            ApplyPackModelShadowSettings();
            return;
        }

        CreatePlaceholderPackModel();
    }

    void ConfigurePackModelFromExisting()
    {
        PackArtLibrary.ApplyPackMaterials(_packModel, packVariantIndex);
        StripVisualColliders(_packModel);
        ApplyPackModelShadowSettings();
        CaptureExistingVisualLayout();
    }

    /// <summary>
    /// Grabbit / scene packs already have a fitted mesh scale. Runtime fields start at
    /// (1,1,1); hover outline used to write that back onto the model and shrink it.
    /// </summary>
    void CaptureExistingVisualLayout()
    {
        if (_packModel == null)
            return;

        Vector3 existingScale = _packModel.localScale;
        if (existingScale.sqrMagnitude > 0.000001f)
            _visualBaseScale = existingScale;

        _packModelCenterOffset = _packModel.localPosition;
        CapturePackBodyThicknessFromCurrentPose();
        if (_cardRef != null
            && TryMeasureMeshBoundsInLocalSpace(_cardRef, _packModel, out Vector3 min, out Vector3 max))
        {
            GetFootprintAxes(max - min, out int thicknessAxis, out _, out _);
            _packNativeThicknessAxis = thicknessAxis;
            _hasPackNativeThicknessAxis = true;
        }
    }

    void CapturePackBodyThicknessFromCurrentPose()
    {
        if (_packModel == null)
            return;

        if (TryMeasureRendererBoundsInLocalSpace(transform, _packModel, out Vector3 min, out Vector3 max))
            _packBodyThickness = Mathf.Max(CardDimensions.Thickness, max.y - min.y);
    }

    void ApplyWorldVisualOrientation(bool alignPackModelToGround = true)
    {
        EnsureVisual();
        if (_cardRef == null)
            return;

        bool applyGroundPose = alignPackModelToGround && !IsPhysicsSimulating;
        _cardRef.localPosition = applyGroundPose
            ? Vector3.up * GetPackModelGroundOffsetY()
            : Vector3.zero;
        _cardRef.localScale = CardArtLibrary.WorldVisualScale;
        SetCardRefMesh(CardArtLibrary.CardMesh);
        ApplyCardRefWorldRotation(alignPackModelToGround);
        ApplyPackModelLocalTransform();
        RefreshCardProxyCenterOnPack();
        ApplyPackBodyCollider();
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
        RefreshCardProxyCenterOnPack();
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
        _cardRef.localRotation = alignPackModelToGround && !IsPhysicsSimulating
            ? GetPackWorldGroundLocalRotation(_groundShowsBack)
            : GetPackPhysicsWorldLocalRotation();
    }

    void ApplyPackModelLocalTransform()
    {
        if (_packModel == null)
            return;

        Vector3 scale = GetPackModelLocalScale();
        _packModel.localRotation = Quaternion.identity;
        _packModel.localScale = scale;

        if (!UsesHandThinPackProfile())
        {
            _packModel.localPosition = _packModelCenterOffset;
            return;
        }

        _packModel.localPosition = Vector3.zero;
        if (TryMeasureMeshBoundsInLocalSpace(_cardRef, _packModel, out Vector3 min, out Vector3 max))
            _packModel.localPosition = -(min + max) * 0.5f;
        else
            _packModel.localPosition = _packModelCenterOffset;
    }

    bool UsesHandThinPackProfile()
    {
        return _state == PackState.Held
            || _state == PackState.FlyingToHand
            || _state == PackState.Opening;
    }

    Vector3 GetPackModelLocalScale()
    {
        if (!UsesHandThinPackProfile())
            return _visualBaseScale;

        EnsurePackThicknessAxis();
        float handMultiplier = PackVisualSettings.GetHeldThicknessFitMultiplierOrDefault();
        float worldRatio = _packBodyThickness > CardDimensions.Thickness
            ? _packBodyThickness / CardDimensions.Thickness
            : PackVisualSettings.GetThicknessFitMultiplierOrDefault();
        if (worldRatio <= 0.0001f)
            return _visualBaseScale;

        Vector3 scale = _visualBaseScale;
        scale[_packNativeThicknessAxis] *= handMultiplier / worldRatio;
        return scale;
    }

    void EnsurePackThicknessAxis()
    {
        if (_hasPackNativeThicknessAxis)
            return;

        if (_cardRef == null || _packModel == null)
            return;

        if (!TryMeasureMeshBoundsInLocalSpace(_cardRef, _packModel, out Vector3 min, out Vector3 max))
            return;

        GetFootprintAxes(max - min, out int thicknessAxis, out _, out _);
        _packNativeThicknessAxis = thicknessAxis;
        _hasPackNativeThicknessAxis = true;
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
        GetFootprintAxes(nativeSize, out int nativeThickness, out _, out _);
        _packNativeThicknessAxis = nativeThickness;
        _hasPackNativeThicknessAxis = true;
        _visualBaseScale = ComputeCardFootprintScale(nativeSize, cardSize);

        _packModel.localScale = _visualBaseScale;
        ApplyPackMeshChildTuning();

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
        RefreshPackOutlineBoundsFromLayout();
    }

    void RefreshPackOutlineBoundsFromLayout()
    {
        _hasPackOutlineBounds = false;
        if (_cardRef == null || _packModel == null)
            return;

        if (!TryMeasureMeshBoundsInLocalSpace(_cardRef, _packModel, out Vector3 min, out Vector3 max))
            return;

        _packOutlineSize = max - min;
        _packOutlineCenterLocal = (min + max) * 0.5f;
        _hasPackOutlineBounds = _packOutlineSize.sqrMagnitude > 0.000001f;
        RefreshCardProxyCenterOnPack();
    }

    Vector3 GetCardMeshSizeInCardRefLocalSpace()
    {
        EnsureCardProxyMesh();
        Transform meshTransform = _cardProxyMesh != null ? _cardProxyMesh : _cardRef;
        if (_cardRef != null && meshTransform != null
            && TryMeasureSingleMeshBoundsInLocalSpace(_cardRef, meshTransform, out Vector3 min, out Vector3 max))
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
        scale[nativeThickness] = (cardSize[cardThickness] * PackVisualSettings.GetThicknessFitMultiplierOrDefault())
            / Mathf.Max(nativeSize[nativeThickness], 0.00001f);
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

    void ApplyPackMeshChildTuning()
    {
        if (_packModel == null)
            return;

        PackVisualSettings settings = PackVisualSettings.LoadOrNull();
        if (settings == null)
            return;

        Transform meshChild = FindPackMeshChildTransform(_packModel, settings.MeshChildName);
        if (meshChild == null)
            return;

        meshChild.localPosition = settings.MeshLocalPosition;
        meshChild.localRotation = Quaternion.Euler(settings.MeshLocalRotationEuler);
        meshChild.localScale = settings.MeshLocalScale;
    }

    static Transform FindPackMeshChildTransform(Transform packModelRoot, string childName)
    {
        if (packModelRoot == null)
            return null;

        if (!string.IsNullOrEmpty(childName))
        {
            Transform[] transforms = packModelRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != packModelRoot && candidate.name == childName)
                    return candidate;
            }
        }

        MeshFilter[] meshFilters = packModelRoot.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            if (meshFilter == null || meshFilter.sharedMesh == null || IsOutlineObject(meshFilter.gameObject))
                continue;

            return meshFilter.transform;
        }

        return null;
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

        // Taken from the mesh after the footprint fit and the mesh child tuning, so the collider spans the
        // body the player sees rather than the card proxy hidden inside it. Face up and face down are
        // mirror images, so both probes measure the same thickness.
        _packBodyThickness = Mathf.Max(CardDimensions.Thickness, max.y - min.y);
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

        ApplyPackModelLocalTransform();
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
        EnsureCardProxyMesh();
        if (_cardProxyMesh == null || mesh == null)
            return;

        var meshFilter = _cardProxyMesh.GetComponent<MeshFilter>();
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
            CardDimensions.Thickness * PackVisualSettings.GetThicknessFitMultiplierOrDefault());
        _packNativeThicknessAxis = 2;
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

        bool useHandMaterials = _state == PackState.Held || _state == PackState.Opening;
        if (!useHandMaterials)
            ReleaseLiveHandMaterials();

        CachePackRenderers();
        for (int i = 0; i < _packRenderers.Length; i++)
        {
            Renderer renderer = _packRenderers[i];
            if (renderer == null)
                continue;

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (useHandMaterials)
                EnsureLiveHandMaterials(renderer);
            else
                PackArtLibrary.ApplyPackMaterials(renderer, packVariantIndex, forHand: false);
        }

        ApplyPackRendererVisibility();
    }

    void EnsureLiveHandMaterials(Renderer renderer)
    {
        if (renderer == null)
            return;

        int rendererId = renderer.GetInstanceID();
        if (_liveHandMaterialsByRenderer.ContainsKey(rendererId))
            return;

        Material[] instances = PackArtLibrary.CreatePackHandMaterialInstances(renderer, packVariantIndex);
        if (instances != null)
            _liveHandMaterialsByRenderer[rendererId] = instances;
    }

    void ReleaseLiveHandMaterials()
    {
        if (_liveHandMaterialsByRenderer.Count == 0)
            return;

        foreach (Material[] instances in _liveHandMaterialsByRenderer.Values)
            PackArtLibrary.DestroyMaterialInstances(instances);

        _liveHandMaterialsByRenderer.Clear();
    }

    void OnDestroy()
    {
        ReleaseLiveHandMaterials();
    }

    void CachePackRenderers()
    {
        if (_packModel == null)
        {
            _packRenderers = System.Array.Empty<Renderer>();
            return;
        }

        Renderer[] renderers = _packModel.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            _packRenderers = System.Array.Empty<Renderer>();
            return;
        }

        int count = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && !IsOutlineRenderer(renderers[i]))
                count++;
        }

        if (count == 0)
        {
            _packRenderers = System.Array.Empty<Renderer>();
            return;
        }

        if (_packRenderers == null || _packRenderers.Length != count)
            _packRenderers = new Renderer[count];

        int write = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || IsOutlineRenderer(renderer))
                continue;

            _packRenderers[write++] = renderer;
        }
    }

    void ApplyPackRendererVisibility()
    {
        if (_packRenderers == null)
            return;

        bool show = _groundModelRenderersVisible
            || _state != PackState.World
            || HasActivePhysics
            || IsInHand
            || (_state == PackState.World && _interactionHighlighted);

        for (int i = 0; i < _packRenderers.Length; i++)
        {
            Renderer renderer = _packRenderers[i];
            if (renderer != null)
                renderer.enabled = show;
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
            return InteractPrompt.Format(Localization.Format(LocalizationKeys.PromptPickUp, PackDisplayName));

        if (!hand.CanPickUpPack)
        {
            if (hand.AvailableSlots <= 0)
                return Localization.Format(
                    LocalizationKeys.PromptHandFull,
                    CardDimensions.MaxHandSize,
                    CardDimensions.MaxHandSize);

            return Localization.Get(LocalizationKeys.PromptHandFullShort);
        }

        return InteractPrompt.Format(Localization.Format(LocalizationKeys.PromptPickUp, PackDisplayName));
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
        if (_interactionHighlighted && !_hasPackOutlineBounds)
            RefreshPackOutlineBoundsFromLayout();
        RefreshPackOutlineState();
        ApplyPackRendererVisibility();
    }

    public void SetHandSelected(bool selected)
    {
        _handSelected = selected;
        if (selected && !_hasPackOutlineBounds)
            RefreshPackOutlineBoundsFromLayout();
        RefreshPackOutlineState();
    }

    void EnsurePackOutline()
    {
        EnsurePackModelVisual();
        if (_packModel == null)
            return;

        if (_packOutline == null)
            _packOutline = _packModel.GetComponent<Outline>();
        if (_packOutline == null)
            _packOutline = _packModel.gameObject.AddComponent<Outline>();

        _packOutline.OutlineMode = Outline.Mode.OutlineAll;
        _packOutline.OutlineWidth = PackVisualSettings.GetQuickOutlineWidthOrDefault();
    }

    void RefreshPackOutlineState()
    {
        EnsurePackOutline();
        if (_packOutline == null)
            return;

        CardOutlineSettings.Palette palette = CardOutlineSettings.GetPaletteOrDefaults();

        if (_state == PackState.World && _interactionHighlighted)
        {
            _packOutline.OutlineColor = palette.cardHover;
            _packOutline.enabled = true;
            return;
        }

        if (IsHeld && _handSelected)
        {
            _packOutline.OutlineColor = palette.handSelection;
            _packOutline.enabled = true;
            return;
        }

        _packOutline.enabled = false;
    }

    void DisablePackOutline()
    {
        if (_packOutline != null)
            _packOutline.enabled = false;
        _handSelected = false;
        _interactionHighlighted = false;
    }

    public void BeginPickupFlight(
        Transform handAnchor,
        float targetHandScale,
        float duration,
        float arcHeight,
        System.Action onComplete = null)
    {
        _state = PackState.FlyingToHand;
        SetGroundModelVisible(true);
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
        CardGroundStack.UntrackPhysicsPack(this);
        RemovePhysics();

        if (_collider != null)
            _collider.enabled = false;

        transform.SetParent(null, true);
        EnsureVisual();
        ApplyHandVisualOrientation();
        ApplyPackModelShadowSettings();
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
        ApplyPackModelShadowSettings();

        System.Action callback = _onPickupFlightComplete;
        _onPickupFlightComplete = null;
        callback?.Invoke();
    }

    public IReadOnlyList<CardDefinition> PeekPreRolledContents()
    {
        return _preRolledContents;
    }

    public void RestoreIntoHand(Transform handAnchor, float targetHandScale)
    {
        BeginPickupFlight(handAnchor, targetHandScale, 0.05f, 0f);
        CompletePickupFlight();
    }

    public void ApplyHeldPose(Vector3 localPosition, Quaternion localRotation, float scale)
    {
        if (_state != PackState.Held && _state != PackState.Opening)
            return;

        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
        transform.localScale = Vector3.one * scale;
        ApplyHandVisualOrientation();
        RefreshCardProxyCenterOnPack();
    }

    public void BeginOpening()
    {
        _state = PackState.Opening;
        DisablePackOutline();
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
        SetGroundModelVisible(true);
        SetHandSelected(false);
        EnsureVisual();
        ConvertHandVisualToWorldRoot();
        transform.SetParent(null, true);

        ApplyPackBodyCollider();

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
        CardCollisionUtility.LaunchThrownBody(_rigidbody, velocity);
        if (_collider is BoxCollider thrownBox)
            CardCollisionUtility.UnstickThrownSpawnOverlap(transform, thrownBox, null, _rigidbody);

        CardLayers.ApplyToGameObject(gameObject);
        CardGroundStack.TrackPhysicsPack(this);
        StartCoroutine(MonitorThrownPackRoutine());
    }

    IEnumerator MonitorThrownPackRoutine()
    {
        var boxCollider = _collider as BoxCollider;

        yield return CardThrownPhysics.Monitor(
            transform,
            _rigidbody,
            boxCollider,
            () => _state == PackState.World && _rigidbody != null,
            onSettled: attempt => CardSettlePlacement.TrySettle(this, boxCollider, _rigidbody, attempt));

        if (_state != PackState.World || _rigidbody == null)
            yield break;

        bool alignToGround = CardSettlePlacement.IsFlatOnFloor(transform);
        ApplyWorldVisualOrientation(alignPackModelToGround: alignToGround);
        if (!alignToGround)
        {
            LiftMeshAboveFloor();
            if (boxCollider != null)
                CardCollisionUtility.ResolveRestingPenetration(transform, boxCollider, null, _rigidbody);
        }
        SetInteractionHighlight(false);
        RefreshPackOutlineState();
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
    /// Lays a thrown pack flat on the floor like an authored ground pack, keeping the landed
    /// front/back face. The pack body is thick enough to sleep standing on an edge; cards cannot,
    /// so packs need this extra flatten. Face is sampled before rewriting rotation.
    /// </summary>
    public void FlattenOntoFloor()
    {
        EnsureVisual();

        _groundShowsBack = ReadGroundShowsBackFromLandedProxy(_cardRef, transform);
        Vector3 heading = ReadGroundSettleHeading(_cardRef, transform, _groundShowsBack);

        // Yaw-only root, same as authored floor packs. Do not Z-flip the root — front/back is
        // the proxy's ground rotation (WorldVisualRotation ± 180 X), not a physics-basis trick.
        Quaternion level = Quaternion.LookRotation(heading, Vector3.up);
        transform.rotation = level;
        if (_rigidbody != null)
        {
            _rigidbody.rotation = level;
            if (!_rigidbody.isKinematic)
                _rigidbody.angularVelocity = Vector3.zero;
        }

        ApplySettledGroundVisual();
        CardGroundStack.ApplyStackHeight(this, placeOnTop: true);

        if (_collider != null)
        {
            _collider.isTrigger = false;
            _collider.enabled = true;
        }
    }

    /// <summary>
    /// Ground visual while the thrown body may still be dynamic for this settle frame.
    /// </summary>
    void ApplySettledGroundVisual()
    {
        EnsureVisual();
        if (_cardRef == null)
            return;

        _cardRef.localPosition = Vector3.up * GetPackModelGroundOffsetY();
        _cardRef.localScale = CardArtLibrary.WorldVisualScale;
        SetCardRefMesh(CardArtLibrary.CardMesh);
        _cardRef.localRotation = GetPackWorldGroundLocalRotation(_groundShowsBack);
        ApplyPackModelLocalTransform();
        RefreshCardProxyCenterOnPack();
        ApplyPackBodyCollider();
        ApplyPackModelShadowSettings();
    }

    static List<CardDefinition> _cachedDefaultPool;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetPackContentCache()
    {
        _cachedDefaultPool = null;
    }

    public IReadOnlyList<CardDefinition> RollContents(int count)
    {
        EnsureContentsPreRolled(count);
        if (_preRolledContents == null || _preRolledContents.Count == 0)
            return new List<CardDefinition>();

        int take = Mathf.Min(count, _preRolledContents.Count);
        return _preRolledContents.GetRange(0, take);
    }

    public void EnsureContentsPreRolled(int count = CardDimensions.CardsPerBoosterPack)
    {
        if (_preRolledContents != null && _preRolledContents.Count >= count)
            return;

        var results = new List<CardDefinition>(count);
        IReadOnlyList<CardDefinition> pool = packDefinition != null
            ? packDefinition.BuildCardPool()
            : GetDefaultPool();

        if (pool.Count == 0)
        {
            Debug.LogWarning("WorldBoosterPack: No card definitions available for pack contents.");
            _preRolledContents = results;
            return;
        }

        for (int i = 0; i < count; i++)
            results.Add(pool[Random.Range(0, pool.Count)]);

        _preRolledContents = results;
    }

    static IReadOnlyList<CardDefinition> GetDefaultPool()
    {
        if (_cachedDefaultPool != null)
            return _cachedDefaultPool;

        CardCatalog.EnsureLoaded();
        IReadOnlyList<CardDefinition> all = CardCatalog.All;
        _cachedDefaultPool = new List<CardDefinition>(all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            CardDefinition definition = all[i];
            if (definition == null)
                continue;
            if (!CardScatterUtility.IsLiveGroundCategory(definition.ShelfCategoryId))
                continue;
            _cachedDefaultPool.Add(definition);
        }

        return _cachedDefaultPool;
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

    /// <summary>
    /// Root lift, over the flat stack height, that rests the pack body's underside on the stack surface.
    /// The ground pose already lifts the mesh onto the card proxy bottom (lift 0), while the physics pose
    /// centres the body on the root, so there the root has to clear half a pack instead of half a card.
    /// Without this the settle snap pulled a landed pack back down into the floor.
    /// </summary>
    public float GroundRestLift
    {
        get
        {
            if (_cardRef == null)
                return 0f;

            float halfBody = Mathf.Max(CardDimensions.Thickness, _packBodyThickness) * 0.5f;
            float halfCard = CardDimensions.Thickness * 0.5f;
            return (halfBody - halfCard - _cardRef.localPosition.y) * CardDimensions.GroundCardScale;
        }
    }

    /// <summary>
    /// Sizes the collider to the visible pack mesh in root space, like PSA slabs. A card-footprint
    /// box let a leaning pack sleep against a wall while the model clipped through it.
    /// </summary>
    void ApplyPackBodyCollider()
    {
        if (_cardRef == null || !(_collider is BoxCollider boxCollider))
            return;

        if (TryFitColliderToPackMesh(boxCollider))
            return;

        boxCollider.center = new Vector3(0f, _cardRef.localPosition.y, 0f);
        boxCollider.size = new Vector3(
            CardDimensions.Width,
            Mathf.Max(CardDimensions.Thickness, _packBodyThickness),
            CardDimensions.Height);
        CardCollisionUtility.ApplyToCollider(boxCollider);
    }

    bool TryFitColliderToPackMesh(BoxCollider boxCollider)
    {
        if (_packModel == null)
            return false;

        if (!TryMeasureMeshBoundsInLocalSpace(transform, _packModel, out Vector3 min, out Vector3 max)
            && !TryMeasureRendererBoundsInLocalSpace(transform, _packModel, out min, out max))
            return false;

        Vector3 size = max - min;
        float maxAxis = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float maxAllowed = Mathf.Max(CardDimensions.Width, CardDimensions.Height) * 2.5f;
        if (maxAxis <= 0.001f || maxAxis > maxAllowed)
            return false;

        const float minSize = 0.012f;
        size.x = Mathf.Max(size.x, minSize);
        size.y = Mathf.Max(size.y, minSize);
        size.z = Mathf.Max(size.z, minSize);
        boxCollider.center = (min + max) * 0.5f;
        boxCollider.size = size;
        CardCollisionUtility.ApplyToCollider(boxCollider);
        _packBodyThickness = Mathf.Max(CardDimensions.Thickness, size.y);
        return true;
    }

    void EnsureRigidbody()
    {
        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody>();

        if (_rigidbody != null)
            return;

        _rigidbody = gameObject.AddComponent<Rigidbody>();
        CardCollisionUtility.ConfigureThrownBody(_rigidbody);
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
        CardGroundStack.UntrackPhysicsPack(this);
    }

    void IgnorePlayerCollision()
    {
        CardCollisionUtility.IgnorePlayerCollision(_collider);
    }
}
