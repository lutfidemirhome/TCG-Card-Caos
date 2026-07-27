using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A card lying in the world. Press E to pick up into the right hand.
/// Static world cards render through <see cref="CardInstancedRenderManager"/>; visuals/outlines spawn lazily.
/// </summary>
public class WorldCard : MonoBehaviour, IInteractable, IInteractionHighlight
{
    enum HandState
    {
        World,
        FlyingToHand,
        Held,
    }

    [SerializeField] string cardLabel = "Pick Up";
    [SerializeField] int cardDefinitionId;
    [SerializeField] int paletteIndex;

    Collider _collider;
    Rigidbody _rigidbody;
    Transform _cardVisual;
    GameObject _outlineObject;
    GameObject _handSelectionOutlineObject;
    HandState _handState = HandState.World;
    Transform _handAnchor;
    bool _interactionHighlighted;
    float _flightStartWorldScale = 1f;
    float _flightTargetHandScale = 1f;
    float _flightElapsed;
    float _flightDuration;
    Vector3 _flightStartWorldPos;
    Quaternion _flightStartWorldRot;
    float _flightArcHeight;
    System.Action _onPickupFlightComplete;
    float _scaleFrom = 1f;
    float _scaleTo = 1f;
    float _scaleTransitionElapsed;
    float _scaleTransitionDuration;
    bool _scaleTransitionActive;

    public bool IsHeld => _handState == HandState.Held;
    public bool IsFlyingToHand => _handState == HandState.FlyingToHand;
    public bool IsInHand => _handState != HandState.World;
    public int CardDefinitionId => cardDefinitionId;
    public int PaletteIndex => paletteIndex;

    public bool CanUseInstancedRendering =>
        Application.isPlaying
        && _handState == HandState.World
        && !_interactionHighlighted
        && !_scaleTransitionActive
        && _rigidbody == null
        && _cardVisual == null;

    public void Initialize(int definitionId, int palette)
    {
        cardDefinitionId = definitionId;
        paletteIndex = palette;
    }

    void Awake()
    {
        _collider = GetComponent<Collider>();

        Transform existingVisual = transform.Find("CardVisual");
        if (existingVisual != null)
            _cardVisual = existingVisual;
    }

    void OnEnable()
    {
        TryRegisterInstancedRendering();
    }

    void OnDisable()
    {
        CardInstancedRenderManager.Instance?.Unregister(this);
    }

    void OnDestroy()
    {
        CardInstancedRenderManager.Instance?.Unregister(this);
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

    System.Collections.IEnumerator ScaleTransitionRoutine()
    {
        while (_scaleTransitionActive
               && _handState != HandState.FlyingToHand
               && _handState != HandState.Held)
        {
            _scaleTransitionElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_scaleTransitionElapsed / _scaleTransitionDuration);
            float smoothT = t * t * (3f - 2f * t);
            transform.localScale = Vector3.one * Mathf.Lerp(_scaleFrom, _scaleTo, smoothT);

            if (t >= 1f)
            {
                _scaleTransitionActive = false;
                RefreshRenderMode();
                yield break;
            }

            yield return null;
        }
    }

    public Matrix4x4 GetInstancedDrawMatrix()
    {
        Vector3 scale = transform.lossyScale;
        Quaternion visualRotation = transform.rotation * CardArtLibrary.WorldVisualRotation;
        return Matrix4x4.TRS(transform.position, visualRotation, scale);
    }

    public Bounds GetInstancedCullBounds()
    {
        float scale = Mathf.Max(transform.lossyScale.x, 0.01f);
        Vector3 size = new Vector3(
            CardDimensions.Width * scale,
            CardDimensions.Thickness * scale * 4f,
            CardDimensions.Height * scale);
        return new Bounds(transform.position, size);
    }

    public void SetWorldColliderEnabled(bool enabled)
    {
        if (_collider == null || !CanUseInstancedRendering)
            return;

        _collider.enabled = enabled;
    }

    public void SetInteractionHighlight(bool highlighted)
    {
        _interactionHighlighted = highlighted && !IsInHand;
        RefreshRenderMode();
    }

