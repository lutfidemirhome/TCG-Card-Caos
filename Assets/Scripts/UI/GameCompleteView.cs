using UnityEngine;
using UnityEngine.UI;

/// <summary>Full-game thank-you screen, driven by the HUD's already cached progress.</summary>
[DefaultExecutionOrder(-95)]
public sealed class GameCompleteView : MonoBehaviour
{
    static GameCompleteView _instance;
    static int _closedFrame = -1;
    public static bool ConsumesInput => (_instance != null && _instance._open) || _closedFrame == Time.frameCount;
    GameObject _panel;
    RectTransform _body;
    bool _initialized, _shown, _open, _wasComplete, _previousVisible;
    float _completedAt;
    int _openedFrame;
    CursorLockMode _previousLock;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _instance = null; _closedFrame = -1; }

    public static void Ensure(Transform hud)
    {
        if (_instance != null || GameBuildVariant.IsDemo) return;
        var prefab = Resources.Load<GameObject>("UI/GameComplete");
        if (prefab != null) Instantiate(prefab, hud, false);
    }

    void Awake()
    {
        _instance = this;
        _panel = transform.Find("Panel").gameObject;
        _body = (RectTransform)transform.Find("Panel/Body");
        _body.Find("Continue").GetComponent<Button>().onClick.AddListener(Close);
        _panel.SetActive(false);
        enabled = false; // Only the visible popup needs Update.
    }

    public static bool IsComplete(GameProgressCounter.Snapshot progress) =>
        progress.totalCards > 0 && progress.cardsPlaced >= progress.totalCards
        && progress.totalCabinets > 0 && progress.cabinetsCompleted >= progress.totalCabinets;

    public static void Observe(GameProgressCounter.Snapshot progress)
    {
        if (GameBuildVariant.IsDemo || !GameScenes.IsActiveGameScene() || GameSceneLoader.IsLoading
            || !CardInstancedRenderManager.IsGameplayReady) return;
        // Also reconcile an already completed save with Steam, independently of the popup.
        if (IsComplete(progress)) SteamSkillAchievements.NotifyGameCompleted();
        if (_instance == null || _instance._shown) return;
        _instance.ObserveReadyProgress(progress);
    }

    void ObserveReadyProgress(GameProgressCounter.Snapshot progress)
    {
        bool complete = IsComplete(progress);
        if (!_initialized)
        {
            _initialized = true;
            // Opening a previously completed save must not replay the ending.
            _shown = complete;
            return;
        }
        if (!complete) { _wasComplete = false; return; }
        if (!_wasComplete) { _wasComplete = true; _completedAt = Time.unscaledTime; }
        // Let the final placement and cabinet celebration finish before pausing.
        if (Time.unscaledTime - _completedAt < 1.5f || GamePause.IsPaused
            || WelcomePopupView.IsWaitingForStart || DemoCompleteView.IsBlockingGameplay
            || SkillPanelView.ConsumesPauseInput || CabinetTutorialView.ConsumesInput) return;
        var hand = PlayerCardHand.Instance;
        if (hand != null && (hand.IsHandInputLocked || hand.IsAwaitingRevealCollect)) return;
        Show();
    }

    void Show()
    {
        _shown = _open = true;
        _openedFrame = Time.frameCount;
        _previousLock = Cursor.lockState; _previousVisible = Cursor.visible;
        GamePause.SetPaused(true);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        transform.SetAsLastSibling();
        _panel.SetActive(true);
        LocalizedText.ApplyAll(_panel);
        UiEventSystem.Ensure();
        Fit();
        enabled = true;
    }

    void Update()
    {
        if (GameSceneLoader.IsLoading) { Close(); return; }
        Fit();
        if (Time.frameCount != _openedFrame && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    void Fit()
    {
        Vector2 size = ((RectTransform)transform).rect.size;
        float scale = Mathf.Min(1f, Mathf.Min((size.x - 40f) / _body.rect.width, (size.y - 80f) / (_body.rect.height + 70f)));
        _body.localScale = Vector3.one * Mathf.Max(.1f, scale);
    }

    void Close()
    {
        if (!_open) return;
        _open = false; _closedFrame = Time.frameCount;
        _panel.SetActive(false);
        if (!GameSceneLoader.IsLoading)
        {
            GamePause.SetPaused(false);
            Cursor.lockState = _previousLock; Cursor.visible = _previousVisible;
        }
        enabled = false;
    }
    void OnDisable() => Close();
    void OnDestroy() { if (_instance == this) _instance = null; }
}
