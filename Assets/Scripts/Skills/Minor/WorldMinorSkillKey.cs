using UnityEngine;
using System.Collections.Generic;
using System.Collections;

[System.Serializable]
public sealed class MinorKeyGroundRecord
{
    public int index;
    public Vector3 position;
    public Quaternion rotation;
}


/// <summary>Authored key with held inventory and saved physical drops.</summary>
[DisallowMultipleComponent]
public sealed class WorldMinorSkillKey : MonoBehaviour, IInteractable, IInteractionHighlight
{
    [SerializeField, Range(0, MinorSkillProgress.Count - 1)] int skillIndex;
    [SerializeField] Transform visualRoot;
    [SerializeField] Sprite inspectSprite;
    public Sprite InspectSprite => inspectSprite;
    Rigidbody _dropBody;
    Coroutine _settleRoutine;
    Vector3 _authoredPosition;
    Quaternion _authoredRotation;
    bool _dropped;
    static MinorKeyGroundRecord[] _restoredGround;
    Collider[] _colliders;
    bool[] _colliderEnabled;
    Renderer[] _fallbackRenderers;
    bool[] _rendererEnabled;
    bool _canToggleVisual;
    Outline _outline;
    bool _aimed, _selected, _usingLock;
    public Transform VisualRoot => visualRoot;
    public bool IsUsingLock => _usingLock;

    static readonly HashSet<WorldMinorSkillKey> Active = new HashSet<WorldMinorSkillKey>();
    Transform _originalParent, _handParent;
    Vector3 _originalPosition, _originalScale, _flightPosition, _flightScale;
    Quaternion _originalRotation, _flightRotation;
    Bounds _visualBounds;
    float _flight;
    public bool IsHeld { get; private set; }
    public int SkillIndex => skillIndex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry() { Active.Clear(); _restoredGround = null; }

    public static MinorKeyGroundRecord[] CaptureGroundStates()
    {
        var result = new List<MinorKeyGroundRecord>(4);
        foreach (var key in Active)
            if (key != null && key._dropped)
                result.Add(new MinorKeyGroundRecord { index = key.skillIndex, position = key.transform.position, rotation = key.transform.rotation });
        return result.ToArray();
    }

    public static void RestoreGroundStates(MinorKeyGroundRecord[] records)
    {
        _restoredGround = records;
        foreach (var key in Active) if (key != null) key.RestoreGroundPose();
    }

    void RestoreGroundPose()
    {
        StopDropPhysics();
        DetachFromHand();
        _dropped = false;
        transform.SetPositionAndRotation(_authoredPosition, _authoredRotation);
        if (_restoredGround == null) return;
        foreach (var record in _restoredGround)
            if (record != null && record.index == skillIndex)
            {
                _dropped = true;
                transform.SetPositionAndRotation(record.position, record.rotation);
                // A save can be taken while the key is still falling.
                StartDropPhysics(Vector3.zero);
                break;
            }
    }

    public bool DropFromHand(Vector3 velocity)
    {
        if (!IsHeld || _usingLock || visualRoot == null || !MinorSkillProgress.HasKey(skillIndex)) return false;
        Vector3 position = visualRoot.position;
        Quaternion rotation = visualRoot.rotation * Quaternion.Inverse(_originalRotation);
        if (!MinorSkillProgress.TryDropKey(skillIndex)) return false;
        transform.SetPositionAndRotation(position - rotation * Vector3.Scale(_originalPosition, transform.lossyScale), rotation);
        _dropped = true;
        StartDropPhysics(velocity);
        return true;
    }

    void StartDropPhysics(Vector3 velocity)
    {
        if (_dropBody == null)
        {
            _dropBody = gameObject.AddComponent<Rigidbody>();
            _dropBody.mass = .08f;
            _dropBody.linearDamping = .35f;
            _dropBody.angularDamping = 1.5f;
            _dropBody.interpolation = RigidbodyInterpolation.Interpolate;
        }
        _dropBody.isKinematic = false;
        _dropBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _dropBody.linearVelocity = velocity;
        _dropBody.angularVelocity = Vector3.zero;
        if (_settleRoutine != null) StopCoroutine(_settleRoutine);
        _settleRoutine = StartCoroutine(WaitForRest());
    }

