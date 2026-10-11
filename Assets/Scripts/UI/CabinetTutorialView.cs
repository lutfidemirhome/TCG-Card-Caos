using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One-time, proximity-based shelf guide. Art and content are authored in its own prefab.</summary>
[DefaultExecutionOrder(-90)]
public sealed class CabinetTutorialView : MonoBehaviour
{
    const string SeenKey = "TCGCardCaos.CabinetTutorialSeen.v1";
    static CabinetTutorialView _instance;
    static int _closedFrame = -1;
    public static bool ConsumesInput => (_instance != null && _instance._open) || _closedFrame == Time.frameCount;
    [SerializeField] float triggerDistance = 1.3f;
    GameObject _panel;
    RectTransform _body;
    Color _backdropColor;
    Transform _automaticParent;
    struct CabinetSurface
    {
        public Transform Root;
        public Bounds Bounds;
    }
    CabinetSurface[] _cabinets;
    bool _open, _seen, _previousVisible, _menuMode;
    CursorLockMode _previousLock;
    float _nextCheck;
    int _openedFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _instance = null; _closedFrame = -1; }

    public static void Ensure(Transform hud)
    {
        if (_instance != null || PlayerPrefs.GetInt(SeenKey, 0) != 0) return;
        var prefab = Resources.Load<GameObject>("UI/CabinetTutorial/CabinetTutorial");
        if (prefab != null) Instantiate(prefab, hud, false);
    }

    void Awake()
    {
        _instance = this;
        _seen = PlayerPrefs.GetInt(SeenKey, 0) != 0;
        _panel = transform.Find("Panel").gameObject;
        _backdropColor = _panel.GetComponent<Image>().color;
        _body = (RectTransform)transform.Find("Panel/Body");
        transform.Find("Panel/Body/Continue").GetComponent<Button>().onClick.AddListener(Dismiss);
        _panel.SetActive(false);
    }

    public static void ShowFromMenu(Transform menu)
    {
        if (_instance == null)
        {
            var prefab = Resources.Load<GameObject>("UI/CabinetTutorial/CabinetTutorial");
            if (prefab == null) return;
            Instantiate(prefab, menu, false);
        }
        if (_instance._open) return;
        _instance._automaticParent = _instance.transform.parent;
        _instance.transform.SetParent(menu, false);
        _instance._menuMode = true;
        _instance.enabled = true;
        _instance.Show();
    }

    void Update()
    {
        if (_open)
        {
            if (GameSceneLoader.IsLoading) { Close(); return; }
            Fit();
            if (Time.frameCount != _openedFrame && Input.GetKeyDown(KeyCode.Escape)) Dismiss();
            return;
        }
        if (_menuMode || _seen || Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + .4f;
        if (!GameScenes.IsActiveGameScene() || GameSceneLoader.IsLoading || GamePause.IsPaused
            || !CardInstancedRenderManager.IsGameplayReady || WelcomePopupView.IsWaitingForStart
            || DemoCompleteView.IsBlockingGameplay || SkillPanelView.ConsumesPauseInput
            || MinorSkillUnlockView.ConsumesInput || Cursor.lockState != CursorLockMode.Locked) return;
        var hand = PlayerCardHand.Instance;
        if (hand == null || hand.IsHandInputLocked || hand.IsAwaitingRevealCollect) return;
        if (_cabinets == null)
        {
            var list = new List<CabinetSurface>();
            foreach (var shelf in FindObjectsByType<CardShelf>(FindObjectsSortMode.None)) CacheSurfaces(shelf.transform, list);
            foreach (var cabinet in FindObjectsByType<PsaCabinet>(FindObjectsSortMode.None)) CacheSurfaces(cabinet.transform, list);
            _cabinets = list.ToArray();
        }
        Vector3 player = hand.transform.position;
        foreach (var cabinet in _cabinets)
        {
            if (cabinet.Root == null || !cabinet.Root.gameObject.activeInHierarchy) continue;
            Vector3 delta = cabinet.Bounds.ClosestPoint(player) - player;
            if (delta.sqrMagnitude <= triggerDistance * triggerDistance) { Show(); break; }
        }
    }

    static void CacheSurfaces(Transform root, List<CabinetSurface> list)
    {
        // Cache only the authored solid cabinet surfaces once; card/aim colliders do not
        // enlarge the tutorial zone. Keep vertical distance so other floors cannot trigger it.
        foreach (var collider in root.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger
                || collider.GetComponentInParent<WorldCard>() != null
                || collider.GetComponentInParent<PsaCabinetSlot>() != null) continue;
            list.Add(new CabinetSurface { Root = root, Bounds = collider.bounds });
        }
    }

    void Show()
    {
        var backdrop = _backdropColor;
        if (_menuMode) backdrop.a = .97f;
        _panel.GetComponent<Image>().color = backdrop;
        _previousLock = Cursor.lockState; _previousVisible = Cursor.visible;
        _open = true; _openedFrame = Time.frameCount;
        if (!_menuMode) GamePause.SetPaused(true);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        transform.SetAsLastSibling();
        _panel.SetActive(true);
        LocalizedText.ApplyAll(_panel);
        UiEventSystem.Ensure();
        Fit();
    }

    void Fit()
    {
        Vector2 size = ((RectTransform)transform).rect.size;
        float scale = Mathf.Min(1f, Mathf.Min((size.x - 40f) / _body.rect.width, (size.y - 40f) / (_body.rect.height + 92f)));
        _body.localScale = Vector3.one * Mathf.Max(.1f, scale);
    }

    void Dismiss()
    {
        if (!_open) return;
        bool manual = _menuMode;
        if (!_menuMode)
        {
            _seen = true;
            PlayerPrefs.SetInt(SeenKey, 1); PlayerPrefs.Save();
        }
        _cabinets = null;
        Close();
        enabled = manual && !_seen;
    }

    void Close()
    {
        if (!_open) return;
        _open = false; _closedFrame = Time.frameCount;
        _panel.SetActive(false);
        if (!GameSceneLoader.IsLoading)
        {
            if (!_menuMode) GamePause.SetPaused(false);
            Cursor.lockState = _previousLock; Cursor.visible = _previousVisible;
        }
        if (_menuMode)
        {
            if (_automaticParent != null) transform.SetParent(_automaticParent, false);
            _automaticParent = null;
            _menuMode = false;
        }
    }
    void OnDisable() => Close();
    void OnDestroy() { if (_instance == this) _instance = null; }
}
