using System.Collections;
using UnityEngine;

/// <summary>Matching key unlocks exactly one permanent minor-skill slot.</summary>
[DisallowMultipleComponent]
public sealed class WorldMinorSkillChest : MonoBehaviour, IInteractable
{
    [SerializeField, Range(0, MinorSkillProgress.Count - 1)] int skillIndex;
    [SerializeField] Transform lid;
    [SerializeField] MinorChestModelView modelView;
    [SerializeField] Quaternion closedLidRotation = Quaternion.identity;
    [SerializeField] float openAngle = 100f;
    [SerializeField, Min(.05f)] float openDuration = .45f;
    [SerializeField] Transform keySocket;
    Vector3 _authoredScale;
    AssemblePickupTrail.Emitter _keyTrail;
    Coroutine _opening;
    Coroutine _unlockRoutine;
    WorldMinorSkillKey _keyInUse;
    PlayerCardHand _lockedHand;
    float _warningUntil;
    string _warningKey;
    public bool IsUnlocking => _unlockRoutine != null;
    public Transform KeySocket => keySocket;
    public void ConfigureSocket(Transform socket) => keySocket = socket;
    bool _unlockingHere;
    bool _awaitingNotice;
    bool _closing;
    Collider[] _colliders;
    bool[] _colliderEnabled;
    GameObject[] _visuals;
    bool[] _visualEnabled;

    void Awake()
    {
        _authoredScale = transform.localScale;
        _colliders = GetComponentsInChildren<Collider>(true);
        _colliderEnabled = new bool[_colliders.Length];
        for (int i = 0; i < _colliders.Length; i++) _colliderEnabled[i] = _colliders[i].enabled;
        _visuals = new GameObject[transform.childCount];
        _visualEnabled = new bool[_visuals.Length];
        for (int i = 0; i < _visuals.Length; i++)
        {
            _visuals[i] = transform.GetChild(i).gameObject;
            _visualEnabled[i] = _visuals[i].activeSelf;
        }
    }

    void SetVisible(bool visible)
    {
        if (_visuals == null) return;
        for (int i = 0; i < _visuals.Length; i++) if (_visuals[i] != null) _visuals[i].SetActive(visible && _visualEnabled[i]);
        for (int i = 0; i < _colliders.Length; i++) if (_colliders[i] != null) _colliders[i].enabled = visible && _colliderEnabled[i];
    }

    public int SkillIndex => skillIndex;
    public Transform Lid => lid;
    public MinorChestModelView ModelView => modelView;
    public void ConfigureModel(MinorChestModelView view) => modelView = view;

    public void Configure(int index, Transform lidPivot)
    {
        skillIndex = index;
        lid = lidPivot;
        closedLidRotation = lid != null ? lid.localRotation : Quaternion.identity;
        if (Application.isPlaying) Refresh();
    }

    void OnEnable()
    {
        MinorSkillProgress.Changed += Refresh;
        MinorSkillUnlockView.Dismissed += OnNoticeDismissed;
        Refresh();
    }

    void OnDisable()
    {
        MinorSkillProgress.Changed -= Refresh;
        MinorSkillUnlockView.Dismissed -= OnNoticeDismissed;
        _awaitingNotice = false;
        StopUnlocking();
        StopOpening();
    }

    Quaternion OpenRotation => closedLidRotation * Quaternion.Euler(openAngle, 0f, 0f);

    void Refresh()
    {
        if (_unlockingHere) return;
        if (_unlockRoutine != null && MinorSkillProgress.Ready && !GameSceneLoader.IsLoading && MinorSkillProgress.HasKey(skillIndex)) return;
        StopUnlocking();
        // A collected key for another chest must not interrupt this one's presentation.
        if ((_awaitingNotice || _closing) && MinorSkillProgress.Ready && MinorSkillProgress.IsUnlocked(skillIndex)) return;
        _awaitingNotice = false;
        StopOpening();
        SetVisible(!MinorSkillProgress.IsUnlocked(skillIndex));
        if (modelView != null)
        {
            modelView.SetOpen(false);
            return;
        }
        if (lid != null)
            lid.localRotation = closedLidRotation;
    }

    void StopOpening()
    {
        if (_opening != null) StopCoroutine(_opening);
        _opening = null;
        _closing = false;
        transform.localScale = _authoredScale;
    }

    public string GetPromptText()
    {
        if (!MinorSkillInteraction.CanInteract() || !MinorSkillProgress.IsValidIndex(skillIndex)) return string.Empty;
        if (MinorSkillProgress.IsUnlocked(skillIndex) || IsUnlocking) return string.Empty;
        if (Time.unscaledTime < _warningUntil) return "<color=#FF5555>" + Localization.Get(_warningKey) + "</color>";
        if (!MinorSkillProgress.HasKey(skillIndex)) return Localization.Format("minor.chest.locked", skillIndex + 1);
        return InteractPrompt.Format(Localization.Format("minor.chest.open", skillIndex + 1));
    }

