using UnityEngine;

/// <summary>
/// Basic first-person movement: WASD walk, mouse look, hold Shift to crouch.
/// Attach to a CharacterController root with a child Camera at eye height.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] float walkSpeed = 4.5f;
    [SerializeField] float crouchSpeed = 2.4f;
    [SerializeField] float gravity = -20f;

    [Header("Crouch")]
    [SerializeField] KeyCode crouchKey = KeyCode.LeftShift;
    [SerializeField] KeyCode crouchKeyAlt = KeyCode.RightShift;
    [Tooltip("CharacterController height while crouching.")]
    [SerializeField] float crouchHeight = 1.0f;
    [SerializeField] float crouchTransitionSpeed = 12f;

    [Header("Look")]
    [SerializeField] float mouseSensitivity = 2f;
    [SerializeField] float minPitch = -80f;
    [SerializeField] float maxPitch = 80f;
    [SerializeField] bool lockCursorOnStart = true;

    CharacterController _controller;
    float _pitch;
    float _verticalVelocity;
    bool _cursorLocked;
    float _crouchBlend;
    float _standingHeight;
    float _standingCenterY;
    float _standingCameraLocalY;

    public bool IsCrouching => _crouchBlend > 0.05f;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform == null)
            Debug.LogWarning("FirstPersonController: assign a cameraTransform.", this);

        _standingHeight = _controller.height;
        _standingCenterY = _controller.center.y;
        _standingCameraLocalY = cameraTransform != null ? cameraTransform.localPosition.y : _standingHeight * 0.89f;
    }

    void Start()
    {
        if (lockCursorOnStart)
            SetCursorLocked(true);
    }

    void Update()
    {
        HandleCursorToggle();
        HandleLook();
        UpdateCrouch();
        HandleMove();
    }

    void HandleCursorToggle()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            SetCursorLocked(!_cursorLocked);
    }

    void HandleLook()
    {
        if (!_cursorLocked || cameraTransform == null)
            return;

        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up, mouseX, Space.World);

        _pitch -= mouseY;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    void UpdateCrouch()
    {
        bool wantsCrouch = Input.GetKey(crouchKey) || Input.GetKey(crouchKeyAlt);
        if (!wantsCrouch && !CanStand())
            wantsCrouch = true;

        float targetBlend = wantsCrouch ? 1f : 0f;
        _crouchBlend = Mathf.MoveTowards(_crouchBlend, targetBlend, crouchTransitionSpeed * Time.deltaTime);

        float height = Mathf.Lerp(_standingHeight, crouchHeight, _crouchBlend);
        float centerY = height * 0.5f;
        float crouchCameraY = _standingCameraLocalY * (crouchHeight / _standingHeight);
        float cameraY = Mathf.Lerp(_standingCameraLocalY, crouchCameraY, _crouchBlend);

        _controller.height = height;
        _controller.center = new Vector3(0f, centerY, 0f);

        if (cameraTransform != null)
        {
            Vector3 localPos = cameraTransform.localPosition;
            localPos.y = cameraY;
            cameraTransform.localPosition = localPos;
        }
    }

    bool CanStand()
    {
        if (_crouchBlend <= 0.001f)
            return true;

        float radius = Mathf.Max(0.05f, _controller.radius - 0.04f);
        float standHeight = _standingHeight;
        Vector3 bottom = transform.position + Vector3.up * (radius + 0.02f);
        Vector3 top = transform.position + Vector3.up * (standHeight - radius - 0.02f);

        Collider[] overlaps = Physics.OverlapCapsule(
            bottom,
            top,
            radius,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider overlap = overlaps[i];
            if (overlap == null)
                continue;

            if (overlap.transform == transform || overlap.transform.IsChildOf(transform))
                continue;

            return false;
        }

        return true;
    }

    void HandleMove()
    {
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");
        Vector3 input = new Vector3(inputX, 0f, inputZ);
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        float speed = Mathf.Lerp(walkSpeed, crouchSpeed, _crouchBlend);
        Vector3 move = (transform.right * input.x + transform.forward * input.z) * speed;

        if (_controller.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;

        _verticalVelocity += gravity * Time.deltaTime;
        move.y = _verticalVelocity;

        _controller.Move(move * Time.deltaTime);
    }

    void SetCursorLocked(bool locked)
    {
        _cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
