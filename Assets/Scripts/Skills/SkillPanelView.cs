using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored, replaceable skill UI. All visible copy uses the game's localization table.</summary>
public sealed class SkillPanelView : MonoBehaviour
{
    public static SkillPanelView Instance { get; private set; }
    static int _closedFrame = -1;
    public static bool ConsumesPauseInput => (Instance != null && Instance.IsOpen) || _closedFrame == Time.frameCount;
    public bool IsOpen => _panel != null && _panel.activeSelf;
    GameObject _panel, _task, _bar;
    TMP_Text _title, _points, _instructions, _name, _description, _stats, _next, _taskTitle, _taskText;
    TMP_Text _closeText, _upgradeText, _openText;
    Button _upgrade;
    [SerializeField] Color activeHotbarColor = new Color(0.22f, 0.42f, 0.28f, 0.92f);
    readonly TMP_Text[] _names = new TMP_Text[5], _levels = new TMP_Text[5], _barLabels = new TMP_Text[5];
    readonly Image[] _nodes = new Image[5];
    readonly Image[] _barBackgrounds = new Image[5];
    readonly Color[] _barIdleColors = new Color[5];
    readonly Image[][] _ranks = new Image[5][];
    readonly GameObject[] _checks = new GameObject[5];
    readonly Vector3[] _corners = new Vector3[4];
    RectTransform _hudStats, _root, _body, _taskRect;
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
        const string body = "Panel/Body/";
        _title = TextAt(body + "Title"); _points = TextAt(body + "Points");
        _instructions = TextAt(body + "Instructions");
        _name = TextAt(body + "Details/Name"); _description = TextAt(body + "Details/Description");
        _stats = TextAt(body + "Details/Stats"); _next = TextAt(body + "Details/Next");
        _upgrade = ButtonAt(body + "Details/Upgrade"); _upgradeText = TextAt(body + "Details/Upgrade/Label");
        _closeText = TextAt(body + "Close/Label");
        _taskTitle = TextAt("Task/Title"); _taskText = TextAt("Task/Description"); _openText = TextAt("Task/Open/Label");
        ButtonAt(body + "Close").onClick.AddListener(Close);
        ButtonAt("Task/Open").onClick.AddListener(Open);
        _upgrade.onClick.AddListener(() => { if (SkillProgress.Upgrade(_selected)) Refresh(); });
        for (int i = 0; i < 5; i++)
        {
            int skill = i;
            string path = body + "Nodes/Skill" + i;
            _names[i] = TextAt(path + "/Name"); _levels[i] = TextAt(path + "/Level");
            _nodes[i] = transform.Find(path).GetComponent<Image>();
            _checks[i] = transform.Find(path + "/Check").gameObject;
            _ranks[i] = new Image[SkillCatalog.MaxLevel(i)];
            for (int j = 0; j < _ranks[i].Length; j++) _ranks[i][j] = transform.Find(path + "/Rank" + j).GetComponent<Image>();
            ButtonAt(path).onClick.AddListener(() => { _selected = skill; Refresh(); });
            _barLabels[i] = TextAt("Hotbar/Skill" + i + "/Label");
            _barBackgrounds[i] = transform.Find("Hotbar/Skill" + i).GetComponent<Image>();
            _barIdleColors[i] = _barBackgrounds[i].color;
        }
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true)) UiMenuFont.Apply(text);
        _hudStats = transform.parent.Find("Panel_TopLeft") as RectTransform;
        _panel.SetActive(false);
        _task.SetActive(false);
        _bar.SetActive(false);
        if (GetComponent<CardSkillController>() == null) gameObject.AddComponent<CardSkillController>();
    }
    void OnEnable() { Localization.LanguageChanged += Refresh; }
    void OnDisable() { Localization.LanguageChanged -= Refresh; Close(); }
    void OnDestroy() { if (Instance == this) Instance = null; }
    void Update()
    {
        bool gameplay = SkillProgress.Ready && CardInstancedRenderManager.IsGameplayReady && !GameSceneLoader.IsLoading && !WelcomePopupView.IsWaitingForStart;
        if (!gameplay) { Close(); SetVisible(_task, false); SetVisible(_bar, false); return; }
        bool menuAvailable = SkillProgress.IsTestingAllSkills || TutorialHintView.CanShowSkillMenu;
        if (!menuAvailable) Close();
        if (IsOpen && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))) Close();
        else if (menuAvailable && !GamePause.IsPaused && Input.GetKeyDown(KeyCode.Tab)) Open();
        bool hudVisible = !GamePause.IsPaused;
        SetVisible(_task, hudVisible && menuAvailable); SetVisible(_bar, hudVisible);
        _refresh -= Time.unscaledDeltaTime;
        if (_revision != SkillProgress.Revision || _refresh <= 0f)
        {
            _refresh = 0.25f;
            if (_revision != SkillProgress.Revision) { _revision = SkillProgress.Revision; Refresh(); }
            else RefreshHotbar(false);
            PositionTask();
        }
    }
    static void SetVisible(GameObject obj, bool visible) { if (obj != null && obj.activeSelf != visible) obj.SetActive(visible); }
    public void Open()
    {
        if (IsOpen || GamePause.IsPaused || !SkillProgress.Ready || GameSceneLoader.IsLoading
            || !CardInstancedRenderManager.IsGameplayReady || WelcomePopupView.IsWaitingForStart
            || (!SkillProgress.IsTestingAllSkills && !TutorialHintView.CanShowSkillMenu)) return;
        PlayerCardHand hand = PlayerCardHand.Instance;
        if (hand != null && (hand.IsHandInputLocked || hand.IsAwaitingRevealCollect)) return;
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
        Set(_points, Localization.Format(LocalizationKeys.SkillsPoints, SkillProgress.Points));
        Set(_instructions, Localization.Get(LocalizationKeys.SkillsInstructions));
        Set(_closeText, Localization.Get(LocalizationKeys.SkillsClose));
        Set(_openText, Localization.Get(LocalizationKeys.SkillsOpen));
        Set(_taskTitle, Localization.Get(LocalizationKeys.SkillsTaskTitle));
        int target = SkillProgress.NextGoal, points = SkillProgress.Points;
        string taskText = target > 0
            ? Localization.Format(LocalizationKeys.SkillsTaskRows, target, SkillProgress.CompletedRows)
            : Localization.Get(LocalizationKeys.SkillsTaskDone);
        if (points > 0) taskText += "\n" + Localization.Format(LocalizationKeys.SkillsPoints, points);
        Set(_taskText, taskText);
        for (int i = 0; i < 5; i++)
        {
            int level = SkillProgress.Level(i);
            string name = Localization.Get("skills." + SkillCatalog.Keys[i] + ".name");
            Set(_names[i], name);
            Set(_levels[i], Localization.Format(LocalizationKeys.SkillsLevel, level, SkillCatalog.MaxLevel(i)));
            _nodes[i].color = i == _selected ? new Color(1f, 0.9f, 0.55f) : level > 0 ? new Color(0.72f,1f,0.72f) : Color.white;
            SetVisible(_checks[i], level > 0);
            for (int j = 0; j < _ranks[i].Length; j++)
                _ranks[i][j].color = j < level ? new Color(0.35f,1f,0.45f) : new Color(0.4f,0.4f,0.45f);

        }
        int current = SkillProgress.Level(_selected), maximum = SkillCatalog.MaxLevel(_selected);
        string key = "skills." + SkillCatalog.Keys[_selected];
        Set(_name, Localization.Get(key + ".name"));
        Set(_description, Localization.Get(key + ".description"));
        Set(_stats, current == 0 ? Localization.Get(LocalizationKeys.SkillsLocked) : Stats(_selected, current));
        Set(_next, current >= maximum ? Localization.Get(LocalizationKeys.SkillsMax) : Localization.Get(LocalizationKeys.SkillsNext) + "\n" + Stats(_selected, current + 1));
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
            string status = level == 0 ? Localization.Get(LocalizationKeys.SkillsLocked) : active > 0
                ? Localization.Format(LocalizationKeys.SkillsActive, active) : cooldown > 0
                ? Localization.Format(LocalizationKeys.SkillsCooldown, cooldown) : Localization.Get(LocalizationKeys.SkillsReady);
            Set(_barLabels[i], Localization.Get("skills." + SkillCatalog.Keys[i] + ".name") + "\n" + status);
            _barBackgrounds[i].color = active > 0 ? activeHotbarColor : _barIdleColors[i];
        }
    }
    static string Stats(int skill, int level)
    {
        int cooldown = SkillCatalog.Cooldown(skill, level), effect = SkillCatalog.Effect(skill, level);
        if (skill >= 2) return Localization.Format(LocalizationKeys.SkillsStats, cooldown, effect);
        string text = Localization.Format(LocalizationKeys.SkillsWait, cooldown);
        return skill == 0 ? text + "\n" + Localization.Format(LocalizationKeys.SkillsAmount, effect) : text;
    }
    void PositionTask()
    {
        float fit = Mathf.Min(1f, Mathf.Min((_root.rect.width - 48f) / Mathf.Max(1f, _body.rect.width),
            (_root.rect.height - 48f) / Mathf.Max(1f, _body.rect.height)));
        Vector3 bodyScale = Vector3.one * Mathf.Max(0.1f, fit);
        if (_body.localScale != bodyScale) _body.localScale = bodyScale;
        if (_hudStats == null) return;
        _hudStats.GetWorldCorners(_corners);
        Vector3 bottomLeft = _root.InverseTransformPoint(_corners[0]);
        Vector2 position = new Vector2(bottomLeft.x - _root.rect.xMin, bottomLeft.y - _root.rect.yMax - 12f);
        if (_taskRect.anchoredPosition != position) _taskRect.anchoredPosition = position;
    }
}
