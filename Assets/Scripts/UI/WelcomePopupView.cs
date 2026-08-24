using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// New-game welcome overlay. MenuScene Hierarchy (MainMenuCanvas / Panel_Welcome) is the source
/// of truth. Runtime clones that panel into the game scene and never builds placeholder UI.
/// Shown every New Game; Continue / Load skip it.
/// </summary>
public class WelcomePopupView : MonoBehaviour
{
    public const string PanelName = "Panel_Welcome";
    const string MenuCanvasName = "MainMenuCanvas";

    [SerializeField] GameObject root;
    [SerializeField] Button startButton;

    static WelcomePopupView _gameplayInstance;
    static GameObject _carriedRoot;
    static bool _sessionDecided;
    static bool _showThisSession;

    bool _wired;
    bool _open;

    /// <summary>
    /// True until Start is clicked this New Game session. ESC may still open the pause menu.
    /// </summary>
    public static bool IsWaitingForStart { get; private set; }

    public bool IsOpen => _open && root != null && root.activeSelf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _gameplayInstance = null;
        _carriedRoot = null;
        IsWaitingForStart = false;
        _sessionDecided = false;
        _showThisSession = false;
    }

    public static void ResetSession()
    {
        _sessionDecided = false;
        _showThisSession = false;
        IsWaitingForStart = false;
    }

    /// <summary>
    /// Clone MenuScene's authored Panel_Welcome before that scene unloads.
    /// Call while MenuScene is still loaded (New Game from the main menu).
    /// </summary>
    public static void CaptureFromLoadedMenu()
    {
        DiscardCarried();

        Transform panel = FindLoadedMenuPanel();
        if (panel == null)
            return;

        var src = panel as RectTransform;
        _carriedRoot = Object.Instantiate(panel.gameObject);
        _carriedRoot.name = PanelName;
        _carriedRoot.SetActive(false);
        Object.DontDestroyOnLoad(_carriedRoot);

        if (src != null)
            CopyLocalRect(src, (RectTransform)_carriedRoot.transform);
    }

    public static bool ShouldBlockGameplay()
    {
        if (!_sessionDecided)
        {
            _sessionDecided = true;
            GameLoadMode mode = GameSceneLoader.PendingLoadMode;
            _showThisSession = mode == GameLoadMode.NewGame;
            PlayerPrefs.DeleteKey("TCGCardCaos.HasSeenWelcome");
        }

        return _showThisSession;
    }

    void Awake()
    {
        BindExisting();
        if (root != null)
            root.SetActive(false);
        _open = false;

        if (GameScenes.IsActiveGameScene())
        {
            _gameplayInstance = this;
            if (ShouldBlockGameplay())
                IsWaitingForStart = true;
        }
    }

    void OnDestroy()
    {
        if (_gameplayInstance != this)
            return;

        _gameplayInstance = null;
        IsWaitingForStart = false;
        _sessionDecided = false;
        _showThisSession = false;
    }

    void Start()
    {
        if (!GameScenes.IsActiveGameScene())
            return;

        if (!ShouldBlockGameplay())
            return;

        Show();
    }

    void BindExisting()
    {
        AdoptCarriedMenuPanel();

        if (root == null)
        {
            Transform found = transform.Find(PanelName);
            if (found != null)
                root = found.gameObject;
        }

        if (startButton == null && root != null)
            startButton = root.transform.Find("Button_Start")?.GetComponent<Button>();

        if (_wired || startButton == null)
            return;

        startButton.onClick.RemoveListener(OnStartClicked);
        startButton.onClick.AddListener(OnStartClicked);
        _wired = true;
    }

    void AdoptCarriedMenuPanel()
    {
        if (_carriedRoot == null || !GameScenes.IsActiveGameScene())
            return;

        GameObject sceneCopy = root;
        if (sceneCopy == null)
        {
            Transform found = transform.Find(PanelName);
            if (found != null)
                sceneCopy = found.gameObject;
        }

        _carriedRoot.transform.SetParent(transform, false);
        _carriedRoot.name = PanelName;
        _carriedRoot.SetActive(false);

        root = _carriedRoot;
        startButton = root.transform.Find("Button_Start")?.GetComponent<Button>();
        _wired = false;
        _carriedRoot = null;

        if (sceneCopy != null && sceneCopy != root)
            Destroy(sceneCopy);
    }

    public void Show()
    {
        BindExisting();
        if (root == null)
            return;

        root.SetActive(true);
        _open = true;
        IsWaitingForStart = true;
        root.transform.SetAsLastSibling();
        GamePause.SetPaused(true);
        UiEventSystem.Ensure();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
        _open = false;
    }

    /// <summary>
    /// Pause menu is opening on top. Keep the Start-pending flag; do not mark the popup as seen.
    /// </summary>
    public static void CoverForPause()
    {
        if (_gameplayInstance == null)
            return;

        _gameplayInstance.Hide();
    }

    public static void RestoreAfterPause()
    {
        if (_gameplayInstance == null || !IsWaitingForStart)
            return;

        _gameplayInstance.Show();
    }

    void OnStartClicked()
    {
        IsWaitingForStart = false;
        _showThisSession = false;
        Hide();
        GamePause.SetPaused(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    static void DiscardCarried()
    {
        if (_carriedRoot == null)
            return;

        Object.Destroy(_carriedRoot);
        _carriedRoot = null;
    }

    static Transform FindLoadedMenuPanel()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform canvas = FindNamed(roots[r].transform, MenuCanvasName);
                if (canvas == null)
                    continue;

                Transform panel = canvas.Find(PanelName);
                if (panel != null)
                    return panel;
            }
        }

        return null;
    }

    static Transform FindNamed(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform nested = FindNamed(parent.GetChild(i), name);
            if (nested != null)
                return nested;
        }

        return null;
    }

    static void CopyLocalRect(RectTransform src, RectTransform dst)
    {
        dst.anchorMin = src.anchorMin;
        dst.anchorMax = src.anchorMax;
        dst.pivot = src.pivot;
        dst.anchoredPosition3D = src.anchoredPosition3D;
        dst.sizeDelta = src.sizeDelta;
        dst.localRotation = src.localRotation;
        dst.localScale = src.localScale;
    }
}
