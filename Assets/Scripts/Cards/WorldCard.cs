using UnityEngine;

/// <summary>
/// A card lying in the world. Press E to pick up into the right hand.
/// </summary>
public class WorldCard : MonoBehaviour, IInteractable
{
    [SerializeField] string cardLabel = "Pick Up";

    Collider _collider;
    Rigidbody _rigidbody;
    bool _isHeld;

    public bool IsHeld => _isHeld;

    void Awake()
    {
        _collider = GetComponent<Collider>();
    }

    public string GetPromptText()
    {
        if (_isHeld)
            return string.Empty;

        PlayerCardHand hand = Object.FindFirstObjectByType<PlayerCardHand>();
        if (hand != null && hand.IsFull)
            return "Hand Full (" + CardDimensions.MaxHandSize + "/" + CardDimensions.MaxHandSize + ")";

        return "Press [E] To " + cardLabel;
    }

    public void Interact(GameObject interactor)
    {
        if (_isHeld)
            return;

        PlayerCardHand hand = interactor.GetComponent<PlayerCardHand>();
        if (hand == null)
            hand = interactor.GetComponentInChildren<PlayerCardHand>();

        if (hand == null)
            return;

        hand.TryPickup(this);
    }

    public void SetHeld(Transform handAnchor, int stackIndex)
    {
        _isHeld = true;
        RemovePhysics();

        if (_collider != null)
            _collider.enabled = false;

        transform.SetParent(handAnchor, false);
        ApplyHeldPose(stackIndex);
    }

    public void DropWithPhysics(Vector3 velocity)
    {
        _isHeld = false;
        transform.SetParent(null, true);
        transform.localScale = Vector3.one;

        if (_collider != null)
            _collider.enabled = true;

        EnsureRigidbody();
        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = true;

        // Velocities can only be set on dynamic bodies.
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

        // Destroy immediately so held cards don't keep simulating this frame.
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

    public void ApplyHeldPose(int stackIndex)
    {
        transform.localPosition = new Vector3(0f, stackIndex * CardDimensions.HandStackSpacing, 0f);
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one * 1.35f;
    }

    public void SetWorldPose(Vector3 position, Quaternion rotation)
    {
        _isHeld = false;
        RemovePhysics();

        if (_collider != null)
            _collider.enabled = true;

        transform.SetParent(null, true);
        transform.SetPositionAndRotation(position, rotation);
        transform.localScale = Vector3.one;
    }
}
