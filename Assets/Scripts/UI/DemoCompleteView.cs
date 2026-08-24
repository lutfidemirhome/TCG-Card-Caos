using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Demo-finished overlay. MenuScene Hierarchy (MainMenuCanvas / Panel_DemoComplete) is the source
/// of truth. Runtime clones that panel into the game scene and never builds placeholder UI.
/// Shown once when the six demo cabinets are all complete.
/// </summary>
public class DemoCompleteView : MonoBehaviour
{
    public const string PanelName = "Panel_DemoComplete";
    const string MenuCanvasName = "MainMenuCanvas";

    [SerializeField] GameObject root;
    [SerializeField] Button wishlistButton;
    [SerializeField] Button backButton;

    static DemoCompleteView _gameplayInstance;
    static GameObject _carriedRoot;

    bool _wired;
    bool _open;
    bool _shownThisSession;
    bool _loadSampled;
    bool _completeOnLoad;
    bool _coveredByPause;

    public static bool IsBlockingGameplay { get; private set; }

    public bool IsOpen => _open && root != null && root.activeSelf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _gameplayInstance = null;
        _carriedRoot = null;
        IsBlockingGameplay = false;
    }

    public static void ResetSession()
    {
        IsBlockingGameplay = false;
        if (_gameplayInstance == null)
            return;

        _gameplayInstance._shownThisSession = false;
        _gameplayInstance._loadSampled = false;
        _gameplayInstance._completeOnLoad = false;
        _gameplayInstance.Hide();
    }

    /// <summary>
    /// Clone MenuScene's authored Panel_DemoComplete before that scene unloads.
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
        LocalizedText.FreezeAuthoredCopy(_carriedRoot);
        Object.DontDestroyOnLoad(_carriedRoot);

        if (src != null)
            CopyLocalRect(src, (RectTransform)_carriedRoot.transform);
    }

    void Awake()
    {
        BindExisting();
        if (root != null)
            root.SetActive(false);
        _open = false;

        if (GameScenes.IsActiveGameScene())
            _gameplayInstance = this;
    }

    void OnDestroy()
    {
        if (_gameplayInstance != this)
            return;

        _gameplayInstance = null;
        IsBlockingGameplay = false;
    }

    void LateUpdate()
    {
        if (!GameScenes.IsActiveGameScene() || _gameplayInstance != this)
            return;

        if (GameSceneLoader.IsLoading || !CardInstancedRenderManager.IsGameplayReady)
            return;

        if (!_loadSampled)
        {
            _loadSampled = true;
            _completeOnLoad = DemoShelfTargets.AreAllComplete();
            return;
        }

        if (_shownThisSession || _open || _completeOnLoad)
            return;

        if (WelcomePopupView.IsWaitingForStart)
            return;

        if (!DemoShelfTargets.AreAllComplete())
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

        if (root != null)
        {
            if (wishlistButton == null)
                wishlistButton = root.transform.Find("Button_Wishlist")?.GetComponent<Button>();
            if (backButton == null)
                backButton = root.transform.Find("Button_Back")?.GetComponent<Button>();
        }

        if (_wired)
            return;

        if (wishlistButton != null)
        {
            wishlistButton.onClick.RemoveListener(OnWishlistClicked);
            wishlistButton.onClick.AddListener(OnWishlistClicked);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(OnBackClicked);
            backButton.onClick.AddListener(OnBackClicked);
        }

        _wired = wishlistButton != null;
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
        wishlistButton = root.transform.Find("Button_Wishlist")?.GetComponent<Button>();
        backButton = root.transform.Find("Button_Back")?.GetComponent<Button>();
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
        _shownThisSession = true;
        IsBlockingGameplay = true;
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
        IsBlockingGameplay = false;
    }

    public static void CoverForPause()
    {
        if (_gameplayInstance == null || !_gameplayInstance._open)
            return;

        _gameplayInstance._coveredByPause = true;
        _gameplayInstance.HideKeepingPending();
    }

    public static void RestoreAfterPause()
    {
        if (_gameplayInstance == null || !_gameplayInstance._coveredByPause)
            return;

        _gameplayInstance._coveredByPause = false;
        _gameplayInstance.Show();
    }

    void HideKeepingPending()
    {
        if (root != null)
            root.SetActive(false);
        _open = false;
    }

    void OnWishlistClicked()
    {
        SteamFullGameStore.OpenWishlistPage();
    }

    void OnBackClicked()
    {
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
