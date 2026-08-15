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

    // Tuned pack mesh locals relative to PackCardRef (card already supplies World/Hand rotation).
    static readonly Vector3 DefaultPackModelLocalPosition = Vector3.zero;
    static readonly Vector3 DefaultPackModelLocalScale = new Vector3(1.779564f, 1.3624f, 1.3624f);

    PackState _state = PackState.World;
    Transform _cardRef;
    Transform _packModel;
    Transform _handAnchor;
    Rigidbody _rigidbody;
    BoxCollider _collider;
    bool _interactionHighlighted;
    bool _usesTunedDefaultPackVisual;
    bool _groundShowsBack;
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

    public void SetGroundStackLayer(int layer)
    {
        _groundStackLayer = Mathf.Max(0, layer);
    }

    public void Initialize(BoosterPackDefinition definition)
    {
        packDefinition = definition;
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

            _usesTunedDefaultPackVisual = visualPrefab == null;
            _visualBaseScale = _usesTunedDefaultPackVisual
                ? DefaultPackModelLocalScale
                : Vector3.one;

            PackArtLibrary.ApplyPackMaterials(_packModel);
            StripVisualColliders(_packModel);
            ApplyPackModelShadowSettings();
            return;
        }

        CreatePlaceholderPackModel();
    }

    void ConfigurePackModelFromExisting()
    {
        _usesTunedDefaultPackVisual = visualPrefab == null;
        _visualBaseScale = _usesTunedDefaultPackVisual
            ? DefaultPackModelLocalScale
            : _packModel.localScale;

        PackArtLibrary.ApplyPackMaterials(_packModel);
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
        _cardRef.localRotation = CardArtLibrary.HandVisualRotation;
        _cardRef.localScale = CardArtLibrary.HandVisualScale;
        SetCardRefMesh(CardArtLibrary.HandCardMesh);
        ApplyPackModelLocalTransform();
    }

    void ApplyCardRefWorldRotation(bool alignPackModelToGround)
    {
        Quaternion rotation = CardArtLibrary.WorldVisualRotation;
        if (alignPackModelToGround && !HasActivePhysics && _groundShowsBack)
            rotation *= Quaternion.Euler(180f, 0f, 0f);

        _cardRef.localRotation = rotation;
    }

    void ApplyPackModelLocalTransform()
    {
        if (_packModel == null)
            return;

        Vector3 basePosition = _usesTunedDefaultPackVisual
            ? DefaultPackModelLocalPosition
            : Vector3.zero;
        _packModel.localPosition = basePosition + _packModelCenterOffset;
        _packModel.localRotation = Quaternion.identity;
        _packModel.localScale = _usesTunedDefaultPackVisual
            ? DefaultPackModelLocalScale
            : _visualBaseScale;
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
        ApplyPackModelProbePose();

        if (TryMeasureRendererBoundsInLocalSpace(_cardRef, _packModel, out Vector3 min, out Vector3 max))
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

    void ApplyCardRefWorldPose(bool faceDown = false)
    {
        _cardRef.localPosition = Vector3.zero;
        _cardRef.localScale = CardArtLibrary.WorldVisualScale;

        Quaternion rotation = CardArtLibrary.WorldVisualRotation;
        if (faceDown)
            rotation *= Quaternion.Euler(180f, 0f, 0f);

        _cardRef.localRotation = rotation;
    }

    void ApplyPackModelProbePose()
    {
        if (_packModel == null)
            return;

        _packModel.localPosition = (_usesTunedDefaultPackVisual
                ? DefaultPackModelLocalPosition
                : Vector3.zero)
            + _packModelCenterOffset;
        _packModel.localRotation = Quaternion.identity;
        _packModel.localScale = _usesTunedDefaultPackVisual
            ? DefaultPackModelLocalScale
            : _visualBaseScale;
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

    bool TryMeasurePackRendererBoundsInRoot(out Vector3 min, out Vector3 max)
    {
        return TryMeasureRendererBoundsInLocalSpace(transform, _packModel, out min, out max);
    }

    static bool IsOutlineRenderer(Renderer renderer)
    {
        if (renderer == null)
            return false;

        string objectName = renderer.gameObject.name;
        return objectName == "InteractionOutline" || objectName == "HandSelectionOutline";
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
        _usesTunedDefaultPackVisual = false;
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
                PackArtLibrary.ApplyPackMaterials(renderer);
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
            return "Press [E] To Pick Up Pack";

        if (!hand.CanPickUpPack)
        {
            if (hand.AvailableSlots <= 0)
                return "Hand Full (" + CardDimensions.MaxHandSize + "/" + CardDimensions.MaxHandSize + ")";

            return "Hand Full";
        }

        return "Press [E] To Pick Up Pack";
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

    void EnsureInteractionOutlineRenderer()
    {
        if (_outlineObject != null)
        {
            EnsureOutlineParent(_outlineObject.transform);
            return;
        }

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
        if (_handSelectionOutlineObject != null)
        {
            EnsureOutlineParent(_handSelectionOutlineObject.transform);
            return;
        }

        _handSelectionOutlineObject = new GameObject("HandSelectionOutline");
        _handSelectionOutlineObject.transform.SetParent(GetOutlineParent(), false);

        var meshFilter = _handSelectionOutlineObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CardVisualResources.HandSelectionBorderFrameMesh;

        var meshRenderer = _handSelectionOutlineObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = CardVisualResources.HandSelectionOutlineMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    Transform GetOutlineParent()
    {
        EnsureVisual();
        return _cardRef != null ? _cardRef : transform;
    }

    void EnsureOutlineParent(Transform outlineTransform)
    {
        Transform parent = GetOutlineParent();
        if (outlineTransform.parent != parent)
            outlineTransform.SetParent(parent, false);
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
        _flightStartWorldPos = transform.position;
        _flightStartWorldRot = transform.rotation;
        _flightStartWorldScale = transform.localScale.x;
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

        transform.rotation = _cardRef.rotation * Quaternion.Inverse(CardArtLibrary.WorldVisualRotation);
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

    void FlattenAndSnapToGround()
    {
        EnsureVisual();
        ApplyFlatWorldCollider();

        bool frontFaceUp = _cardRef != null && _cardRef.forward.y >= 0f;

        Vector3 heading = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (heading.sqrMagnitude < 0.0001f)
            heading = Vector3.ProjectOnPlane(transform.right, Vector3.up);
        if (heading.sqrMagnitude < 0.0001f)
            heading = Vector3.forward;
        heading.Normalize();

        transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
        _groundShowsBack = !frontFaceUp;
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