    IEnumerator WaitForRest()
    {
        var delay = new WaitForSeconds(.25f);
        do { yield return delay; } while (_dropBody != null && !_dropBody.isKinematic && !_dropBody.IsSleeping());
        _settleRoutine = null;
        GameSaveDirtyTracker.MarkDirty();
    }

    void StopDropPhysics()
    {
        if (_settleRoutine != null) { StopCoroutine(_settleRoutine); _settleRoutine = null; }
        if (_dropBody == null) return;
        if (!_dropBody.isKinematic) { _dropBody.linearVelocity = Vector3.zero; _dropBody.angularVelocity = Vector3.zero; }
        _dropBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
        _dropBody.isKinematic = true;
    }
    public static void BindOwnedKeys()
    {
        foreach (var key in Active) if (key != null) key.Refresh();
    }

    public void AttachToHand(Transform anchor)
    {
        if (IsHeld || !_canToggleVisual) return;
        StopDropPhysics();
        _flightPosition = visualRoot.position;
        _flightRotation = visualRoot.rotation;
        _flightScale = visualRoot.lossyScale;
        _handParent = anchor;
        visualRoot.SetParent(anchor, true);
        visualRoot.gameObject.SetActive(true);
        _flight = 0f;
        IsHeld = true;
    }

    void DetachFromHand()
    {
        if (!IsHeld) return;
        PlayerCardHand.Instance?.RemoveHeldKey(this);
        IsHeld = false;
        if (visualRoot == null) return;
        visualRoot.SetParent(_originalParent, false);
        visualRoot.localPosition = _originalPosition;
        visualRoot.localRotation = _originalRotation;
        visualRoot.localScale = _originalScale;
    }

    public void ApplyHandPose(Transform anchor, HandCardPose pose, bool selected)
    {
        if (!IsHeld || visualRoot == null || _usingLock) return;
        SetOutlineState(false, selected);
        float scale = pose.Scale * CardDimensions.Height * 1.1f / Mathf.Max(.001f, _visualBounds.size.x);
        Quaternion rotation = anchor.rotation * pose.LocalRotation * Quaternion.Euler(0, 90, 0);
        Vector3 position = anchor.TransformPoint(pose.LocalPosition + Vector3.up * (CardDimensions.Height * pose.Scale * .28f)) - rotation * (_visualBounds.center * scale);
        _flight = Mathf.Min(1f, _flight + Time.deltaTime / .4f);
        float t = Mathf.SmoothStep(0, 1, _flight);
        visualRoot.SetPositionAndRotation(Vector3.Lerp(_flightPosition, position, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .12f), Quaternion.Slerp(_flightRotation, rotation, t));
        visualRoot.localScale = Vector3.Lerp(_flightScale, Vector3.one * scale, t);
    }

    public void SetInteractionHighlight(bool highlighted) => SetOutlineState(highlighted && !IsHeld, _selected && IsHeld);

    void SetOutlineState(bool aimed, bool selected)
    {
        if (_outline == null || (_aimed == aimed && _selected == selected)) return;
        _aimed = aimed; _selected = selected;
        _outline.OutlineColor = aimed ? new Color(1f, .85f, 0f) : Color.black;
        _outline.OutlineWidth = aimed ? 3f : 2.2f;
        _outline.enabled = aimed || selected;
    }

    public void BeginLockUse()
    {
        _usingLock = true;
        SetOutlineState(false, false);
    }

    public void EndLockUse()
    {
        _usingLock = false;
        if (IsHeld && visualRoot != null && _handParent != null) visualRoot.SetParent(_handParent, true);
        // Restore a cancelled flight to the hand, or hide a consumed key.
        if (visualRoot != null)
        {
            _flightPosition = visualRoot.position; _flightRotation = visualRoot.rotation;
            _flightScale = visualRoot.lossyScale; _flight = 0f;
        }
        Refresh();
    }

    public void HideDuringOpening() { if (visualRoot != null) visualRoot.gameObject.SetActive(false); }

    public Vector3 LockTipLocal => new Vector3(_visualBounds.max.x, _visualBounds.center.y, _visualBounds.center.z);
    public float WorldModelScale => transform.lossyScale.x * _originalScale.x;