    public void Interact(GameObject interactor)
    {
        if (!MinorSkillInteraction.CanInteract() || IsUnlocking || MinorSkillProgress.IsUnlocked(skillIndex)) return;
        if (!MinorSkillProgress.ChestIntroductionSeen)
        {
            if (MinorSkillUnlockView.Instance != null && MinorSkillUnlockView.Instance.ShowChestIntroduction())
                MinorSkillProgress.MarkChestIntroductionSeen();
            return;
        }
        var hand = PlayerCardHand.Instance;
        var key = hand != null ? hand.SelectedHeldKey : null;
        if (key == null || key.SkillIndex != skillIndex || !MinorSkillProgress.HasKey(skillIndex))
        {
            _warningKey = key != null ? "minor.chest.wrong_key" : "minor.chest.select_key";
            _warningUntil = Time.unscaledTime + 2.2f;
            return;
        }
        if (keySocket == null || modelView == null) return;
        _warningUntil = 0f;
        _lockedHand = hand; _keyInUse = key;
        hand.SetHandInputLocked(true);
        key.BeginLockUse();
        _unlockRoutine = StartCoroutine(UnlockSequence());
    }

    IEnumerator UnlockSequence()
    {
        Transform visual = _keyInUse.VisualRoot;
        _keyTrail = AssemblePickupTrail.Begin(visual);
        // Local +X is the key's tip. Point it into the front of the lock.
        Quaternion targetRotation = keySocket.rotation * Quaternion.Euler(0, -90, 0);
        float scale = _keyInUse.WorldModelScale;
        Vector3 tipOffset = targetRotation * (_keyInUse.LockTipLocal * scale);
        Vector3 inserted = keySocket.position - tipOffset;
        Vector3 approach = inserted - keySocket.forward * .09f;
        yield return MoveKey(visual, approach, targetRotation, scale, .65f, .12f);
        _keyTrail?.Finish();
        _keyTrail = null;
        yield return MoveKey(visual, inserted, targetRotation, scale, .2f, 0f);
        Quaternion turned = Quaternion.AngleAxis(90, keySocket.forward) * targetRotation;
        Vector3 turnedPosition = keySocket.position - turned * (_keyInUse.LockTipLocal * scale);
        yield return MoveKey(visual, turnedPosition, turned, scale, .3f, 0f);
        yield return new WaitForSeconds(.12f);
        _keyInUse.HideDuringOpening();
        yield return modelView.Open();
        _unlockingHere = true;
        bool unlocked;
        try { unlocked = MinorSkillProgress.TryUnlock(skillIndex); }
        finally { _unlockingHere = false; }
        _awaitingNotice = unlocked;
        _keyInUse.EndLockUse(); _keyInUse = null;
        if (_lockedHand != null) _lockedHand.SetHandInputLocked(false);
        _lockedHand = null; _unlockRoutine = null;
        if (!unlocked) Refresh();
    }

    IEnumerator MoveKey(Transform visual, Vector3 target, Quaternion rotation, float scale, float duration, float arc)
    {
        Vector3 start = visual.position;
        Quaternion startRotation = visual.rotation;
        Vector3 startScale = visual.localScale;
        // Detach the presentation only: its gameplay/save root never leaves its authored location.
        visual.SetParent(null, true);
        startScale = visual.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / duration));
            visual.SetPositionAndRotation(Vector3.Lerp(start, target, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * arc), Quaternion.Slerp(startRotation, rotation, t));
            visual.localScale = Vector3.Lerp(startScale, Vector3.one * scale, t);
            _keyTrail?.Follow(visual.position);
            yield return null;
        }
    }

    void StopUnlocking()
    {
        _keyTrail?.Finish(true);
        _keyTrail = null;
        if (_unlockRoutine != null) StopCoroutine(_unlockRoutine);
        _unlockRoutine = null;
        if (_keyInUse != null) { _keyInUse.EndLockUse(); _keyInUse = null; }
        if (_lockedHand != null) _lockedHand.SetHandInputLocked(false);
        _lockedHand = null;
    }

    void OnNoticeDismissed(int skill)
    {
        if (skill != skillIndex || !_awaitingNotice) return;
        _awaitingNotice = false;
        StopOpening();
        if (!MinorSkillProgress.Ready || GameSceneLoader.IsLoading) { Refresh(); return; }
        _closing = true;
        _opening = StartCoroutine(AnimateLid(false));
    }

    IEnumerator ShrinkAway()
    {
        for (int i = 0; i < _colliders.Length; i++) if (_colliders[i] != null) _colliders[i].enabled = false;
        float elapsed = 0f;
        const float duration = .4f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = _authoredScale * (1f - Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        SetVisible(false);
        transform.localScale = _authoredScale;
    }

    IEnumerator AnimateLid(bool open)
    {
        if (modelView != null)
        {
            yield return open ? modelView.Open() : modelView.Close();
            if (!open) yield return ShrinkAway();
            _opening = null;
            _closing = false;
            yield break;
        }
        Quaternion start = lid.localRotation;
        Quaternion target = open ? OpenRotation : closedLidRotation;
        float duration = Mathf.Max(.05f, openDuration);
        float elapsed = 0f;
        while (elapsed < duration && lid != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - (1f - t) * (1f - t) * (1f - t);
            lid.localRotation = Quaternion.SlerpUnclamped(start, target, t);
            yield return null;
        }
        if (lid != null) lid.localRotation = target;
        if (!open) yield return ShrinkAway();
        _opening = null;
        _closing = false;
    }
}
