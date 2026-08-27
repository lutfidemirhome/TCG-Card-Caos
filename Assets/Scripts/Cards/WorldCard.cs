using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A card lying in the world. Press E to pick up into the right hand.
/// Static world cards render through <see cref="CardInstancedRenderManager"/>; visuals/outlines spawn lazily.
/// </summary>
[SelectionBase]
public class WorldCard : MonoBehaviour, IInteractable, IInteractionHighlight
{
    enum HandState
    {
        World,
        FlyingToHand,
        FlyingToShelf,
        Held,
        PackReveal,
    }

    enum ShelfPlacementStatus
    {
        None,
        Correct,
        Incorrect,
    }

    const int ShelfPlacementFlashPulses = 2;
    const float ShelfPlacementFlashOnSeconds = 0.12f;
    const float ShelfPlacementFlashOffSeconds = 0.1f;
    const float ShelfRowCompletePulseScale = 1.14f;
    const float ShelfRowCompleteUpSeconds = 0.12f;
    const float ShelfRowCompleteDownSeconds = 0.16f;
    /// <summary>
    /// Instanced ground draw is a one-sided quad. Only clearly face-up roots are safe;
    /// tilted / face-down cards keep a two-sided mesh so they stay visible.
    /// </summary>
    const float InstancedFaceUpMinY = 0.72f;

    [SerializeField] string cardLabel = "Card";
    [SerializeField] CardDefinition definition;
    [SerializeField] int paletteIndex;
    [Tooltip("0 = normal kart. 7–10 = PSA dolap slot numarası.")]
    [SerializeField] int psaSlotNumber;
    [Tooltip("PSA slot içindeki varyant (psa_7_1 → 1, psa_7_2 → 2).")]
    [SerializeField] int psaVariantIndex;

    Collider _collider;
    PsaCardVisualController _psaController;
    Rigidbody _rigidbody;
    [SerializeField] Transform _cardVisual;
    bool _handSelected;
    GameObject _outlineObject;
    GameObject _handSelectionOutlineObject;
    GameObject _shelfStatusOutlineObject;
    ShelfPlacementStatus _shelfPlacementStatus = ShelfPlacementStatus.None;
    Coroutine _shelfPlacementFlashRoutine;
    Coroutine _shelfRowCompleteRoutine;
    GameObject _shelfRowCompleteFill;
    HandState _handState = HandState.World;
    bool _authoredPhysicsItem;
    float _packRevealFlipT;
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
    Transform _shelfFlightSlot;
    float _shelfFlightSurfacePadding;
    System.Action _onShelfFlightComplete;
    bool _usePsaCabinetPlacement;
    bool _psaCabinetPlaced;
    Vector3 _psaCabinetLocalPosition;
    Quaternion _psaCabinetLocalRotation;
    Vector3 _psaCabinetLocalScale = Vector3.one * CardDimensions.GroundCardScale;
    float _scaleFrom = 1f;
    float _scaleTo = 1f;
    float _scaleTransitionElapsed;
    float _scaleTransitionDuration;
    bool _scaleTransitionActive;
    int _groundStackLayer;
    bool _worldColliderRequested;
    bool _landingSurfaceRequested;
    [SerializeField] bool groundShowsBack;

    public bool IsHeld => _handState == HandState.Held;
    public bool IsFlyingToHand => _handState == HandState.FlyingToHand;
    public bool IsFlyingToShelf => _handState == HandState.FlyingToShelf;
    public bool IsInHand => _handState == HandState.Held || _handState == HandState.FlyingToHand;
    public bool HasActivePhysics => _rigidbody != null;
    public bool IsShelfRowCompleteLocked => _shelfRowCompleteRoutine != null;

    /// <summary>
    /// True only while the solver is still moving the card. A settled card keeps a frozen (kinematic)
    /// body as its solid surface, so <see cref="HasActivePhysics"/> alone cannot tell "still flying"
    /// from "already at rest" — stack layering and settle math need this distinction.
    /// </summary>
    public bool IsPhysicsSimulating => _rigidbody != null && !_rigidbody.isKinematic;
    public int CardDefinitionId => definition != null ? definition.GetInstanceID() : 0;
    public int PaletteIndex => paletteIndex;
    public int GroundStackLayer => _groundStackLayer;
    public bool UsesPsaSlab => PsaArtLibrary.IsCabinetSlotNumber(psaSlotNumber);
    public int PsaSlotNumber => psaSlotNumber;
    public int PsaVariantIndex => Mathf.Max(1, psaVariantIndex);
    public float GroundRestLift => UsesPsaSlab && _psaController != null ? _psaController.GroundRestLift : 0f;
    internal Transform RootTransform => transform;
    internal Collider PhysCollider => _collider;
    internal Rigidbody PhysicsBody => _rigidbody;
    public CardDefinition Definition => definition;
    public bool HasShelfRules => definition != null;
    public string ShelfCategoryId => definition != null ? definition.ShelfCategoryId : string.Empty;
    public int ShelfSlotNumber => definition != null ? definition.ShelfSlotNumber : 0;
    public bool HasShelfPlacementFeedback =>
        _shelfPlacementStatus != ShelfPlacementStatus.None
        || _shelfPlacementFlashRoutine != null
        || _shelfRowCompleteRoutine != null;

    public bool CanUseInstancedRendering =>
        Application.isPlaying
        && !UsesPsaSlab
        && _handState == HandState.World
        && !_scaleTransitionActive
        && _rigidbody == null
        && _cardVisual == null
        && !_interactionHighlighted
        && !_authoredPhysicsItem
        && !groundShowsBack
        && transform.up.y >= InstancedFaceUpMinY
        && GetComponentInParent<CardShelfSlot>() == null;

    public bool CanUseInstancedBackRendering =>
        Application.isPlaying
        && !UsesPsaSlab
        && _handState == HandState.World
        && !_scaleTransitionActive
        && _rigidbody == null
        && _cardVisual == null
        && !_interactionHighlighted
        && !_authoredPhysicsItem
        && groundShowsBack
        && transform.up.y >= InstancedFaceUpMinY
        && GetComponentInParent<CardShelfSlot>() == null;

    public bool IsGroundFaceDown
    {
        get
        {
            // PSA card-ref uses WorldVisualRotation, so visual.forward points down while the
            // slab is face-up. Hover/refresh then applied a 180° flip and showed the back.
            if (UsesPsaSlab)
                return groundShowsBack || transform.up.y < 0f;

            if (_cardVisual != null)
                return _cardVisual.forward.y < 0f;

            return groundShowsBack || transform.up.y < 0f;
        }
    }

    bool UsesDefinitionFrontArt => definition != null && definition.FrontTexture != null;

