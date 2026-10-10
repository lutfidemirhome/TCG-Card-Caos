using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored, replaceable skill UI. All visible copy uses the game's localization table.</summary>
public sealed class SkillPanelView : MonoBehaviour
{
    public static SkillPanelView Instance { get; private set; }
    static int _closedFrame = -1;
    public static bool ConsumesPauseInput => (Instance != null && Instance.IsOpen) || _closedFrame == Time.frameCount || MinorSkillUnlockView.ConsumesInput;
    public bool IsOpen => _panel != null && _panel.activeSelf;
    GameObject _panel, _task, _bar;
    TMP_Text _title, _points, _instructions, _name, _description, _stats, _next, _taskTitle, _taskText;
    TMP_Text _closeText, _upgradeText, _openText, _selectedLevel;
    TMP_Text _minorTitle;
    readonly TMP_Text[] _minorNames = new TMP_Text[MinorSkillProgress.Count];
    readonly Image[] _minorIcons = new Image[MinorSkillProgress.Count];
    readonly GameObject[] _minorSelections = new GameObject[MinorSkillProgress.Count];
    readonly SkillPanelButtonFeedback[] _minorFeedback = new SkillPanelButtonFeedback[MinorSkillProgress.Count];
    readonly GameObject[] _majorDetails = new GameObject[7];
    Sprite _minorLock;
    [SerializeField] Sprite[] minorSkillIcons = new Sprite[MinorSkillProgress.Count];
    public Sprite GetMinorSkillIcon(int index) => index >= 0 && index < minorSkillIcons.Length
        ? minorSkillIcons[index] : null;
    int _selectedMinor = -1;
    Button _upgrade;
    Image _selectedIcon;
    [SerializeField] Sprite hotbarReadyBackground;
    [SerializeField] Sprite hotbarPassiveBackground;
    readonly TMP_Text[] _names = new TMP_Text[5], _levels = new TMP_Text[5], _barLabels = new TMP_Text[5];
    readonly Image[] _nodes = new Image[5];
    readonly Image[] _icons = new Image[5];
    readonly Image[] _barBackgrounds = new Image[5];
    readonly Image[] _barIcons = new Image[5], _barLocks = new Image[5], _barKeys = new Image[5], _barFills = new Image[5];
    readonly GameObject[] _barTimers = new GameObject[5];
    readonly float[] _barActiveDuration = new float[5];
    readonly bool[] _barActive = new bool[5];
    readonly GameObject[] _checks = new GameObject[5];
    readonly GameObject[] _selections = new GameObject[5];
    readonly SkillPanelButtonFeedback[] _nodeFeedback = new SkillPanelButtonFeedback[5];
    readonly Vector3[] _corners = new Vector3[4];
    RectTransform _hudStats, _root, _body, _taskRect, _taskOpen;
    float _refresh;
    int _selected, _revision = -1;
    readonly int[] _barState = { -1,-1,-1,-1,-1 };
    bool _pausedByUs;
    CursorLockMode _previousLock;
    bool _previousVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; _closedFrame = -1; }

    public static void Ensure(Transform hud)
    {
        MinorSkillUnlockView.Ensure(hud);
        if (Instance != null) return;
        GameObject prefab = Resources.Load<GameObject>("UI/Skills/SkillUI");
        if (prefab == null) { Debug.LogError("[Skills] Missing SkillUI prefab."); return; }
        GameObject obj = Instantiate(prefab, hud, false);
        obj.name = "SkillUI";
        if (obj.GetComponent<SkillPanelView>() == null) obj.AddComponent<SkillPanelView>();
    }

    TMP_Text TextAt(string path) => transform.Find(path).GetComponent<TMP_Text>();
    Button ButtonAt(string path) => transform.Find(path).GetComponent<Button>();
    void Awake()
    {
        Instance = this;
        UiEventSystem.Ensure();
        _panel = transform.Find("Panel").gameObject;
        _task = transform.Find("Task").gameObject;
        _bar = transform.Find("Hotbar").gameObject;
        _root = (RectTransform)transform;
        _body = (RectTransform)transform.Find("Panel/Body");
        _taskRect = (RectTransform)_task.transform;
        _taskOpen = (RectTransform)transform.Find("Task/Open");
        const string body = "Panel/Body/";
        _title = TextAt(body + "Title"); _points = TextAt(body + "Points");
        _instructions = TextAt(body + "Instructions");
        _name = TextAt(body + "Details/Name"); _description = TextAt(body + "Details/Description");
        _selectedLevel = TextAt(body + "Details/Level");
        _selectedIcon = transform.Find(body + "Details/IconFrame/Icon").GetComponent<Image>();
        _stats = TextAt(body + "Details/Stats"); _next = TextAt(body + "Details/Next");
        _upgrade = ButtonAt(body + "Details/Upgrade"); _upgradeText = TextAt(body + "Details/Upgrade/Label");
        _closeText = TextAt("Panel/Close/Label");
        _minorTitle = TextAt(body + "MinorSkills/Title");
        string[] majorDetailPaths = { "Level", "LevelFrame", "Stats", "Next", "StatsFrame", "StatsRule", "Upgrade" };
        for (int i = 0; i < majorDetailPaths.Length; i++)
            _majorDetails[i] = transform.Find(body + "Details/" + majorDetailPaths[i]).gameObject;
        _taskTitle = TextAt("Task/Title"); _taskText = TextAt("Task/Description"); _openText = TextAt("Task/Open/Label");
        ButtonAt("Panel/Close").onClick.AddListener(Close);
        SkillPanelButtonFeedback.Ensure(ButtonAt("Panel/Close"));
        SkillPanelButtonFeedback.Ensure(_upgrade);
        ButtonAt("Task/Open").onClick.AddListener(Open);
        _upgrade.onClick.AddListener(() => { if (_selectedMinor < 0 && SkillProgress.Upgrade(_selected)) Refresh(); });
        for (int i = 0; i < 5; i++)
        {
            int skill = i;
            string path = body + "Nodes/Skill" + i;
            _names[i] = TextAt(path + "/Name"); _levels[i] = TextAt(path + "/Level");
            _nodes[i] = transform.Find(path).GetComponent<Image>();
            _icons[i] = transform.Find(path + "/Icon").GetComponent<Image>();
            _checks[i] = transform.Find(path + "/Check").gameObject;
            _selections[i] = transform.Find(path + "/Selection").gameObject;
            _nodeFeedback[i] = SkillPanelButtonFeedback.Ensure(ButtonAt(path));
            ButtonAt(path).onClick.AddListener(() => { _selectedMinor = -1; _selected = skill; Refresh(); });
            _barLabels[i] = TextAt("Hotbar/Skill" + i + "/Label");
            _barBackgrounds[i] = transform.Find("Hotbar/Skill" + i).GetComponent<Image>();
            _barIcons[i] = transform.Find("Hotbar/Skill" + i + "/Icon").GetComponent<Image>();
            _barLocks[i] = transform.Find("Hotbar/Skill" + i + "/LockIcon").GetComponent<Image>();
            _barKeys[i] = transform.Find("Hotbar/Skill" + i + "/KeyHint").GetComponent<Image>();
            _barTimers[i] = transform.Find("Hotbar/Skill" + i + "/BarBackground").gameObject;
            _barFills[i] = transform.Find("Hotbar/Skill" + i + "/BarBackground/Fill").GetComponent<Image>();
        }
        _minorLock = _barLocks[0].sprite;
        for (int i = 0; i < MinorSkillProgress.Count; i++)
        {
            int minor = i;
            string path = body + "MinorSkills/Skill" + i;
            _minorNames[i] = TextAt(path + "/Name");
            _minorIcons[i] = transform.Find(path + "/Icon").GetComponent<Image>();
            _minorSelections[i] = transform.Find(path + "/Selection").gameObject;
            _minorFeedback[i] = SkillPanelButtonFeedback.Ensure(ButtonAt(path));
            ButtonAt(path).onClick.AddListener(() => { _selectedMinor = minor; Refresh(); });
        }
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true)) UiMenuFont.Apply(text);
        _hudStats = transform.parent != null ? transform.parent.Find("Panel_TopLeft") as RectTransform : null;
        _panel.SetActive(false);
        _task.SetActive(false);
        _bar.SetActive(false);
        if (GetComponent<CardSkillController>() == null) gameObject.AddComponent<CardSkillController>();
    }
    void OnEnable() { Localization.LanguageChanged += Refresh; MinorSkillProgress.Changed += Refresh; }
    void OnDisable() { Localization.LanguageChanged -= Refresh; MinorSkillProgress.Changed -= Refresh; Close(); }
    void OnDestroy() { if (Instance == this) Instance = null; }
    void Update()
    {
        bool gameplay = SkillProgress.Ready && CardInstancedRenderManager.IsGameplayReady && !GameSceneLoader.IsLoading && !WelcomePopupView.IsWaitingForStart;
        if (!gameplay) { Close(); SetVisible(_task, false); SetVisible(_bar, false); return; }
        if (IsOpen && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))) Close();
        else if (!GamePause.IsPaused && !MinorSkillUnlockView.ConsumesInput && Input.GetKeyDown(KeyCode.Tab)) Open();
        bool hudVisible = !GamePause.IsPaused;
        SetVisible(_task, hudVisible); SetVisible(_bar, hudVisible);
        _refresh -= Time.unscaledDeltaTime;
        if (_revision != SkillProgress.Revision || _refresh <= 0f)
        {
            _refresh = 0.25f;
            if (_revision != SkillProgress.Revision) { _revision = SkillProgress.Revision; Refresh(); }
            else RefreshHotbar(false);
            PositionTask();
        }
        if (hudVisible) RefreshHotbarFills();
    }
    static void SetVisible(GameObject obj, bool visible) { if (obj != null && obj.activeSelf != visible) obj.SetActive(visible); }
    public void Open()
    {
        if (IsOpen || GamePause.IsPaused || MinorSkillUnlockView.ConsumesInput || !SkillProgress.Ready || GameSceneLoader.IsLoading
            || !CardInstancedRenderManager.IsGameplayReady || WelcomePopupView.IsWaitingForStart) return;
        PlayerCardHand hand = PlayerCardHand.Instance;
        if (hand != null && (hand.IsHandInputLocked || hand.IsAwaitingRevealCollect)) return;
        _selected = (int)CardSkill.Sort;
        _selectedMinor = -1;
        _previousLock = Cursor.lockState; _previousVisible = Cursor.visible;
        _pausedByUs = true;
        GamePause.SetPaused(true); Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        _panel.SetActive(true); PositionTask(); Refresh();
    }
    public void Close()
    {
        if (!IsOpen && !_pausedByUs) return;
        SetVisible(_panel, false);
        _closedFrame = Time.frameCount;
        if (_pausedByUs)
        {
            GamePause.SetPaused(false); Cursor.lockState = _previousLock; Cursor.visible = _previousVisible;
            _pausedByUs = false;
        }
    }
    static void Set(TMP_Text label, string value) { if (label != null && label.text != value) label.text = value; }
    public void Refresh()
    {
        if (_title == null) return;
        Set(_title, Localization.Get(LocalizationKeys.SkillsTitle));
        Set(_points, Localization.Format(LocalizationKeys.SkillsPoints,
            "<color=#2E7D32>" + SkillProgress.Points + "</color>"));
        Set(_instructions, Localization.Get(LocalizationKeys.SkillsInstructions));
        Set(_closeText, Localization.Get(LocalizationKeys.PauseBack));
        Set(_openText, Localization.Get(LocalizationKeys.SkillsOpen));
        Set(_taskTitle, Localization.Get(LocalizationKeys.SkillsTaskTitle));
        int target = SkillProgress.NextGoal, points = SkillProgress.Points;
        string taskText = target > 0
            ? Localization.Format(LocalizationKeys.SkillsTaskRows, target, SkillProgress.CompletedRows)
            : Localization.Get(LocalizationKeys.SkillsTaskDone);
        Set(_minorTitle, Localization.Get("minor.title"));
        if (points > 0) taskText += "\n" + Localization.Format(LocalizationKeys.SkillsPoints, points);
        Set(_taskText, taskText);
        LayoutTask();
        for (int i = 0; i < 5; i++)
        {
            int level = SkillProgress.Level(i);
            string name = Localization.Get("skills." + SkillCatalog.Keys[i] + ".name");
            Set(_names[i], name);
            Set(_levels[i], Localization.Format(LocalizationKeys.SkillsLevel, level, SkillCatalog.MaxLevel(i)));
            SetVisible(_selections[i], _selectedMinor < 0 && i == _selected);
            _nodeFeedback[i].SetSelected(_selectedMinor < 0 && i == _selected);
            _levels[i].color = level > 0 ? new Color32(46, 111, 56, 255) : new Color32(94, 89, 77, 255);
            SetVisible(_checks[i], level > 0);
        }
        for (int i = 0; i < MinorSkillProgress.Count; i++)
        {
            bool unlocked = MinorSkillProgress.IsUnlocked(i);
            Set(_minorNames[i], Localization.Get(unlocked ? "minor.skill" + (i + 1) + ".name" : "minor.locked.name"));
            _minorIcons[i].sprite = unlocked ? GetMinorSkillIcon(i) : _minorLock;
            _minorIcons[i].color = Color.white;
            SetVisible(_minorSelections[i], i == _selectedMinor);
            _minorFeedback[i].SetSelected(i == _selectedMinor);
        }
        foreach (GameObject detail in _majorDetails) SetVisible(detail, _selectedMinor < 0);
        if (_selectedMinor >= 0)
        {
            bool unlocked = MinorSkillProgress.IsUnlocked(_selectedMinor);
            string minorKey = "minor.skill" + (_selectedMinor + 1);
            Set(_name, Localization.Get(unlocked ? minorKey + ".name" : "minor.locked.name"));
            Set(_description, Localization.Get(unlocked ? minorKey + ".description" : "minor.locked.description"));
            _selectedIcon.sprite = unlocked ? GetMinorSkillIcon(_selectedMinor) : _minorLock;
            _selectedIcon.color = Color.white;
            SetVisible(_next.gameObject, unlocked);
            Set(_next, Localization.Get("minor.unlocked.status"));
            _upgrade.interactable = false;
            RefreshHotbar(true);
            return;
        }
        int current = SkillProgress.Level(_selected), maximum = SkillCatalog.MaxLevel(_selected);
        string key = "skills." + SkillCatalog.Keys[_selected];
        Set(_name, Localization.Get(key + ".name"));
        Set(_selectedLevel, Localization.Format(LocalizationKeys.SkillsLevel, current, maximum));
        _selectedIcon.sprite = _icons[_selected].sprite;
        _selectedIcon.color = _icons[_selected].color;
        Set(_description, Localization.Get(key + ".description"));
        Set(_stats, Stats(_selected, current));
        Set(_next, current >= maximum ? Localization.Get(LocalizationKeys.SkillsMax)
            : current == 0 ? Localization.Get(LocalizationKeys.SkillsLocked) + " / " + Localization.Get(LocalizationKeys.SkillsNext)
            : Localization.Get(LocalizationKeys.SkillsNext) + " (" + current + " \u2192 " + (current + 1) + ")");
        Set(_upgradeText, Localization.Get(current >= maximum ? LocalizationKeys.SkillsMax : LocalizationKeys.SkillsUpgrade));
        _upgrade.interactable = points > 0 && current < maximum;
        RefreshHotbar(true);
    }
    void RefreshHotbar(bool force)
    {
        for (int i = 0; i < 5; i++)
        {
            int level = SkillProgress.Level(i), active = Mathf.CeilToInt(SkillProgress.ActiveTime(i)), cooldown = Mathf.CeilToInt(SkillProgress.Cooldown(i));
            int state = level | (cooldown << 5) | (active << 14);
            if (!force && _barState[i] == state) continue;
            _barState[i] = state;
            bool locked = level == 0, isActive = !locked && active > 0;
            bool waiting = !locked && !isActive && cooldown > 0;
            if (isActive && !_barActive[i])
            {
                // Assemble uses its real flight duration, not its card-count upgrade value.
                float duration = i >= (int)CardSkill.ShelfGuide ? SkillCatalog.Effect(i, level) : 0f;
                _barActiveDuration[i] = Mathf.Max(SkillProgress.ActiveTime(i), duration);
            }
            _barActive[i] = isActive;
            if (!isActive) _barActiveDuration[i] = 0f;
            SetVisible(_barIcons[i].gameObject, !locked && !waiting);
            SetVisible(_barLocks[i].gameObject, locked);
            SetVisible(_barLabels[i].gameObject, waiting);
            SetVisible(_barTimers[i], isActive);
            if (waiting) Set(_barLabels[i], Localization.Format(LocalizationKeys.SkillsCooldownShort, cooldown));
            _barKeys[i].color = waiting ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
            _barBackgrounds[i].sprite = locked || waiting ? hotbarPassiveBackground : hotbarReadyBackground;
        }
    }
    void RefreshHotbarFills()
    {
        // Five cached images; only active bars change geometry, with no per-frame text work.
        for (int i = 0; i < SkillCatalog.Count; i++)
            if (_barActive[i])
                _barFills[i].fillAmount = Mathf.Clamp01(SkillProgress.ActiveTime(i) / Mathf.Max(0.001f, _barActiveDuration[i]));
    }
    static string Stats(int skill, int level)
    {
        bool hasNext = level < SkillCatalog.MaxLevel(skill);
        string cooldown = StatValue(SkillCatalog.Cooldown(skill, level), SkillCatalog.Cooldown(skill, level + 1), level == 0, hasNext);
        string effect = StatValue(SkillCatalog.Effect(skill, level), SkillCatalog.Effect(skill, level + 1), level == 0, hasNext);
        if (skill >= 2) return Localization.Format(LocalizationKeys.SkillsStats, cooldown, effect).Replace(" / ", "\n");
        string text = Localization.Format(LocalizationKeys.SkillsWait, cooldown);
        return skill == 0 ? text + "\n" + Localization.Format(LocalizationKeys.SkillsAmount, effect) : text;
    }
    static string StatValue(int current, int next, bool locked, bool hasNext)
    {
        if (!hasNext) return current.ToString();
        return (locked ? "\u2014" : current.ToString()) + " \u2192 <color=#2E7D32>" + next + "</color>";
    }
    void LayoutTask()
    {
        // Relayout only when progress/language changes; keep readable text and grow downward.
        RectTransform description = _taskText.rectTransform;
        float textHeight = Mathf.Ceil(_taskText.GetPreferredValues(_taskText.text, description.rect.width, 0f).y);
        if (description.rect.height != textHeight)
            description.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight);
        float buttonSpace = _taskOpen.anchoredPosition.y + _taskOpen.rect.height * (1f - _taskOpen.pivot.y);
        float height = -description.anchoredPosition.y + textHeight + buttonSpace;
        if (_taskRect.rect.height != height)
            _taskRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }
    void PositionTask()
    {
        float fit = Mathf.Min(1f, Mathf.Min((_root.rect.width - 48f) / Mathf.Max(1f, _body.rect.width),
            (_root.rect.height - 160f) / Mathf.Max(1f, _body.rect.height)));
        Vector3 bodyScale = Vector3.one * Mathf.Max(0.1f, fit);
        if (_body.localScale != bodyScale) _body.localScale = bodyScale;
        if (_hudStats == null) return;
        _hudStats.GetWorldCorners(_corners);
        Vector3 bottomLeft = _root.InverseTransformPoint(_corners[0]);
        // Keep the task background flush with the screen, independent of the stats text inset.
        Vector2 position = new Vector2(0f, bottomLeft.y - _root.rect.yMax - 12f);
        if (_taskRect.anchoredPosition != position) _taskRect.anchoredPosition = position;
    }
}