    // Called by the editor placeholder builder; also usable when replacing the model.
    public void Configure(int index, Transform visual)
    {
        skillIndex = index;
        visualRoot = visual;
        if (Application.isPlaying)
        {
            if (_colliders == null) CachePresentation();
            _canToggleVisual = visualRoot != null && visualRoot != transform && visualRoot.IsChildOf(transform);
            Refresh();
        }
    }

    void Awake()
    {
        _authoredPosition = transform.position;
        _authoredRotation = transform.rotation;
        CachePresentation();
        RestoreGroundPose();
    }

    void OnEnable()
    {
        Active.Add(this);
        MinorSkillProgress.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        MinorSkillProgress.Changed -= Refresh;
        Active.Remove(this);
        StopDropPhysics();
        _usingLock = false;
        SetOutlineState(false, false);
        DetachFromHand();
    }

    void CachePresentation()
    {
        _colliders = GetComponentsInChildren<Collider>(true);
        _colliderEnabled = new bool[_colliders.Length];
        for (int i = 0; i < _colliders.Length; i++) _colliderEnabled[i] = _colliders[i].enabled;
        // Never disable this component's root: it must keep receiving save restores.
        _canToggleVisual = visualRoot != null && visualRoot != transform && visualRoot.IsChildOf(transform);
        if (_canToggleVisual)
        {
            _originalParent = visualRoot.parent;
            _originalPosition = visualRoot.localPosition;
            _originalRotation = visualRoot.localRotation;
            _originalScale = visualRoot.localScale;
            bool first = true;
            foreach (var mesh in visualRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mesh.sharedMesh == null) continue;
                Bounds b = mesh.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    Vector3 point = visualRoot.InverseTransformPoint(mesh.transform.TransformPoint(corner));
                    if (first) { _visualBounds = new Bounds(point, Vector3.zero); first = false; }
                    else _visualBounds.Encapsulate(point);
                }
            }
        }
        _outline = visualRoot != null ? visualRoot.GetComponent<Outline>() : null;
        _fallbackRenderers = GetComponentsInChildren<Renderer>(true);
        _rendererEnabled = new bool[_fallbackRenderers.Length];
        for (int i = 0; i < _fallbackRenderers.Length; i++) _rendererEnabled[i] = _fallbackRenderers[i].enabled;
    }

    void Refresh()
    {
        if (_usingLock && MinorSkillProgress.Ready && MinorSkillProgress.HasKey(skillIndex)) return;
        SetOutlineState(false, false);
        if (!MinorSkillProgress.Ready || MinorSkillProgress.IsUnlocked(skillIndex)) StopDropPhysics();
        bool visible = MinorSkillProgress.Ready && MinorSkillProgress.IsValidIndex(skillIndex)
            && !MinorSkillProgress.HasKey(skillIndex) && !MinorSkillProgress.IsUnlocked(skillIndex);
        bool owned = MinorSkillProgress.Ready && MinorSkillProgress.HasKey(skillIndex) && !MinorSkillProgress.IsUnlocked(skillIndex);
        if (owned && _canToggleVisual && PlayerCardHand.Instance != null) PlayerCardHand.Instance.AddHeldKey(this);
        else DetachFromHand();
        if (_canToggleVisual) visualRoot.gameObject.SetActive(visible || IsHeld);
        else if (_fallbackRenderers != null)
            for (int i = 0; i < _fallbackRenderers.Length; i++)
                if (_fallbackRenderers[i] != null) _fallbackRenderers[i].enabled = visible && _rendererEnabled[i];

        if (_colliders != null)
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] != null) _colliders[i].enabled = visible && _colliderEnabled[i];
    }

    public string GetPromptText()
    {
        if (!MinorSkillInteraction.CanInteract() || !MinorSkillProgress.IsValidIndex(skillIndex)
            || MinorSkillProgress.HasKey(skillIndex) || MinorSkillProgress.IsUnlocked(skillIndex))
            return string.Empty;
        return InteractPrompt.Format(Localization.Format("minor.key.pickup", skillIndex + 1));
    }

    public void Interact(GameObject interactor)
    {
        if (MinorSkillInteraction.CanInteract()) MinorSkillProgress.TryCollectKey(skillIndex);
    }
}