    public string GetInstancedBatchKey()
    {
        if (CanUseInstancedBackRendering)
            return CardInstancedRenderManager.BackBatchKey;

        if (UsesDefinitionFrontArt && definition != null)
            return definition.DefinitionId;

        return CardInstancedRenderManager.PaletteBatchPrefix + PaletteIndex;
    }

    public void SetGroundStackLayer(int layer)
    {
        _groundStackLayer = Mathf.Max(0, layer);
    }

    /// <summary>
    /// Moves the card and drags its physics body along. Auto Sync Transforms is off project-wide, so
    /// writing the transform alone leaves the collider behind: the card then draws where nothing is
    /// solid, and the next card thrown at it stops on the invisible surface and reads as sunk into it.
    /// </summary>
    public void SetGroundRestPosition(Vector3 position)
    {
        transform.position = position;
        if (_rigidbody != null)
            _rigidbody.position = position;
    }

    /// <summary>World center for ground-card aim tests — matches the visible card.</summary>
    public Vector3 GetGroundQueryCenter()
    {
        return transform.position;
    }

    /// <summary>
    /// Keeps a thrown PSA slab's visible mesh above the floor without flattening its landed rotation.
    /// </summary>
    public void LiftPsaMeshAboveFloor()
    {
        if (!UsesPsaSlab || _psaController == null)
            return;
        if (IsInHand || IsFlyingToShelf || _psaCabinetPlaced || IsInPsaCabinetSlot())
            return;
        if (GetComponentInParent<CardShelfSlot>() != null)
            return;

        _psaController.LiftMeshAboveFloor();
    }

    public void SetGroundShowsBack(bool showsBack)
    {
        if (groundShowsBack == showsBack)
            return;

        groundShowsBack = showsBack;
        if (Application.isPlaying && _handState == HandState.World)
            RefreshRenderMode();
    }

