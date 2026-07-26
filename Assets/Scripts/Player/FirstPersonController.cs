using UnityEngine;

/// <summary>
/// Basic first-person movement: WASD walk, mouse look, optional sprint.
/// Attach to a CharacterController root with a child Camera at eye height.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] float walkSpeed = 4.5f;
    [SerializeField] float sprintSpeed = 7f;
    [SerializeField] float gravity = -20f;

    [Header("Look")]
    [SerializeField] float mouseSensitivity = 2f;
    [SerializeField] float minPitch = -80f;
    [SerializeField] float maxPitch = 80f;
    [SerializeField] bool lockCursorOnStart = true;

    CharacterController _controller;
    float _pitch;
    float _verticalVelocity;
    bool _cursorLocked;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform == null)
            Debug.LogWarning("FirstPersonController: assign a cameraTransform.", this);
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

    void HandleMove()
    {
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");
        Vector3 input = new Vector3(inputX, 0f, inputZ);
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;
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
