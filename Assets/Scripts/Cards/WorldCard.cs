using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A card lying in the world. Press E to pick up into the right hand.
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
    GameObject _outlineObject;
    GameObject _handSelectionOutlineObject;
    HandState _handState = HandState.World;
    Transform _handAnchor;
    float _flightStartWorldScale = 1f;
    float _flightTargetHandScale = 1f;
    float _flightElapsed;
    float _flightDuration;
    Vector3 _flightStartWorldPos;
    Quaternion _flightStartWorldRot;
    float _flightArcHeight;
    System.Action _onPickupFlightComplete;

    public bool IsHeld => _handState == HandState.Held;
    public bool IsFlyingToHand => _handState == HandState.FlyingToHand;
    public bool IsInHand => _handState != HandState.World;
    public int CardDefinitionId => cardDefinitionId;

    public void Initialize(int definitionId, int palette)
    {
        cardDefinitionId = definitionId;
        paletteIndex = palette;
    }

    void Awake()
    {
        _collider = GetComponent<Collider>();
        EnsureOutlineRenderer();
        EnsureHandSelectionOutlineRenderer();
    }

    public void SetInteractionHighlight(bool highlighted)
    {
        if (_outlineObject != null)
            _outlineObject.SetActive(highlighted && !IsInHand);
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

        System.Action callback = _onPickupFlightComplete;
        _onPickupFlightComplete = null;
        callback?.Invoke();
    }

    public void DropWithPhysics(Vector3 velocity)
    {
        _handState = HandState.World;
        SetHandSelected(false);
        transform.SetParent(null, true);
        transform.localScale = Vector3.one;

        if (_collider != null)
            _collider.enabled = true;

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

        HandCardPose pose = HandFanLayout.GetPose(fanIndex, fanCount, layout, isSelected);
        transform.localPosition = pose.LocalPosition;
        transform.localRotation = pose.LocalRotation;
        transform.localScale = Vector3.one * pose.Scale;
        SetHandSelected(isSelected);
    }

    public void SetHandSelected(bool selected)
    {
        if (_handSelectionOutlineObject != null)
            _handSelectionOutlineObject.SetActive(selected && IsHeld);
    }

    void EnsureOutlineRenderer()
    {
        if (_outlineObject != null)
            return;

        _outlineObject = new GameObject("InteractionOutline");
        _outlineObject.transform.SetParent(transform, false);

        var meshFilter = _outlineObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CardVisualResources.InteractionBorderFrameMesh;

        var meshRenderer = _outlineObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = CardVisualResources.InteractionOutlineMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        _outlineObject.SetActive(false);
    }

    void EnsureHandSelectionOutlineRenderer()
    {
        if (_handSelectionOutlineObject != null)
            return;

        _handSelectionOutlineObject = new GameObject("HandSelectionOutline");
        _handSelectionOutlineObject.transform.SetParent(transform, false);

        var meshFilter = _handSelectionOutlineObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CardVisualResources.HandSelectionBorderFrameMesh;

        var meshRenderer = _handSelectionOutlineObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = CardVisualResources.HandSelectionOutlineMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        _handSelectionOutlineObject.SetActive(false);
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
        transform.localScale = Vector3.one;
    }
}