    public void Initialize(CardDefinition cardDefinition, int palette)
    {
        definition = cardDefinition;
        paletteIndex = palette;
        psaSlotNumber = 0;
        psaVariantIndex = 0;

        if (definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName))
            cardLabel = definition.DisplayName;
    }

    /// <summary>PSA slab kartı — normal kart oyun mantığı, 3D holder görseli.</summary>
    public void InitializePsa(int slotNumber, int variantIndex = 1)
    {
        definition = null;
        paletteIndex = 0;
        psaSlotNumber = PsaArtLibrary.ClampCabinetSlotNumber(slotNumber);
        psaVariantIndex = Mathf.Max(1, variantIndex);
        cardLabel = "PSA " + psaSlotNumber + "-" + psaVariantIndex;
        _psaController = new PsaCardVisualController(this);
        _psaController.Build(psaSlotNumber, psaVariantIndex);
    }

    public void Initialize(int definitionId, int palette)
    {
        paletteIndex = palette;
        if (CardCatalog.TryGetById(definitionId.ToString(), out CardDefinition found))
        {
            Initialize(found, palette);
            return;
        }

        CardDefinition legacy = Resources.Load<CardDefinition>("Cards/Definitions/" + definitionId);
        if (legacy != null)
            Initialize(legacy, palette);
    }

    void Awake()
    {
        _collider = GetComponent<Collider>();
        _authoredPhysicsItem = GetComponent<PhysicsLevelItem>() != null
            || GetComponentInParent<PhysicsLevelLayout>() != null
            || !IsUnderScatterRoot(transform);

        Transform existingVisual = transform.Find("CardVisual");
        if (existingVisual != null)
            _cardVisual = existingVisual;

        SetWorldColliderEnabled(false);
    }

    static bool IsUnderScatterRoot(Transform t)
    {
        while (t != null)
        {
            if (t.name == CardScatterUtility.ScatterRootName)
                return true;
            t = t.parent;
        }

        return false;
    }

    void OnEnable()
    {
        if (CardInstancedRenderManager.DeferGroundRegistration)
            return;

        TryRegisterInstancedRendering();
    }

    void OnDisable()
    {
        CardInstancedRenderManager.ReleaseFromGround(this);
    }

    void OnDestroy()
    {
        CardInstancedRenderManager.ReleaseFromGround(this);
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
               && _handState != HandState.FlyingToShelf
               && _handState != HandState.Held)
        {
            _scaleTransitionElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_scaleTransitionElapsed / _scaleTransitionDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
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
        if (IsGroundFaceDown)
            visualRotation *= Quaternion.Euler(180f, 0f, 0f);

        Vector3 position = transform.position;
        if (GroundStackLayer == 0)
            position.y += CardGroundStack.GetUniqueDepthBias(this);
        return Matrix4x4.TRS(position, visualRotation, scale);
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
        _worldColliderRequested = enabled;
        ApplyWorldColliderState();
    }

    public void SetPlayerAimFocus(bool focused)
    {
        if (IsInHand || IsFlyingToShelf || _rigidbody != null)
            return;

        SetWorldColliderEnabled(focused);
    }

    void ApplyWorldColliderState()
    {
        if (_collider == null || IsInHand)
            return;

        // Two independent requesters. Aim/render state must not be able to drop the landing surface that
        // is being held for an item in flight: the interaction prompt refreshes the render mode the moment
        // the aim target changes, which used to switch the collider off mid-flight and let a card thrown at
        // the very card the player is looking at pass straight through it and land underneath.
        _collider.enabled = _worldColliderRequested || _landingSurfaceRequested;
    }

    public void SetInteractionHighlight(bool highlighted)
    {
        _interactionHighlighted = highlighted && !IsInHand;
        RefreshRenderMode();
    }

    public string GetPromptText()
    {
        if (IsInHand || IsFlyingToShelf || IsShelfRowCompleteLocked)
            return string.Empty;

        PlayerCardHand hand = PlayerCardHand.Instance;
        if (hand != null && (hand.IsFull || hand.AvailableSlots <= 0))
            return Localization.Format(
                LocalizationKeys.PromptHandFull,
                CardDimensions.MaxHandSize,
                CardDimensions.MaxHandSize);

        return InteractPrompt.Format(Localization.Format(LocalizationKeys.PromptPickUp, ResolvePickupName()));
    }

    string ResolvePickupName()
    {
        if (definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName))
            return definition.DisplayName;
        if (psaSlotNumber > 0)
            return "PSA " + psaSlotNumber + "-" + psaVariantIndex;
        return string.IsNullOrWhiteSpace(cardLabel) ? "Card" : cardLabel;
    }

    public void Interact(GameObject interactor)
    {
        if (IsInHand || IsFlyingToShelf || IsShelfRowCompleteLocked)
            return;

        PlayerCardHand hand = PlayerCardHandResolver.FromInteractor(interactor);

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

        CardShelfSlot shelfSlot = GetComponentInParent<CardShelfSlot>();
        if (shelfSlot != null)
            shelfSlot.ClearIfMatches(this);

        PsaCabinetSlot psaCabinetSlot = GetComponentInParent<PsaCabinetSlot>();
        if (psaCabinetSlot != null)
            psaCabinetSlot.ClearIfMatches(this);

        _psaCabinetPlaced = false;

        ClearShelfPlacementStatus();
        CardInteractionFocus.ClearFocus();
        CardGroundQuery.UntrackShelfCard(this);
        CardInstancedRenderManager.ReleaseFromGround(this);
        CardGroundStack.UntrackPhysicsCard(this);

        SetInteractionHighlight(false);
        SetHandSelected(false);
        RemovePhysics();

        if (_collider != null)
            _collider.enabled = false;

        transform.SetParent(null, true);
        EnsureCardVisual();
        ApplyHandVisualOrientation();
        if (UsesPsaSlab && _psaController != null)
            _psaController.AlignRootRotationForHandPickup();
        RefreshRenderMode();
    }

    public void UpdatePickupFlight(Vector3 targetWorldPos, Quaternion targetWorldRot)
    {
        if (_handState != HandState.FlyingToHand)
            return;

        AdvanceFlightToward(targetWorldPos, targetWorldRot);

        if (_flightElapsed >= _flightDuration)
            CompletePickupFlight();
    }

    void CompletePickupFlight()
    {
        _handState = HandState.Held;
        transform.SetParent(_handAnchor, false);
        EnsureCardVisual();
        ApplyHandVisualOrientation();
        RefreshRenderMode();

        System.Action callback = _onPickupFlightComplete;
        _onPickupFlightComplete = null;
        callback?.Invoke();
    }

    public void RestoreIntoHand(Transform handAnchor, float targetHandScale)
    {
        BeginPickupFlight(handAnchor, targetHandScale, 0.05f, 0f);
        CompletePickupFlight();
    }

    /// <summary>
    /// Shows a detail card during pack reveal before the card joins the hand fan.
    /// When <paramref name="showsBack"/> is true, the card back faces the camera first.
    /// </summary>
    public void BeginRevealPreview(
        Transform parent,
        Vector3 localPosition,
        Quaternion localRotation,
        float scale,
        bool showsBack = false)
    {
        _handState = HandState.PackReveal;
        _packRevealFlipT = showsBack ? 0f : 1f;
        _handAnchor = null;
        RemovePhysics();
        CardInstancedRenderManager.ReleaseFromGround(this);

        if (_collider != null)
            _collider.enabled = false;

        transform.SetParent(parent, false);
        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
        transform.localScale = Vector3.one * scale;
        EnsureCardVisual();
        if (_cardVisual != null)
            _cardVisual.localPosition = Vector3.zero;
        ApplyRevealVisualOrientation(showsBack ? 0f : 1f);
        RefreshRenderMode();
    }

    public PackRevealCardSparkle AttachRevealSparkle(float revealScale)
    {
        EnsureCardVisual();
        Transform anchor = _cardVisual != null ? _cardVisual : transform;
        return PackRevealCardSparkle.Attach(anchor, revealScale);
    }

    void ApplyRevealVisualOrientation(float frontT)
    {
        if (_cardVisual == null)
            return;

        frontT = Mathf.Clamp01(frontT);
        SetCardVisualMesh(CardArtLibrary.HandCardMesh);
        _cardVisual.localRotation = GetRevealPageTurnRotation(frontT);
        _cardVisual.localPosition = Vector3.zero;
        _cardVisual.localScale = CardArtLibrary.HandVisualScale;
    }

    /// <summary>
    /// Page-turn flip around the card center: 0 = back toward camera, 1 = front toward camera.
    /// </summary>
    public static Quaternion GetRevealPageTurnRotation(float frontT)
    {
        frontT = EaseRevealPageTurn(frontT);

        if (frontT < 0.5f)
        {
            float halfT = frontT * 2f;
            float y = halfT * 90f;
            return CardArtLibrary.RevealBackVisualLocalRotation * Quaternion.Euler(0f, y, 0f);
        }

        float frontHalfT = (frontT - 0.5f) * 2f;
        float frontY = Mathf.Lerp(-90f, 0f, frontHalfT);
        return CardArtLibrary.RevealFrontVisualLocalRotation * Quaternion.Euler(0f, frontY, 0f);
    }

    static float EaseRevealPageTurn(float frontT)
    {
        frontT = Mathf.Clamp01(frontT);
        return frontT * frontT * (3f - 2f * frontT);
    }

    public void SetRevealVisualFlip(float frontT)
    {
        if (_handState != HandState.PackReveal)
            return;

        _packRevealFlipT = frontT;
        EnsureCardVisual();
        ApplyRevealVisualOrientation(frontT);
    }

    public void BeginPsaCabinetPlacementFlight(
        Transform anchor,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale,
        float targetWorldScale,
        float duration,
        float arcHeight,
        System.Action onComplete = null)
    {
        if (anchor == null)
            return;

        _psaCabinetLocalPosition = localPosition;
        _psaCabinetLocalRotation = localRotation;
        _psaCabinetLocalScale = localScale;

        BeginShelfFlight(anchor, targetWorldScale, duration, arcHeight, 0f, onComplete);
        // BeginShelfFlight clears this flag; re-assert after so CompleteShelfFlight uses PSA pose.
        _usePsaCabinetPlacement = true;
    }

    public void BeginShelfFlight(
        Transform slot,
        float targetWorldScale,
        float duration,
        float arcHeight,
        float surfacePadding,
        System.Action onComplete = null)
    {
        if (slot == null)
            return;

        _usePsaCabinetPlacement = false;
        _handState = HandState.FlyingToShelf;
        _handAnchor = null;
        _flightTargetHandScale = targetWorldScale;
        _flightDuration = Mathf.Max(0.05f, duration);
        _flightElapsed = 0f;
        _flightStartWorldPos = transform.position;
        _flightStartWorldRot = transform.rotation;
        _flightStartWorldScale = transform.localScale.x;
        _flightArcHeight = arcHeight;
        _shelfFlightSlot = slot;
        _shelfFlightSurfacePadding = surfacePadding;
        _onShelfFlightComplete = onComplete;

        SetInteractionHighlight(false);
        SetHandSelected(false);
        RemovePhysics();

        if (_collider != null)
            _collider.enabled = false;

        transform.SetParent(null, true);
        EnsureCardVisual();
        if (UsesPsaSlab && _psaController != null)
            _psaController.ApplyCabinetSlotOrientation();
        else
            ApplyShelfVisualOrientation();
        RefreshRenderMode();
    }

    public void UpdateShelfFlight(Vector3 targetWorldPos, Quaternion targetWorldRot)
    {
        if (_handState != HandState.FlyingToShelf)
            return;

        AdvanceFlightToward(targetWorldPos, targetWorldRot);

        if (_flightElapsed >= _flightDuration)
            CompleteShelfFlight();
    }

    void CompleteShelfFlight()
    {
        Transform slot = _shelfFlightSlot;
        float surfacePadding = _shelfFlightSurfacePadding;
        bool usePsaCabinetPlacement = _usePsaCabinetPlacement;
        Vector3 psaLocalPosition = _psaCabinetLocalPosition;
        Quaternion psaLocalRotation = _psaCabinetLocalRotation;
        Vector3 psaLocalScale = _psaCabinetLocalScale;
        _shelfFlightSlot = null;
        _usePsaCabinetPlacement = false;

        if (slot != null)
        {
            if (usePsaCabinetPlacement)
                PlaceOnPsaCabinetSlot(slot, psaLocalPosition, psaLocalRotation, psaLocalScale);
            else
                PlaceOnShelfSlot(slot, surfacePadding);
        }

        System.Action callback = _onShelfFlightComplete;
        _onShelfFlightComplete = null;
        callback?.Invoke();
    }

    void AdvanceFlightToward(Vector3 targetWorldPos, Quaternion targetWorldRot)
    {
        _flightElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_flightElapsed / _flightDuration);
        float smoothT = Mathf.SmoothStep(0f, 1f, t);

        Vector3 pos = Vector3.Lerp(_flightStartWorldPos, targetWorldPos, smoothT);
        pos += Vector3.up * (Mathf.Sin(smoothT * Mathf.PI) * _flightArcHeight);

        transform.SetPositionAndRotation(pos, Quaternion.Slerp(_flightStartWorldRot, targetWorldRot, smoothT));
        float scale = Mathf.Lerp(_flightStartWorldScale, _flightTargetHandScale, smoothT);
        transform.localScale = Vector3.one * scale;
    }

    public void DropWithPhysics(Vector3 velocity, float worldScaleTransitionDuration = 0.12f)
    {
        _handState = HandState.World;
        SetHandSelected(false);
        EnsureCardVisual();
        ConvertHandVisualToWorldRoot();
        transform.SetParent(null, true);

        ApplyFlatWorldCollider();

        if (_collider is BoxCollider boxCollider)
        {
            boxCollider.isTrigger = false;
            boxCollider.enabled = true;
            _worldColliderRequested = true;
        }
        else if (_collider != null)
        {
            _collider.enabled = true;
            _worldColliderRequested = true;
        }

        IgnorePlayerCollision();

        BeginScaleTransition(transform.localScale.x, CardDimensions.GroundCardScale, worldScaleTransitionDuration);

        EnsureRigidbody();
        RefreshRenderMode();
        CardCollisionUtility.LaunchThrownBody(_rigidbody, velocity);
        if (_collider is BoxCollider thrownBox)
            CardCollisionUtility.UnstickThrownSpawnOverlap(transform, thrownBox, this, _rigidbody);

        CardGroundStack.TrackPhysicsCard(this);
        StartCoroutine(MonitorThrownCardRoutine());
    }

    IEnumerator MonitorThrownCardRoutine()
    {
        var boxCollider = _collider as BoxCollider;

        yield return CardThrownPhysics.Monitor(
            transform,
            _rigidbody,
            boxCollider,
            () => _handState == HandState.World && _rigidbody != null,
            onSettled: attempt => CardSettlePlacement.TrySettle(this, boxCollider, _rigidbody, attempt));

        if (_handState != HandState.World || _rigidbody == null)
            yield break;

        SetInteractionHighlight(false);
        RefreshRenderMode();
    }

    void IgnorePlayerCollision()
    {
        CardCollisionUtility.IgnorePlayerCollision(_collider);
    }

    /// <summary>Solid collider for nearby cards while another card is flying.</summary>
    public void EnableLandingCollider()
    {
        if (IsInHand || IsPhysicsSimulating)
            return;

        _landingSurfaceRequested = true;

        if (_collider is BoxCollider boxCollider)
        {
            if (UsesPsaSlab && _psaController != null)
                _psaController.ApplyBodyCollider();
            else
                CardCollisionUtility.ApplyFlatWorldSize(boxCollider);
            boxCollider.isTrigger = false;
        }

        ApplyWorldColliderState();
    }

    public void RestoreGroundCollider()
    {
        if (IsInHand || _rigidbody != null)
            return;

        _landingSurfaceRequested = false;

        if (_collider is BoxCollider boxCollider)
            boxCollider.isTrigger = true;

        ApplyWorldColliderState();
    }

    /// <summary>
    /// Editor level-builder: shows the real card mesh and a solid BoxCollider so Grabbit can drop it.
    /// Does not change artwork, UVs, or gameplay data.
    /// </summary>
    public void PrepareEditorPhysicsPlacement()
    {
        RefreshAuthoredVisual();
        ApplySolidEditorCollider();
        if (_collider is BoxCollider boxCollider)
            CardCollisionUtility.ApplyAuthoringWorldSize(boxCollider);
    }

    /// <summary>
    /// Rebinds CardVisual, restores the card mesh, and rebuilds URP materials.
    /// Use after Grabbit/bake when a card turns magenta (missing shader).
    /// </summary>
    public void RefreshAuthoredVisual()
    {
        CardArtLibrary.EnsureLoaded();
        CardArtLibrary.InvalidateFrontMaterials(definition, paletteIndex);

        if (UsesPsaSlab)
        {
            EnsureCardVisual();
            return;
        }

        BindExistingCardVisual();
        EnsureCardVisual();
        RestoreCardVisualMeshAndRenderer();
        ApplyCardVisualTextureQuality();
        if (_cardVisual != null && !_cardVisual.gameObject.activeSelf)
            _cardVisual.gameObject.SetActive(true);
    }

    public void ApplySolidEditorCollider()
    {
        if (_collider == null)
            _collider = GetComponent<Collider>();

        ApplyFlatWorldCollider();
        if (_collider is BoxCollider boxCollider)
            boxCollider.isTrigger = false;

        _worldColliderRequested = true;
        _landingSurfaceRequested = false;
        ApplyWorldColliderState();
    }

    public void StripEditorRigidbody()
    {
        RemovePhysics();
    }

    void ApplyFlatWorldCollider()
    {
        if (UsesPsaSlab && _psaController != null)
        {
            _psaController.ApplyBodyCollider();
            return;
        }

        if (_collider is BoxCollider boxCollider)
            CardCollisionUtility.ApplyFlatWorldSize(boxCollider);
    }

    /// <summary>
    /// Always flat on the floor (never upright). Keeps the landing yaw — no 90°/180° remapping.
    /// </summary>
    void FlattenAndSnapToGround()
    {
        EnsureCardVisual();
        ApplyFlatWorldCollider();

        bool frontFaceUp = (_cardVisual != null ? _cardVisual.forward : transform.up).y >= 0f;

        Vector3 heading = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (heading.sqrMagnitude < 0.0001f)
            heading = Vector3.ProjectOnPlane(transform.right, Vector3.up);
        if (heading.sqrMagnitude < 0.0001f)
            heading = Vector3.forward;
        heading.Normalize();

        transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
        groundShowsBack = !frontFaceUp;
        ApplyWorldVisualOrientation();

        CardGroundStack.ApplyStackHeight(this, placeOnTop: true);

        if (_collider is BoxCollider box)
            box.isTrigger = true;
        SetWorldColliderEnabled(false);
    }

    /// <summary>
    /// Preserves world-space card art when changing local visual from Hand to World rotation.
    /// </summary>
    void ConvertHandVisualToWorldRoot()
    {
        if (UsesPsaSlab && _psaController != null)
        {
            groundShowsBack = false;
            _psaController.ConvertHandVisualToWorldRoot();
            return;
        }

        EnsureCardVisual();
        if (_cardVisual == null)
            return;

        // Hand always shows the front. The bake below assumes WorldVisualRotation (front-up local).
        // A leftover groundShowsBack from a face-down pickup would apply an extra 180° and flash
        // the back toward the camera — most obvious when throwing while looking upward.
        transform.rotation = _cardVisual.rotation * Quaternion.Inverse(CardArtLibrary.WorldVisualRotation);
        groundShowsBack = false;
        ApplyWorldVisualOrientation();
    }

    bool IsCardFaceUp()
    {
        if (_cardVisual != null)
            return _cardVisual.forward.y >= 0f;

        return !IsGroundFaceDown;
    }

    void RemovePhysics()
    {
        Rigidbody rb = _rigidbody != null ? _rigidbody : GetComponent<Rigidbody>();
        if (rb == null)
        {
            _rigidbody = null;
            return;
        }

        // Destroy() is deferred — raycast skips cards with a Rigidbody, so clear it now.
        DestroyImmediate(rb);
        _rigidbody = null;
        CardGroundStack.UntrackPhysicsCard(this);
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

    public void ApplyFanPose(int fanIndex, int fanCount, in HandFanLayoutSettings layout, bool isSelected)
    {
        HandCardPose pose = HandFanLayout.GetPose(fanIndex, fanCount, layout, isSelected);
        ApplyFanPose(pose, isSelected);
    }

    public void ApplyFanPose(HandCardPose pose, bool isSelected)
    {
        if (_handState != HandState.Held)
            return;

        EnsureCardVisual();
        transform.localPosition = pose.LocalPosition;
        transform.localRotation = pose.LocalRotation;
        transform.localScale = Vector3.one * pose.Scale;
        ApplyHandVisualOrientation();
        SetHandSelected(isSelected);
    }

    void ApplyHandVisualOrientation()
    {
        if (UsesPsaSlab && _psaController != null)
        {
            _psaController.ApplyHandOrientation();
            return;
        }

        if (_cardVisual == null)
            return;

        _cardVisual.localPosition = Vector3.zero;
        _cardVisual.localRotation = CardArtLibrary.HandVisualRotation;
        _cardVisual.localScale = CardArtLibrary.HandVisualScale;
        SetCardVisualMesh(CardArtLibrary.HandCardMesh);
    }

    void ApplyWorldVisualOrientation()
    {
        if (UsesPsaSlab && _psaController != null)
        {
            _psaController.ApplyWorldOrientation(alignModelToGround: !HasActivePhysics);
            _psaController.ApplyBodyCollider();
            return;
        }

        if (_cardVisual == null)
            return;

        _cardVisual.localRotation = groundShowsBack
            ? CardArtLibrary.WorldVisualRotation * Quaternion.Euler(180f, 0f, 0f)
            : CardArtLibrary.WorldVisualRotation;
        _cardVisual.localScale = CardArtLibrary.WorldVisualScale;
        SetCardVisualMesh(CardArtLibrary.CardMesh);
    }

    void ApplyShelfVisualOrientation()
    {
        if (_cardVisual == null)
            return;

        _cardVisual.localRotation = CardArtLibrary.ShelfVisualRotation;
        _cardVisual.localScale = CardArtLibrary.ShelfVisualScale;
        SetCardVisualMesh(CardArtLibrary.CardMesh);
    }

    void SetCardVisualMesh(Mesh mesh)
    {
        if (_cardVisual == null || mesh == null)
            return;

        var meshFilter = _cardVisual.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != mesh)
            meshFilter.sharedMesh = mesh;
    }

    void SetVisualRotation(Quaternion localRotation)
    {
        if (_cardVisual != null)
            _cardVisual.localRotation = localRotation;
    }

    public void SetHandSelected(bool selected)
    {
        _handSelected = selected;

        if (UsesPsaSlab && _psaController != null)
        {
            ReleaseHandSelectionOutline();
            _psaController.RefreshOutlineState(_interactionHighlighted, selected);
            return;
        }

        if (selected)
            EnsureHandSelectionOutlineRenderer();
        else
            ReleaseHandSelectionOutline();

        if (_handSelectionOutlineObject != null)
            _handSelectionOutlineObject.SetActive(selected && IsHeld);
    }

    public void NotifyShelfPlacement(bool isCorrect)
    {
        PsaCabinetSlot cabinetSlot = GetComponentInParent<PsaCabinetSlot>();
        if (cabinetSlot != null)
        {
            cabinetSlot.NotifyPlacementFeedback(isCorrect);
            return;
        }

        if (_shelfPlacementFlashRoutine != null)
        {
            StopCoroutine(_shelfPlacementFlashRoutine);
            _shelfPlacementFlashRoutine = null;
        }

        if (isCorrect)
            _shelfPlacementFlashRoutine = StartCoroutine(ShelfPlacementFlashRoutine(ShelfPlacementStatus.Correct));
        else
            _shelfPlacementFlashRoutine = StartCoroutine(ShelfPlacementFlashRoutine(ShelfPlacementStatus.Incorrect));
    }

    /// <summary>
    /// Whole-row celebrate: solid yellow fill and a short upward scale pulse.
    /// Pickup and aim are locked until this finishes.
    /// </summary>
    public void PlayShelfRowCompleteFeedback()
    {
        if (!isActiveAndEnabled)
            return;

        StopShelfRowCompleteFeedback(restorePose: true);

        if (_shelfPlacementFlashRoutine != null)
        {
            StopCoroutine(_shelfPlacementFlashRoutine);
            _shelfPlacementFlashRoutine = null;
        }

        _shelfPlacementStatus = ShelfPlacementStatus.None;
        ReleaseShelfStatusOutline();
        SetInteractionHighlight(false);
        _shelfRowCompleteRoutine = StartCoroutine(ShelfRowCompleteRoutine());
    }

    public void ClearShelfPlacementStatus()
    {
        PsaCabinetSlot cabinetSlot = GetComponentInParent<PsaCabinetSlot>();
        if (cabinetSlot != null)
        {
            cabinetSlot.ClearPlacementFeedback();
            RefreshRenderMode();
            return;
        }

        if (_shelfPlacementFlashRoutine != null)
        {
            StopCoroutine(_shelfPlacementFlashRoutine);
            _shelfPlacementFlashRoutine = null;
        }

        StopShelfRowCompleteFeedback(restorePose: true);
        _shelfPlacementStatus = ShelfPlacementStatus.None;
        ReleaseShelfStatusOutline();
        RefreshRenderMode();
    }

    System.Collections.IEnumerator ShelfPlacementFlashRoutine(ShelfPlacementStatus flashStatus)
    {
        EnsureShelfStatusOutline();

        for (int pulse = 0; pulse < ShelfPlacementFlashPulses; pulse++)
        {
            ApplyShelfStatusOutlineMaterial(flashStatus);
            if (_shelfStatusOutlineObject != null)
                _shelfStatusOutlineObject.SetActive(true);

            yield return new WaitForSeconds(ShelfPlacementFlashOnSeconds);

            if (_shelfStatusOutlineObject != null)
                _shelfStatusOutlineObject.SetActive(false);

            yield return new WaitForSeconds(ShelfPlacementFlashOffSeconds);
        }

        _shelfPlacementStatus = ShelfPlacementStatus.None;
        ReleaseShelfStatusOutline();
        _shelfPlacementFlashRoutine = null;
        RefreshRenderMode();
    }

    System.Collections.IEnumerator ShelfRowCompleteRoutine()
    {
        EnsureCardVisual();
        EnsureShelfRowCompleteFill();
        RefreshRenderMode();

        float restScale = CardDimensions.WorldCardScale;
        float peakScale = restScale * ShelfRowCompletePulseScale;
        float padding = GetShelfSurfacePadding();
        float elapsed = 0f;
        float total = ShelfRowCompleteUpSeconds + ShelfRowCompleteDownSeconds;

        while (elapsed < total && _handState == HandState.World)
        {
            elapsed += Time.deltaTime;
            float t;
            if (elapsed <= ShelfRowCompleteUpSeconds)
                t = Mathf.Clamp01(elapsed / ShelfRowCompleteUpSeconds);
            else
                t = 1f - Mathf.Clamp01((elapsed - ShelfRowCompleteUpSeconds) / ShelfRowCompleteDownSeconds);

            float scale = Mathf.Lerp(restScale, peakScale, Mathf.SmoothStep(0f, 1f, t));
            ApplyShelfPulseScale(scale, padding);
            yield return null;
        }

        ApplyShelfPulseScale(restScale, padding);
        ReleaseShelfRowCompleteFill();
        _shelfRowCompleteRoutine = null;
        RefreshRenderMode();
    }

    void StopShelfRowCompleteFeedback(bool restorePose)
    {
        if (_shelfRowCompleteRoutine != null)
        {
            StopCoroutine(_shelfRowCompleteRoutine);
            _shelfRowCompleteRoutine = null;
        }

        ReleaseShelfRowCompleteFill();
        if (restorePose && _handState == HandState.World && GetComponentInParent<CardShelfSlot>() != null)
            ApplyShelfPulseScale(CardDimensions.WorldCardScale, GetShelfSurfacePadding());
    }

    void ApplyShelfPulseScale(float scale, float padding)
    {
        transform.localScale = Vector3.one * scale;
        transform.localPosition = new Vector3(
            0f,
            CardDimensions.Height * 0.5f * scale + padding,
            0f);
    }

    float GetShelfSurfacePadding()
    {
        CardShelf shelf = GetComponentInParent<CardShelf>();
        return shelf != null ? shelf.SurfacePadding : 0.003f;
    }

    void EnsureShelfRowCompleteFill()
    {
        if (_shelfRowCompleteFill != null)
        {
            _shelfRowCompleteFill.SetActive(true);
            return;
        }

        CardArtLibrary.EnsureLoaded();
        _shelfRowCompleteFill = new GameObject("ShelfRowCompleteFill");
        _shelfRowCompleteFill.transform.SetParent(GetOutlineParent(), false);
        _shelfRowCompleteFill.transform.localPosition = new Vector3(0f, 0f, 0.0012f);
        _shelfRowCompleteFill.transform.localRotation = Quaternion.identity;
        _shelfRowCompleteFill.transform.localScale = Vector3.one;

        var meshFilter = _shelfRowCompleteFill.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CardArtLibrary.CardMesh;

        var meshRenderer = _shelfRowCompleteFill.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = CardVisualResources.ShelfRowCompleteFillMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    void ReleaseShelfRowCompleteFill()
    {
        if (_shelfRowCompleteFill == null)
            return;

        if (Application.isPlaying)
            Destroy(_shelfRowCompleteFill);
        else
            DestroyImmediate(_shelfRowCompleteFill);

        _shelfRowCompleteFill = null;
    }

    void RefreshRenderMode()
    {
        CardInstancedRenderManager.Instance?.Unregister(this);

        if (CanUseInstancedRendering || CanUseInstancedBackRendering)
        {
            ReleaseCardVisual();

            if (HasShelfPlacementFeedback)
            {
                EnsureShelfStatusOutline();
                ApplyShelfStatusOutlineMaterial(_shelfPlacementStatus);
                if (_shelfStatusOutlineObject != null)
                    _shelfStatusOutlineObject.SetActive(true);
                ReleaseInteractionOutline();
            }
            else if (_interactionHighlighted)
            {
                EnsureInteractionOutlineRenderer();
                if (_outlineObject != null)
                    _outlineObject.SetActive(true);
            }
            else
            {
                ReleaseInteractionOutline();
            }

            SetWorldColliderEnabled(false);
            CardInstancedRenderManager.Instance?.Register(this);
            return;
        }

        EnsureCardVisual();
        ApplyCardVisualTextureQuality();
        ApplyActiveVisualOrientation();

        RefreshInteractionOutline();
    }

    void ApplyActiveVisualOrientation()
    {
        if (UsesPsaSlab && _psaController != null)
        {
            ApplyPsaVisualOrientation();
            return;
        }

        if (_cardVisual == null)
            return;

        if (_handState == HandState.PackReveal)
        {
            ApplyRevealVisualOrientation(_packRevealFlipT);
            return;
        }

        if (_handState == HandState.Held || _handState == HandState.FlyingToHand)
        {
            ApplyHandVisualOrientation();
            return;
        }

        if (_handState == HandState.FlyingToShelf)
        {
            ApplyShelfVisualOrientation();
            return;
        }

        if (GetComponentInParent<CardShelfSlot>() != null)
        {
            ApplyShelfVisualOrientation();
            return;
        }

        ApplyWorldVisualOrientation();
    }

    void ApplyPsaVisualOrientation()
    {
        if (_handState == HandState.Held || _handState == HandState.FlyingToHand)
        {
            _psaController.ApplyHandOrientation();
            return;
        }

        if (_psaCabinetPlaced || IsInPsaCabinetSlot() || (_handState == HandState.FlyingToShelf && _usePsaCabinetPlacement))
        {
            _psaController.ApplyCabinetSlotOrientation();
            return;
        }

        _psaController.ApplyWorldOrientation(alignModelToGround: !HasActivePhysics);
        _psaController.ApplyBodyCollider();
    }

    bool IsInPsaCabinetSlot() => GetComponentInParent<PsaCabinetSlot>() != null;

    void RefreshInteractionOutline()
    {
        if (_shelfRowCompleteRoutine != null)
        {
            ReleaseInteractionOutline();
            ReleaseHandSelectionOutline();
            ReleaseShelfStatusOutline();
            return;
        }

        if (UsesPsaSlab && _psaController != null)
        {
            PsaCabinetSlot cabinetSlot = GetComponentInParent<PsaCabinetSlot>();
            if (cabinetSlot != null)
            {
                ReleaseInteractionOutline();
                ReleaseHandSelectionOutline();
                ReleaseShelfStatusOutline();
                _psaController.DisableOutline();
                cabinetSlot.SetOccupiedCardAimOutline(_interactionHighlighted);
                return;
            }

            ReleaseInteractionOutline();
            ReleaseHandSelectionOutline();

            if (_shelfPlacementFlashRoutine != null)
            {
                _psaController.DisableOutline();
                return;
            }

            if (HasShelfPlacementFeedback)
            {
                _psaController.DisableOutline();
                EnsureShelfStatusOutline();
                ApplyShelfStatusOutlineMaterial(_shelfPlacementStatus);
                if (_shelfStatusOutlineObject != null)
                    _shelfStatusOutlineObject.SetActive(true);
                return;
            }

            ReleaseShelfStatusOutline();
            _psaController.RefreshOutlineState(_interactionHighlighted, _handSelected);
            return;
        }

        if (_shelfPlacementFlashRoutine != null)
        {
            ReleaseInteractionOutline();
            return;
        }

        if (HasShelfPlacementFeedback)
        {
            ReleaseInteractionOutline();
            EnsureShelfStatusOutline();
            ApplyShelfStatusOutlineMaterial(_shelfPlacementStatus);
            if (_shelfStatusOutlineObject != null)
                _shelfStatusOutlineObject.SetActive(true);
            return;
        }

        ReleaseShelfStatusOutline();

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

    /// <summary>
    /// Ground setup for play. Scene-authored Grabbit cards keep their mesh and world pose.
    /// Only leftover runtime scatter cards that are clearly face-up may GPU-instance.
    /// </summary>
    public void RegisterForInstancedGround()
    {
        if (IsInHand)
            return;

        CardInstancedRenderManager.EnsureExists();
        ReleaseInteractionOutline();
        ReleaseHandSelectionOutline();
        ReleaseShelfStatusOutline();
        SetWorldColliderEnabled(false);
        RefreshRenderMode();
        CardGroundStack.Track(this);
    }

    void TryRegisterInstancedRendering()
    {
        CardInstancedRenderManager.EnsureExists();
        RefreshRenderMode();
    }

    void EnsureCardVisual()
    {
        if (UsesPsaSlab)
        {
            if (_psaController == null)
            {
                _psaController = new PsaCardVisualController(this);
                _psaController.Build(psaSlotNumber, psaVariantIndex);
            }
            else
                _psaController.EnsureVisual();

            _cardVisual = _psaController.CardRef;
            return;
        }

        BindExistingCardVisual();
        if (_cardVisual != null)
            return;

        CardArtLibrary.EnsureLoaded();

        var visualGo = new GameObject("CardVisual");
        visualGo.transform.SetParent(transform, false);
        visualGo.transform.localRotation = CardArtLibrary.WorldVisualRotation;
        CardLayers.ApplyToGameObject(visualGo);

        var meshFilter = visualGo.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CardArtLibrary.CardMesh;

        var meshRenderer = visualGo.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        _cardVisual = visualGo.transform;
        ApplyCardVisualTextureQuality();
    }

    void BindExistingCardVisual()
    {
        if (_cardVisual == null)
        {
            Transform found = transform.Find("CardVisual");
            if (found != null)
                _cardVisual = found;
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null || !child.name.StartsWith("CardVisual"))
                continue;

            if (_cardVisual == null)
                _cardVisual = child;
            else if (child != _cardVisual)
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }
    }

    void RestoreCardVisualMeshAndRenderer()
    {
        if (_cardVisual == null)
            return;

        MeshFilter meshFilter = _cardVisual.GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = _cardVisual.gameObject.AddComponent<MeshFilter>();
        if (CardArtLibrary.CardMesh != null)
            meshFilter.sharedMesh = CardArtLibrary.CardMesh;

        MeshRenderer meshRenderer = _cardVisual.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = _cardVisual.gameObject.AddComponent<MeshRenderer>();

        meshRenderer.enabled = true;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    void ApplyCardVisualTextureQuality()
    {
        if (_cardVisual == null)
            return;

        var meshRenderer = _cardVisual.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            return;

        // Mesh cards stay opaque Geometry (hand packs same). Ground instanced quads use World/2501.
        Material[] materials = UsesDefinitionFrontArt
            ? CardArtLibrary.GetCardMaterials(definition, CardTextureQuality.Detail)
            : CardArtLibrary.GetCardMaterials(paletteIndex, CardTextureQuality.Detail);

        Texture2D frontTexture = UsesDefinitionFrontArt ? definition.FrontTexture : null;
        for (int i = 0; i < materials.Length; i++)
        {
            CardArtLibrary.ConfigureHandDetailMaterial(materials[i]);
            if (CardArtLibrary.IsBrokenMaterial(materials[i]))
            {
                materials[i] = CardArtLibrary.CreateFallbackLitMaterial(
                    i == 0 ? frontTexture : null,
                    i == 0 ? "CardFrontFallback" : "CardBackFallback");
            }
        }

        meshRenderer.enabled = true;
        meshRenderer.sharedMaterials = materials;
    }

    void ReleaseCardVisual()
    {
        ReleaseInteractionOutline();
        ReleaseHandSelectionOutline();

        if (UsesPsaSlab && _psaController != null)
        {
            _psaController.ReleaseVisual();
            _cardVisual = null;
            return;
        }

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
        {
            if (_cardVisual == null)
                _outlineObject.transform.localPosition = Vector3.up * GetOutlineLift();
            return;
        }

        _outlineObject = new GameObject("InteractionOutline");
        Transform outlineParent = _cardVisual != null ? _cardVisual : transform;
        _outlineObject.transform.SetParent(outlineParent, false);
        if (_cardVisual == null)
        {
            _outlineObject.transform.localRotation = CardArtLibrary.WorldVisualRotation;
            _outlineObject.transform.localPosition = Vector3.up * GetOutlineLift();
        }

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

    /// <summary>
    /// Per-layer tiebreak only. The frame mesh already spans the card's own thickness, so lifting it any
    /// further would stand it over a card resting on this one and hide that card's edge.
    /// </summary>
    float GetOutlineLift()
    {
        return _groundStackLayer * 0.00025f;
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

    void EnsureShelfStatusOutline()
    {
        if (_shelfStatusOutlineObject != null)
            return;

        _shelfStatusOutlineObject = new GameObject("ShelfStatusOutline");
        _shelfStatusOutlineObject.transform.SetParent(GetOutlineParent(), false);

        var meshFilter = _shelfStatusOutlineObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CardVisualResources.InteractionBorderFrameMesh;

        var meshRenderer = _shelfStatusOutlineObject.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    void ApplyShelfStatusOutlineMaterial(ShelfPlacementStatus status, Material overrideMaterial = null)
    {
        if (_shelfStatusOutlineObject == null)
            return;

        var meshRenderer = _shelfStatusOutlineObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            return;

        if (overrideMaterial != null)
        {
            meshRenderer.sharedMaterial = overrideMaterial;
            return;
        }

        Material material = status == ShelfPlacementStatus.Correct
            ? CardVisualResources.ShelfCorrectOutlineMaterial
            : CardVisualResources.ShelfIncorrectOutlineMaterial;
        meshRenderer.sharedMaterial = material;
    }

    void ReleaseShelfStatusOutline()
    {
        if (_shelfStatusOutlineObject == null)
            return;

        if (Application.isPlaying)
            Destroy(_shelfStatusOutlineObject);
        else
            DestroyImmediate(_shelfStatusOutlineObject);

        _shelfStatusOutlineObject = null;
    }

    /// <summary>
    /// Places the card face-up flat on the ground without physics.
    /// </summary>
    public void PlaceOnSurface(Transform parent, Vector3 worldPosition, Quaternion worldRotation)
    {
        _handState = HandState.World;
        ClearShelfPlacementStatus();
        SetInteractionHighlight(false);
        SetHandSelected(false);
        RemovePhysics();
        _scaleTransitionActive = false;

        EnsureCardVisual();
        ConvertHandVisualToWorldRoot();

        if (_collider != null)
        {
            _collider.enabled = true;
            if (_collider is BoxCollider boxCollider)
                CardCollisionUtility.ApplyFlatWorldSize(boxCollider);
        }

        transform.SetParent(parent, true);
        transform.SetPositionAndRotation(worldPosition, worldRotation);
        transform.localScale = Vector3.one * CardDimensions.GroundCardScale;

        if (!UsesPsaSlab)
            ReleaseCardVisual();

        RefreshRenderMode();
    }

    /// <summary>
    /// Parents a PSA slab into a cabinet holder anchor using a tuned local pose.
    /// </summary>
    public void PlaceOnPsaCabinetSlot(
        Transform anchor,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale)
    {
        if (anchor == null)
            return;

        _psaCabinetPlaced = true;
        _handState = HandState.World;
        SetInteractionHighlight(false);
        SetHandSelected(false);
        RemovePhysics();
        _scaleTransitionActive = false;

        if (_collider != null)
            _collider.enabled = false;

        transform.SetParent(anchor, false);
        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
        transform.localScale = localScale;

        if (UsesPsaSlab && _psaController != null)
            _psaController.ApplyCabinetSlotOrientation();

        CardInstancedRenderManager.ReleaseFromGround(this);
        SetPlayerAimFocus(false);
        CardGroundQuery.TrackShelfCard(this);
        RefreshRenderMode();
    }

    /// <summary>
    /// Places the card using the slot marker's full rotation (tilt, yaw, etc.).
    /// Card local axes match the slot: +Y = height, +Z = face, bottom pivot at origin.
    /// </summary>
    public void PlaceOnShelfSlot(Transform slot, float surfacePadding)
    {
        if (slot == null)
            return;

        _handState = HandState.World;
        SetInteractionHighlight(false);
        SetHandSelected(false);
        RemovePhysics();
        _scaleTransitionActive = false;

        CardInstancedRenderManager.ReleaseFromGround(this);

        EnsureCardVisual();
        ApplyShelfVisualOrientation();
        ApplyCardVisualTextureQuality();

        if (_collider != null && _collider is BoxCollider boxCollider)
            CardCollisionUtility.ApplyUprightShelfSize(boxCollider);

        transform.SetParent(slot, false);
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one * CardDimensions.WorldCardScale;
        // CardMesh is center-pivoted. localScale is already WorldCardScale, so half-height in
        // parent space is Height * 0.5 * scale — omit the scale and the card sinks into the slot.
        transform.localPosition = new Vector3(
            0f,
            CardDimensions.Height * 0.5f * CardDimensions.WorldCardScale + surfacePadding,
            0f);

        CardGroundQuery.TrackShelfCard(this);
        SetPlayerAimFocus(false);
        RefreshRenderMode();
    }

    public void RefreshShelfVisualAfterLoad()
    {
        CardArtLibrary.EnsureLoaded();
        CardInstancedRenderManager.ReleaseFromGround(this);
        EnsureCardVisual();
        ApplyShelfVisualOrientation();
        ApplyCardVisualTextureQuality();
        if (_cardVisual == null)
            return;

        if (!_cardVisual.gameObject.activeSelf)
            _cardVisual.gameObject.SetActive(true);

        MeshRenderer renderer = _cardVisual.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.enabled = true;

        if (transform.parent != null)
        {
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * CardDimensions.WorldCardScale;
            CardShelf shelf = GetComponentInParent<CardShelf>();
            float padding = shelf != null ? shelf.SurfacePadding : 0.003f;
            transform.localPosition = new Vector3(
                0f,
                CardDimensions.Height * 0.5f * CardDimensions.WorldCardScale + padding,
                0f);
        }

        RefreshRenderMode();
    }
}
