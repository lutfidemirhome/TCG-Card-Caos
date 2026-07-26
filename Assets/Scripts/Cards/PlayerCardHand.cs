using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds up to 10 cards in the right hand. Newest card stacks on top.
/// </summary>
public class PlayerCardHand : MonoBehaviour
{
    [SerializeField] Vector3 cameraHandOffset = new Vector3(0.42f, -0.2f, 0.58f);
    [SerializeField] float throwSpeed = 4.5f;
    [SerializeField] float throwUpBoost = 0.2f;
    [SerializeField] KeyCode dropKey = KeyCode.Q;

    Transform _handAnchor;
    Camera _camera;
    readonly List<WorldCard> _cards = new List<WorldCard>();

    public int Count => _cards.Count;
    public bool IsFull => _cards.Count >= CardDimensions.MaxHandSize;

    void Awake()
    {
        _camera = Camera.main;
        EnsureHandAnchor();
    }

    void Update()
    {
        if (!Input.GetKeyDown(dropKey) || Cursor.lockState != CursorLockMode.Locked)
            return;

        TryDropTopCard();
    }

    void LateUpdate()
    {
        UpdateHandAnchorTransform();
    }

    void EnsureHandAnchor()
    {
        if (_handAnchor != null)
            return;

        var anchorGo = new GameObject("RightHandAnchor");
        _handAnchor = anchorGo.transform;
        UpdateHandAnchorTransform();
    }

    void UpdateHandAnchorTransform()
    {
        if (_handAnchor == null)
            return;

        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null)
            return;

        if (_handAnchor.parent != _camera.transform)
            _handAnchor.SetParent(_camera.transform, false);

        _handAnchor.localPosition = cameraHandOffset;

        // Flat horizontal stack while following the camera.
        float pitch = _camera.transform.eulerAngles.x;
        if (pitch > 180f)
            pitch -= 360f;

        _handAnchor.localRotation = Quaternion.Euler(-pitch, 0f, 0f);
    }

    public bool TryPickup(WorldCard card)
    {
        if (card == null || card.IsHeld || IsFull)
            return false;

        EnsureHandAnchor();
        UpdateHandAnchorTransform();

        _cards.Add(card);
        RefreshStackLayout();
        return true;
    }

    public bool TryDropTopCard()
    {
        if (_cards.Count == 0)
            return false;

        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null)
            return false;

        WorldCard topCard = _cards[_cards.Count - 1];
        _cards.RemoveAt(_cards.Count - 1);

        Vector3 throwDirection = (_camera.transform.forward + _camera.transform.up * throwUpBoost).normalized;
        topCard.DropWithPhysics(throwDirection * throwSpeed);

        RefreshStackLayout();
        return true;
    }

    void RefreshStackLayout()
    {
        for (int i = 0; i < _cards.Count; i++)
            _cards[i].SetHeld(_handAnchor, i);
    }
}