    public string GetPromptText()
    {
        if (IsInHand)
            return string.Empty;

        PlayerCardHand hand = Object.FindFirstObjectByType<PlayerCardHand>();
        if (hand != null && hand.IsFull)
            return "Hand Full (" + CardDimensions.MaxHandSize + "/" + CardDimensions.MaxHandSize + ")";

        return "Press [E] To " + cardLabel;
    }

    public void Interact(GameObject interactor)
    {
        if (IsInHand)
            return;

        PlayerCardHand hand = interactor.GetComponent<PlayerCardHand>();
        if (hand == null)
            hand = interactor.GetComponentInChildren<PlayerCardHand>();

        if (hand == null)
            return;

        hand.TryPickup(this);
    }

    public void BeginPickupFlight(
        Transform handAnchor,
        float targetHandScale,
        float duration,
        float arcHeight,
        System.Action onComplete = null)
    {
        _handState = HandState.FlyingToHand;
        _handAnchor = handAnchor;
        _flightTargetHandScale = targetHandScale;
        _flightDuration = Mathf.Max(0.05f, duration);
        _flightElapsed = 0f;
        _flightStartWorldPos = transform.position;
        _flightStartWorldRot = transform.rotation;
        _flightStartWorldScale = transform.localScale.x;
        _flightArcHeight = arcHeight;
        _onPickupFlightComplete = onComplete;

        SetInteractionHighlight(false);
        SetHandSelected(false);
        RemovePhysics();

        if (_collider != null)
            _collider.enabled = false;

        transform.SetParent(null, true);
        EnsureCardVisual();
        SetVisualRotation(CardArtLibrary.HandVisualRotation);
        RefreshRenderMode();
    }

    public void UpdatePickupFlight(Vector3 targetWorldPos, Quaternion targetWorldRot)
    {
        if (_handState != HandState.FlyingToHand)
            return;

        _flightElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_flightElapsed / _flightDuration);
        float smoothT = t * t * (3f - 2f * t);

        Vector3 pos = Vector3.Lerp(_flightStartWorldPos, targetWorldPos, smoothT);
        pos += Vector3.up * (Mathf.Sin(smoothT * Mathf.PI) * _flightArcHeight);

        transform.SetPositionAndRotation(pos, Quaternion.Slerp(_flightStartWorldRot, targetWorldRot, smoothT));
        float scale = Mathf.Lerp(_flightStartWorldScale, _flightTargetHandScale, smoothT);
        transform.localScale = Vector3.one * scale;

