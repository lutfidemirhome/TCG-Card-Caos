using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored unlock notification. Waits for other modals and never replays loaded unlocks.</summary>
[DefaultExecutionOrder(-80)]
public sealed class MinorSkillUnlockView : MonoBehaviour
{
    public static MinorSkillUnlockView Instance { get; private set; }
    public static event System.Action<int> Dismissed;
    static int _closedFrame = -1;
    public static bool ConsumesInput => (Instance != null && Instance.IsOpen) || _closedFrame == Time.frameCount;
    public bool IsOpen => _panel != null && _panel.activeSelf;
    readonly Queue<int> _pending = new Queue<int>(MinorSkillProgress.Count);
    GameObject _panel;
    RectTransform _body, _root;
    TMP_Text _title, _message, _closeLabel;
    Image _icon;
    GameObject _iconBackground;
    [SerializeField] Sprite[] minorSkillIcons = new Sprite[MinorSkillProgress.Count];
    public Sprite GetMinorSkillIcon(int index) => index >= 0 && index < minorSkillIcons.Length
        ? minorSkillIcons[index] : null;
    int _skill = -1, _openedFrame = -1;
    bool _pausedByUs, _previousVisible;
    CursorLockMode _previousLock;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; _closedFrame = -1; Dismissed = null; }

    public static void Ensure(Transform hud)
    {
        if (Instance != null) return;
        var prefab = Resources.Load<GameObject>("UI/Skills/MinorSkillUnlock");
        if (prefab == null) { Debug.LogError("[Minor Skills] Missing unlock notification prefab."); return; }
        var obj = Instantiate(prefab, hud, false);
        obj.name = "MinorSkillUnlock";
    }

    void Awake()
    {
        Instance = this;
        _root = (RectTransform)transform;
        _panel = transform.Find("Panel").gameObject;
        _body = (RectTransform)transform.Find("Panel/Body");
        _title = transform.Find("Panel/Body/Title").GetComponent<TMP_Text>();
        _message = transform.Find("Panel/Body/Message").GetComponent<TMP_Text>();
        _icon = transform.Find("Panel/Body/Icon").GetComponent<Image>();
        _iconBackground = transform.Find("Panel/Body/IconBackground")?.gameObject;
        _closeLabel = transform.Find("Panel/Body/Close/Label").GetComponent<TMP_Text>();
        var close = transform.Find("Panel/Body/Close").GetComponent<Button>();
        close.onClick.AddListener(Close);
        SkillPanelButtonFeedback.Ensure(close);
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true)) UiMenuFont.Apply(text);
        _panel.SetActive(false);
    }

    void OnEnable()
    {
        MinorSkillProgress.Unlocked += QueueUnlock;
        Localization.LanguageChanged += Refresh;
    }

    void OnDisable()
    {
        MinorSkillProgress.Unlocked -= QueueUnlock;
        Localization.LanguageChanged -= Refresh;
        _pending.Clear();
        Close();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public bool ShowChestIntroduction()
    {
        if (IsOpen || _pending.Count != 0) return false;
        _pending.Enqueue(-2);
        return true;
    }

    void QueueUnlock(int skill)
    {
        if (skill < 0 || skill >= MinorSkillProgress.Count || _pending.Contains(skill)) return;
        _pending.Enqueue(skill);
    }

    void Update()
    {
        if (!MinorSkillProgress.Ready || GameSceneLoader.IsLoading)
        {
            _pending.Clear();
            Close();
            return;
        }
        if (IsOpen)
        {
            Fit();
            if (_openedFrame != Time.frameCount && Input.GetKeyDown(KeyCode.Escape)) Close();
            return;
        }
        if (_pending.Count == 0 || ConsumesInput || GamePause.IsPaused
            || !GameScenes.IsActiveGameScene() || !CardInstancedRenderManager.IsGameplayReady
            || WelcomePopupView.IsWaitingForStart || DemoCompleteView.IsBlockingGameplay
            || SkillPanelView.ConsumesPauseInput) return;
        var hand = PlayerCardHand.Instance;
        if (hand != null && (hand.IsHandInputLocked || hand.IsAwaitingRevealCollect)) return;
        _skill = _pending.Dequeue();
        _previousLock = Cursor.lockState;
        _previousVisible = Cursor.visible;
        _pausedByUs = true;
        GamePause.SetPaused(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        transform.SetAsLastSibling();
        _panel.SetActive(true);
        _openedFrame = Time.frameCount;
        UiEventSystem.Ensure();
        Refresh();
        Fit();
    }

    public void Close()
    {
        if (!IsOpen && !_pausedByUs) return;
        _panel.SetActive(false);
        _closedFrame = Time.frameCount;
        if (_pausedByUs)
        {
            _pausedByUs = false;
            // A quick-load can start while this modal is open. The loader now owns
            // pause/cursor state; do not relock its cursor during scene teardown.
            if (!GameSceneLoader.IsLoading)
            {
                GamePause.SetPaused(false);
                Cursor.lockState = _previousLock;
                Cursor.visible = _previousVisible;
            }
        }
        int dismissedSkill = _skill;
        _skill = -1;
        if (MinorSkillProgress.IsValidIndex(dismissedSkill)) Dismissed?.Invoke(dismissedSkill);
    }

    void Refresh()
    {
        if (_title == null || _skill == -1) return;
        bool introduction = _skill == -2;
        _icon.gameObject.SetActive(!introduction);
        if (_iconBackground != null) _iconBackground.SetActive(!introduction);
        _message.rectTransform.anchoredPosition = new Vector2(76f, introduction ? -148f : -280f);
        _message.rectTransform.sizeDelta = new Vector2(648f, introduction ? 220f : 100f);
        _message.enableAutoSizing = true;
        _message.fontSizeMin = 20f; _message.fontSizeMax = 28f;
        if (introduction)
        {
            _title.text = Localization.Get("minor.chest.intro.title");
            _message.text = Localization.Get("minor.chest.intro.message");
            _closeLabel.text = Localization.Get("minor.chest.intro.ok");
            return;
        }
        _title.text = Localization.Get("minor.unlock.title");
        string key = "minor.skill" + (_skill + 1);
        _icon.sprite = GetMinorSkillIcon(_skill);
        _message.text = Localization.Format("minor.unlock.message", Localization.Get(key + ".name"))
            + "\n" + Localization.Get(key + ".description");
        _closeLabel.text = Localization.Get("minor.unlock.close");
    }

    void Fit()
    {
        float fit = Mathf.Min(1f, Mathf.Min((_root.rect.width - 48f) / Mathf.Max(1f, _body.rect.width),
            (_root.rect.height - 100f) / Mathf.Max(1f, _body.rect.height)));
        Vector3 scale = Vector3.one * Mathf.Max(.1f, fit);
        if (_body.localScale != scale) _body.localScale = scale;
    }
}
