using UnityEngine;

/// <summary>
/// Basic first-person movement: WASD walk, mouse look, unlocked Left Shift run toggle, Space jump.
/// Attach to a CharacterController root with a child Camera at eye height.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] float walkSpeed = 3.15f;
    [SerializeField] float gravity = -20f;

    [Header("Jump")]
    [SerializeField] KeyCode jumpKey = KeyCode.Space;
    [SerializeField] float jumpHeight = 1.1f;

    [Header("Look")]
    [SerializeField] float mouseSensitivity = 2f;
    [SerializeField] float minPitch = -80f;
    [SerializeField] float maxPitch = 89f;
    [SerializeField] bool lockCursorOnStart = true;

    [Header("Zoom (hold right mouse button)")]
    [SerializeField, Range(0.25f, 1f)] float zoomFovMultiplier = 0.6f;
    [SerializeField, Min(1f)] float zoomTransitionSpeed = 160f;

    Camera _viewCamera;
    float _normalFov = GameSettings.DefaultFov;

    CharacterController _controller;
    float _pitch;
    float _verticalVelocity;
    bool _cursorLocked;
    bool _runToggled;
    bool _jumpInProgress;
    public bool IsLongJumpInProgress => _jumpInProgress && MinorSkillProgress.IsUnlocked(MinorSkillProgress.LongJump);
    public bool IsRunning => _runToggled && MinorSkillProgress.IsUnlocked(MinorSkillProgress.Run);
    internal float CurrentMoveSpeed => walkSpeed * (IsRunning ? MinorSkillProgress.UnlockedRunMultiplier : 1f);
    public static bool IsZoomInputActive { get; private set; }

    public PlayerSaveRecord CaptureSaveState()
    {
        Vector3 position = transform.position;
        return new PlayerSaveRecord
        {
            px = position.x,
            py = position.y,
            pz = position.z,
            yaw = transform.eulerAngles.y,
            pitch = _pitch,
            crouching = false, // Retained only for compatibility with older save files.
        };
    }

    public void RestoreSaveState(PlayerSaveRecord state)
    {
        if (state == null || _controller == null)
            return;
        if (!IsFinite(state.px) || !IsFinite(state.py) || !IsFinite(state.pz)
            || !IsFinite(state.yaw) || !IsFinite(state.pitch))
            return;

        bool wasEnabled = _controller.enabled;
        _controller.enabled = false;
        transform.SetPositionAndRotation(
            new Vector3(state.px, state.py, state.pz), Quaternion.Euler(0f, state.yaw, 0f));
        _verticalVelocity = 0f;
        _pitch = Mathf.Clamp(state.pitch, minPitch, maxPitch);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

        _runToggled = false;
        _jumpInProgress = false;
        _controller.enabled = wasEnabled;
    }

    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        // Pairwise IgnoreCollision is lost when the controller is disabled during
        // save restoration. A persistent exclusion keeps floor cards/packs from
        // becoming tiny steps under the player, including after load or re-enable.
        int worldCardLayer = LayerMask.NameToLayer(CardLayers.WorldCardLayerName);
        if (worldCardLayer >= 0)
            _controller.excludeLayers |= 1 << worldCardLayer;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform == null)
            Debug.LogWarning("FirstPersonController: assign a cameraTransform.", this);

        if (cameraTransform != null)
            _viewCamera = cameraTransform.GetComponent<Camera>();


    }

    void Start()
    {
        if (lockCursorOnStart)
        {
            _cursorLocked = true;
            if (!GamePause.IsPaused)
                SetCursorLocked(true);
        }

        ApplySettingsFov(GameSettings.Fov);
    }

    public void ApplySettingsFov(float fov)
    {
        _normalFov = Mathf.Clamp(fov, GameSettings.MinFov, GameSettings.MaxFov);
        if (_viewCamera == null && cameraTransform != null)
            _viewCamera = cameraTransform.GetComponent<Camera>();
        ResetZoom();
    }

    void UpdateCameraZoom()
    {
        PlayerCardHand hand = PlayerCardHand.Instance;
        bool canZoom = Application.isFocused && Cursor.lockState == CursorLockMode.Locked
            && !GamePause.IsPaused && !GameSceneLoader.IsLoading
            && CardInstancedRenderManager.IsGameplayReady && !WelcomePopupView.IsWaitingForStart
            && (hand == null || (!hand.IsOpeningPack && !hand.IsAwaitingRevealCollect && !hand.IsPackOpenMovementLocked));
        if (!canZoom)
        {
            ResetZoom();
            return;
        }
        UpdateZoom(Input.GetMouseButton(1), Time.unscaledDeltaTime);
    }

    void UpdateZoom(bool held, float deltaTime)
    {
        IsZoomInputActive = held && _viewCamera != null;
        if (_viewCamera == null) return;
        float target = held ? _normalFov * (zoomFovMultiplier / 1.2f) : _normalFov;
        float fov = Mathf.MoveTowards(_viewCamera.fieldOfView, target, zoomTransitionSpeed * deltaTime);
        if (_viewCamera.fieldOfView != fov) _viewCamera.fieldOfView = fov;
    }

    void ResetZoom()
    {
        IsZoomInputActive = false;
        // Zoom is temporary camera state, never a settings/save change.
        if (_viewCamera != null && _viewCamera.fieldOfView != _normalFov)
            _viewCamera.fieldOfView = _normalFov;
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused) ResetZoom();
    }

    void Update()
    {
        // Hand layout runs in LateUpdate and must see this frame's zoom.
        UpdateCameraZoom();
        if (GamePause.IsPaused || GameSceneLoader.IsLoading || !CardInstancedRenderManager.IsGameplayReady)
            return;

        Vector3 previousPosition = transform.position;
        Quaternion previousRotation = transform.rotation;
        float previousPitch = _pitch;
        HandleLook();

        bool movementLocked = IsPackOpenMovementLocked();
        if (!movementLocked)
        {
            if (Application.isFocused && Cursor.lockState == CursorLockMode.Locked
                && Input.GetKeyDown(KeyCode.LeftShift)) ToggleRun();
            HandleJump();
        }

        HandleMove(movementLocked);

        // Walking/look changes alone must also trigger autosave and save-on-exit.
        if (transform.position != previousPosition || transform.rotation != previousRotation
            || _pitch != previousPitch)
            GameSaveDirtyTracker.MarkDirty();
    }

    bool IsPackOpenMovementLocked()
    {
        PlayerCardHand hand = PlayerCardHand.Instance;
        return hand != null && hand.IsPackOpenMovementLocked;
    }

    void HandleLook()
    {
        if (!_cursorLocked || cameraTransform == null)
            return;

        float lookSpeed = GameSettings.LookSensitivity;
        if (lookSpeed <= 0.01f)
            lookSpeed = mouseSensitivity;

        float mouseX = Input.GetAxisRaw("Mouse X") * lookSpeed;
        float mouseY = Input.GetAxisRaw("Mouse Y") * lookSpeed;
        if (GameSettings.InvertX)
            mouseX = -mouseX;
        if (GameSettings.InvertY)
            mouseY = -mouseY;

        ApplyLookDelta(mouseX, mouseY);
    }

    internal void ApplyLookDelta(float mouseX, float mouseY)
    {
        transform.Rotate(Vector3.up, mouseX, Space.World);

        _pitch -= mouseY;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    internal void ToggleRun()
    {
        _runToggled = MinorSkillProgress.IsUnlocked(MinorSkillProgress.Run) && !_runToggled;
    }

    void HandleJump()
    {
        if (Input.GetKeyDown(jumpKey))
            TryJump();
    }

    internal bool TryJump()
    {
        if (IsPackOpenMovementLocked())
            return false;

        if (!_controller.isGrounded)
            return false;

        _verticalVelocity = Mathf.Sqrt(jumpHeight * PlayerJumpSkill.HeightMultiplier * -2f * gravity);
        _jumpInProgress = true;
        return true;
    }

    void HandleMove(bool movementLocked)
    {
        float inputX = movementLocked ? 0f : Input.GetAxisRaw("Horizontal");
        float inputZ = movementLocked ? 0f : Input.GetAxisRaw("Vertical");
        ApplyMoveInput(inputX, inputZ);
    }

    internal void ApplyMoveInput(float inputX, float inputZ) => ApplyMoveInput(inputX, inputZ, Time.deltaTime);

    internal void ApplyMoveInput(float inputX, float inputZ, float deltaTime)
    {
        Vector3 input = new Vector3(inputX, 0f, inputZ);
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        float speed = CurrentMoveSpeed;
        Vector3 move = (transform.right * input.x + transform.forward * input.z) * speed;

        if (_controller.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;

        _verticalVelocity += gravity * deltaTime;
        move.y = _verticalVelocity;

        CollisionFlags collisions = _controller.Move(move * deltaTime);
        if ((collisions & CollisionFlags.Below) != 0 && _verticalVelocity <= 0f)
            _jumpInProgress = false;
        if ((collisions & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
            _verticalVelocity = 0f;
    }

    void SetCursorLocked(bool locked)
    {
        _cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void OnDisable()
    {
        _jumpInProgress = false;
        ResetZoom();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