        if (t >= 1f)
            CompletePickupFlight();
    }

    void CompletePickupFlight()
    {
        _handState = HandState.Held;
        transform.SetParent(_handAnchor, false);
        EnsureCardVisual();
        SetVisualRotation(CardArtLibrary.HandVisualRotation);
        RefreshRenderMode();

        System.Action callback = _onPickupFlightComplete;
        _onPickupFlightComplete = null;
        callback?.Invoke();
    }

    public void DropWithPhysics(Vector3 velocity, float worldScaleTransitionDuration = 0.12f)
    {
        _handState = HandState.World;
        SetHandSelected(false);
        EnsureCardVisual();
        SetVisualRotation(CardArtLibrary.WorldVisualRotation);
        transform.SetParent(null, true);

        if (_collider != null)
            _collider.enabled = true;

        BeginScaleTransition(transform.localScale.x, CardDimensions.WorldCardScale, worldScaleTransitionDuration);
        RefreshRenderMode();

        EnsureRigidbody();
        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = true;

        if (!_rigidbody.isKinematic)
        {
            _rigidbody.linearVelocity = velocity;
            _rigidbody.angularVelocity = new Vector3(
                Random.Range(-3f, 3f),
                Random.Range(-1f, 1f),
                Random.Range(-3f, 3f));
        }

        StartCoroutine(SettleDroppedCardRoutine());
    }

    System.Collections.IEnumerator SettleDroppedCardRoutine()
    {
        while (_handState == HandState.World
               && !_scaleTransitionActive
               && !_interactionHighlighted
               && _rigidbody != null
               && !_rigidbody.IsSleeping())
        {
            yield return null;
        }

        if (_handState != HandState.World || _scaleTransitionActive || _interactionHighlighted || _rigidbody == null)
            yield break;

        RemovePhysics();
        ReleaseCardVisual();
        RefreshRenderMode();
    }

    void RemovePhysics()
    {
        Rigidbody rb = _rigidbody != null ? _rigidbody : GetComponent<Rigidbody>();
        if (rb == null)
        {
            _rigidbody = null;
            return;
        }

        if (Application.isPlaying)
            Destroy(rb);
        else
            DestroyImmediate(rb);
        _rigidbody = null;
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
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    public void ApplyFanPose(int fanIndex, int fanCount, in HandFanLayoutSettings layout, bool isSelected)
    {
        if (_handState != HandState.Held)
            return;

        EnsureCardVisual();
        HandCardPose pose = HandFanLayout.GetPose(fanIndex, fanCount, layout, isSelected);
        transform.localPosition = pose.LocalPosition;
        transform.localRotation = pose.LocalRotation;
        transform.localScale = Vector3.one * pose.Scale;
        SetVisualRotation(CardArtLibrary.HandVisualRotation);
        SetHandSelected(isSelected);
    }

    void SetVisualRotation(Quaternion localRotation)
    {
        if (_cardVisual != null)
            _cardVisual.localRotation = localRotation;
    }

    public void SetHandSelected(bool selected)
    {
        if (selected)
            EnsureHandSelectionOutlineRenderer();
        else
            ReleaseHandSelectionOutline();

        if (_handSelectionOutlineObject != null)
            _handSelectionOutlineObject.SetActive(selected && IsHeld);
    }

    void RefreshRenderMode()
    {
        CardInstancedRenderManager.Instance?.Unregister(this);

        if (CanUseInstancedRendering)
        {
            ReleaseCardVisual();
            ReleaseInteractionOutline();
            CardInstancedRenderManager.Instance?.Register(this);
            return;
        }

        EnsureCardVisual();

        if (_interactionHighlighted)
        {
            EnsureInteractionOutlineRenderer();
            if (_outlineObject != null)
                _outlineObject.SetActive(true);
        }
        else
        {
            ReleaseInteractionOutline();
        }
    }

    void TryRegisterInstancedRendering()
    {
        CardInstancedRenderManager.EnsureExists();
        RefreshRenderMode();
    }

    void EnsureCardVisual()
    {
        if (_cardVisual != null)
            return;

        CardArtLibrary.EnsureLoaded();

        var visualGo = new GameObject("CardVisual");
        visualGo.transform.SetParent(transform, false);
        visualGo.transform.localRotation = CardArtLibrary.WorldVisualRotation;

        var meshFilter = visualGo.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CardArtLibrary.CardMesh;

        var meshRenderer = visualGo.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterials = CardArtLibrary.GetCardMaterials(paletteIndex);
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        _cardVisual = visualGo.transform;
    }

    void ReleaseCardVisual()
    {
        ReleaseInteractionOutline();
        ReleaseHandSelectionOutline();

        if (_cardVisual == null)
            return;

        if (Application.isPlaying)
            Destroy(_cardVisual.gameObject);
        else
            DestroyImmediate(_cardVisual.gameObject);

        _cardVisual = null;
    }

    Transform GetOutlineParent()
    {
        EnsureCardVisual();
        return _cardVisual != null ? _cardVisual : transform;
    }

    void EnsureInteractionOutlineRenderer()
    {
        if (_outlineObject != null)
            return;

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
            return;

        _handSelectionOutlineObject = new GameObject("HandSelectionOutline");
        _handSelectionOutlineObject.transform.SetParent(GetOutlineParent(), false);

        var meshFilter = _handSelectionOutlineObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CardVisualResources.HandSelectionBorderFrameMesh;

        var meshRenderer = _handSelectionOutlineObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = CardVisualResources.HandSelectionOutlineMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
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

    public void SetWorldPose(Vector3 position, Quaternion rotation)
    {
        _handState = HandState.World;
        SetInteractionHighlight(false);
        SetHandSelected(false);
        RemovePhysics();

        if (_collider != null)
            _collider.enabled = true;

        transform.SetParent(null, true);
        transform.SetPositionAndRotation(position, rotation);
        _scaleTransitionActive = false;
        transform.localScale = Vector3.one * CardDimensions.WorldCardScale;
        RefreshRenderMode();
    }
}
