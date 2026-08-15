using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pickupable booster pack on the ground or held in the player's hand.
/// Assign a child visual prefab in the inspector once the pack model is imported.
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
    [Tooltip("Optional imported pack model prefab. Card-sized, authored flat like a ground card.")]
    [SerializeField] GameObject visualPrefab;
    [SerializeField] Color placeholderColor = new Color(0.72f, 0.58f, 0.18f, 1f);

    PackState _state = PackState.World;
    Transform _visualRoot;
    Transform _handAnchor;
    Rigidbody _rigidbody;
    BoxCollider _collider;
    bool _interactionHighlighted;
    Vector3 _visualBaseScale = Vector3.one;
    GameObject _outlineObject;
    GameObject _handSelectionOutlineObject;
    int _groundStackLayer;
    bool _scaleTransitionActive;
    float _scaleFrom;
    float _scaleTo;
    float _scaleTransitionDuration;
    float _scaleTransitionElapsed;

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
        ApplyWorldOrientation();
        ApplyPackVisualShadowSettings();
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
        if (_visualRoot != null)
            return;

        if (visualPrefab != null)
        {
            var instance = Instantiate(visualPrefab, transform);
            instance.name = "PackVisual";
            _visualRoot = instance.transform;
            _visualRoot.localPosition = Vector3.zero;
            _visualRoot.localRotation = CardArtLibrary.WorldVisualRotation;
            _visualBaseScale = _visualRoot.localScale;
            ApplyPackVisualShadowSettings();
            return;
        }

        var visualGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visualGo.name = "PackVisual";
        visualGo.transform.SetParent(transform, false);
        // Authored upright like the card mesh; WorldVisualRotation lays it flat on the floor.
        _visualBaseScale = new Vector3(
            CardDimensions.Width * 0.94f,
            CardDimensions.Height * 0.98f,
            CardDimensions.Thickness * 2.5f);
        visualGo.transform.localScale = _visualBaseScale;

        var renderer = visualGo.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = CreatePlaceholderMaterial();
        }

        var visualCollider = visualGo.GetComponent<Collider>();
        if (visualCollider != null)
            Destroy(visualCollider);

        _visualRoot = visualGo.transform;
        ApplyPackVisualShadowSettings();
    }

    void ApplyPackVisualShadowSettings()
    {
        if (_visualRoot == null)
            return;

        var renderers = _visualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (_state == PackState.World)
                CardArtLibrary.ApplyGroundWorldRendererSettings(renderer);
        }
    }

    Material CreatePlaceholderMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader);
        material.color = placeholderColor;
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

        if (hand.HasHeldPack)
            return "Already Holding A Pack";

        if (!hand.CanPickUpPack)
            return "Need " + CardDimensions.CardsPerBoosterPack + " Free Hand Slots To Pick Up Pack";

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
        {
            EnsureHandSelectionOutlineRenderer();
            SyncOutlineTransforms();
        }
        else if (_handSelectionOutlineObject != null)
        {
            _handSelectionOutlineObject.SetActive(false);
        }

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
            SyncOutlineTransforms();
            if (_outlineObject != null)
                _outlineObject.SetActive(true);
            return;
        }

        ReleaseInteractionOutline();
    }

    void EnsureInteractionOutlineRenderer()
    {
        if (_outlineObject != null)
            return;

        _ = CardVisualResources.InteractionOutlineMaterial;
        _outlineObject = new GameObject("InteractionOutline");
        _outlineObject.transform.SetParent(transform, false);
        SetupOutlineRenderer(
            _outlineObject,
            CardVisualResources.InteractionBorderFrameMesh,
            CardVisualResources.InteractionOutlineMaterial);
    }

    void EnsureHandSelectionOutlineRenderer()
    {
        if (_handSelectionOutlineObject != null)
            return;

        _ = CardVisualResources.HandSelectionOutlineMaterial;
        _handSelectionOutlineObject = new GameObject("HandSelectionOutline");
        _handSelectionOutlineObject.transform.SetParent(transform, false);
        SetupOutlineRenderer(
            _handSelectionOutlineObject,
            CardVisualResources.HandSelectionBorderFrameMesh,
            CardVisualResources.HandSelectionOutlineMaterial);
    }

    static void SetupOutlineRenderer(GameObject outlineObject, Mesh mesh, Material material)
    {
        var meshFilter = outlineObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        var meshRenderer = outlineObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    void SyncOutlineTransforms()
    {
        ApplyOutlineLocalTransform(_outlineObject != null ? _outlineObject.transform : null);
        ApplyOutlineLocalTransform(
            _handSelectionOutlineObject != null ? _handSelectionOutlineObject.transform : null);
    }

    void ApplyOutlineLocalTransform(Transform outlineTransform)
    {
        if (outlineTransform == null)
            return;

        if (_state == PackState.Held || _state == PackState.FlyingToHand || _state == PackState.Opening)
        {
            outlineTransform.localRotation = CardArtLibrary.HandVisualRotation;
            outlineTransform.localScale = CardArtLibrary.HandVisualScale;
            outlineTransform.localPosition = Vector3.zero;
            return;
        }

        outlineTransform.localRotation = CardArtLibrary.WorldVisualRotation;
        outlineTransform.localScale = CardArtLibrary.WorldVisualScale;
        outlineTransform.localPosition = Vector3.up * GetOutlineLift();
    }

    float GetOutlineLift()
    {
        float halfThickness = CardDimensions.Thickness * transform.localScale.x * 0.5f;
        return halfThickness + 0.00025f;
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
        SyncOutlineTransforms();
    }

    public void BeginOpening()
    {
        _state = PackState.Opening;
        transform.SetParent(null, true);
    }

    public void DropWithPhysics(Vector3 velocity, float worldScaleTransitionDuration = 0.12f)
    {
        _state = PackState.World;
        SetHandSelected(false);
        EnsureVisual();
        ConvertHandVisualToWorldRoot();
        ApplyPackVisualShadowSettings();
        transform.SetParent(null, true);
        ApplyFlatWorldCollider();

        if (_collider != null)
        {
            _collider.isTrigger = false;
            _collider.enabled = true;
        }

        IgnorePlayerCollision();
        BeginScaleTransition(transform.localScale.x, CardDimensions.GroundCardScale, worldScaleTransitionDuration);

        EnsureRigidbody();
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = true;
        _rigidbody.constraints = RigidbodyConstraints.None;
        _rigidbody.linearVelocity = velocity;
        _rigidbody.angularVelocity = new Vector3(
            Random.Range(-0.2f, 0.2f),
            Random.Range(-0.35f, 0.35f),
            Random.Range(-0.2f, 0.2f));

        CardGroundStack.EnableLandingCollidersNear(transform.position, 2.5f);
        StartCoroutine(SettleDroppedPackRoutine());
    }

    void ConvertHandVisualToWorldRoot()
    {
        EnsureVisual();
        if (_visualRoot == null)
            return;

        transform.rotation = _visualRoot.rotation * Quaternion.Inverse(CardArtLibrary.WorldVisualRotation);
        ApplyWorldOrientation();
    }

    void BeginScaleTransition(float fromScale, float toScale, float duration)
    {
        _scaleFrom = fromScale;
        _scaleTo = toScale;
        _scaleTransitionDuration = Mathf.Max(0.01f, duration);
        _scaleTransitionElapsed = 0f;
        _scaleTransitionActive = true;
        transform.localScale = Vector3.one * fromScale;
        StartCoroutine(ScaleTransitionRoutine());
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
                yield break;
            }

            yield return null;
        }

        _scaleTransitionActive = false;
    }

    IEnumerator SettleDroppedPackRoutine()
    {
        float groundedTime = 0f;
        float elapsed = 0f;
        float colliderRefreshTimer = 0f;
        const float settleAfterGrounded = 0.18f;
        const float forceSettleAfterGrounded = 0.55f;
        const float maxFlightTime = 4f;

        try
        {
            while (_state == PackState.World && _rigidbody != null)
            {
                elapsed += Time.deltaTime;
                colliderRefreshTimer += Time.deltaTime;
                if (colliderRefreshTimer >= 0.08f)
                {
                    colliderRefreshTimer = 0f;
                    CardGroundStack.EnableLandingCollidersNear(transform.position, 1.75f);
                }

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

        Vector3 heading = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (heading.sqrMagnitude < 0.0001f)
            heading = Vector3.ProjectOnPlane(transform.right, Vector3.up);
        if (heading.sqrMagnitude < 0.0001f)
            heading = Vector3.forward;
        heading.Normalize();

        transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
        ApplyWorldOrientation();
        ApplyPackVisualShadowSettings();
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

    void ApplyWorldOrientation()
    {
        if (_visualRoot != null)
        {
            _visualRoot.localRotation = CardArtLibrary.WorldVisualRotation;
            _visualRoot.localScale = Vector3.Scale(_visualBaseScale, CardArtLibrary.WorldVisualScale);
        }

        SyncOutlineTransforms();
    }

    void ApplyHandVisualOrientation()
    {
        if (_visualRoot == null)
            return;

        _visualRoot.localRotation = CardArtLibrary.HandVisualRotation;
        _visualRoot.localScale = Vector3.Scale(_visualBaseScale, CardArtLibrary.HandVisualScale);
        SyncOutlineTransforms();
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

        if (_rigidbody == null)
            _rigidbody = gameObject.AddComponent<Rigidbody>();

        _rigidbody.mass = 0.08f;
        _rigidbody.linearDamping = 0.4f;
        _rigidbody.angularDamping = 0.8f;
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void RemovePhysics()
    {
        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody>();

        if (_rigidbody == null)
            return;

        DestroyImmediate(_rigidbody);
        _rigidbody = null;
    }

    void IgnorePlayerCollision()
    {
        if (_collider == null)
            return;

        FirstPersonController player = FindFirstObjectByType<FirstPersonController>();
        if (player == null)
            return;

        Collider[] playerColliders = player.GetComponentsInChildren<Collider>();
        for (int i = 0; i < playerColliders.Length; i++)
        {
            if (playerColliders[i] != null)
                Physics.IgnoreCollision(_collider, playerColliders[i], true);
        }
    }
}
