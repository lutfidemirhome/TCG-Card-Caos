using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// In-game tutorial shell. MenuScene Hierarchy (MainMenuCanvas / Panel_Tutorial) is the source
/// of truth — including the authored screen position. Runtime clones that panel onto
/// InGameHudCanvas and never rebuilds it. Steps reuse the same sliced panel.
/// </summary>
public class TutorialHintView : MonoBehaviour
{
    public const string PanelName = "Panel_Tutorial";
    public const string LabelName = "Label";
    public const string SpriteResourcePath = "Sprite Assets/TutorialKeys";
    const string MenuCanvasName = "MainMenuCanvas";
    const float StepInputDelay = 0.4f;

    enum Step
    {
        Move = 0,
        Pickup = 1,
        Drop = 2,
        Scroll = 3,
        Arrange = 4,
        Crouch = 5,
        Done = 6,
    }

    [SerializeField] GameObject root;
    [SerializeField] TMP_Text label;

    static TutorialHintView _gameplayInstance;
    static GameObject _carriedRoot;
    static Step _step;
    static bool _showThisSession;
    static bool _sessionDecided;
    static bool _usedCrouchBeforeHint;

    TutorialHintFitter _fitter;
    LocalizedText _localized;
    string _laidOutText;
    float _stepShownAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _gameplayInstance = null;
        _carriedRoot = null;
        _step = Step.Move;
        _showThisSession = false;
        _sessionDecided = false;
        _usedCrouchBeforeHint = false;
    }

    public static void ResetSession()
    {
        _step = Step.Move;
        _sessionDecided = true;
        _showThisSession = !VideoCaptureQuality.DisableOnboardingUi
            && GameSceneLoader.PendingLoadMode == GameLoadMode.NewGame;
        _usedCrouchBeforeHint = false;
        if (_gameplayInstance != null)
            _gameplayInstance.Hide();
    }

    /// <summary>
    /// Clone MenuScene's authored Panel_Tutorial before that scene unloads.
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

    void Awake()
    {
        BindExisting();
        Hide();

        if (GameScenes.IsActiveGameScene())
        {
            _gameplayInstance = this;
            EnsureSession();
        }
    }

    void OnEnable()
    {
        Localization.LanguageChanged += OnLanguageChanged;
    }

    void OnDisable()
    {
        Localization.LanguageChanged -= OnLanguageChanged;
    }

    void OnDestroy()
    {
        if (_gameplayInstance == this)
            _gameplayInstance = null;
    }

    void LateUpdate()
    {
        if (!GameScenes.IsActiveGameScene() || _gameplayInstance != this)
            return;

        if (_showThisSession && _step < Step.Crouch)
            RememberCrouchKeyIfPressed();

        if (GameSceneLoader.IsLoading || !CardInstancedRenderManager.IsGameplayReady)
        {
            Hide();
            return;
        }

        if (!_showThisSession || _step >= Step.Done)
        {
            Hide();
            return;
        }

        if (WelcomePopupView.IsWaitingForStart || GamePause.IsPaused)
        {
            Hide();
            return;
        }

        if (_step == Step.Drop && IsPackFlowBlockingDropHint())
        {
            Hide();
            return;
        }

        Show();
        RefreshLayoutIfNeeded();

        if (!root.activeSelf)
            return;

        if (Time.realtimeSinceStartup - _stepShownAt < StepInputDelay)
            return;

        if (_step == Step.Move && HasMoveInput())
        {
            AdvanceTo(Step.Pickup);
            return;
        }

        if (_step == Step.Pickup && HasPickupInput())
        {
            AdvanceTo(Step.Drop);
            return;
        }

        if (_step == Step.Drop && HasDropInput())
        {
            AdvanceTo(Step.Scroll);
            return;
        }

        if (_step == Step.Scroll && HasScrollInput())
        {
            AdvanceTo(Step.Arrange);
            return;
        }

        if (_step == Step.Arrange && HasArrangedMatchingRow())
        {
            AdvanceTo(_usedCrouchBeforeHint ? Step.Done : Step.Crouch);
            return;
        }

        if (_step == Step.Crouch && HasCrouchInput())
            AdvanceTo(Step.Done);
    }

    static void EnsureSession()
    {
        if (_sessionDecided)
            return;

        _sessionDecided = true;
        _step = Step.Move;
        _showThisSession = !VideoCaptureQuality.DisableOnboardingUi
            && GameSceneLoader.PendingLoadMode == GameLoadMode.NewGame;
        _usedCrouchBeforeHint = false;
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
            if (label == null)
            {
                Transform found = root.transform.Find(LabelName);
                if (found != null)
                    label = found.GetComponent<TMP_Text>();
            }

            if (label != null && _localized == null)
                _localized = label.GetComponent<LocalizedText>();

            _fitter = root.GetComponent<TutorialHintFitter>();
        }

        BindKeySprites();
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
        Transform foundLabel = root.transform.Find(LabelName);
        label = foundLabel != null ? foundLabel.GetComponent<TMP_Text>() : null;
        _localized = label != null ? label.GetComponent<LocalizedText>() : null;
        _fitter = root.GetComponent<TutorialHintFitter>();
        _carriedRoot = null;

        if (sceneCopy != null && sceneCopy != root)
            Destroy(sceneCopy);
    }

    void BindKeySprites()
    {
        if (label == null)
            return;

        TMP_SpriteAsset sprites = Resources.Load<TMP_SpriteAsset>(SpriteResourcePath);
        if (sprites == null)
            return;

        sprites.UpdateLookupTables();
        label.spriteAsset = sprites;
        label.richText = true;
    }

    void Show()
    {
        BindExisting();
        if (root == null)
            return;

        ApplyStepCopy();

        if (!root.activeSelf)
        {
            root.SetActive(true);
            BindKeySprites();
            UiMenuFont.Apply(label);
            _stepShownAt = Time.realtimeSinceStartup;
            _laidOutText = null;
            if (_fitter != null)
                _fitter.Relayout();
        }
    }

    void Hide()
    {
        if (root != null && root.activeSelf)
            root.SetActive(false);
        _laidOutText = null;
    }

    void AdvanceTo(Step next)
    {
        _step = next;
        _laidOutText = null;
        _stepShownAt = Time.realtimeSinceStartup;

        if (_step >= Step.Done)
        {
            Hide();
            return;
        }

        if (_step == Step.Drop && IsPackFlowBlockingDropHint())
        {
            Hide();
            return;
        }

        ApplyStepCopy();
        if (_fitter != null)
            _fitter.Relayout();
    }

    void ApplyStepCopy()
    {
        string key = CurrentStepKey();
        if (string.IsNullOrEmpty(key))
            return;

        if (_localized != null)
        {
            if (_localized.Key != key)
                _localized.SetKey(key);
            return;
        }

        if (label != null)
            label.text = Localization.Get(key);
    }

    static string CurrentStepKey()
    {
        switch (_step)
        {
            case Step.Move:
                return LocalizationKeys.TutorialMove;
            case Step.Pickup:
                return LocalizationKeys.TutorialPickup;
            case Step.Drop:
                return LocalizationKeys.TutorialDrop;
            case Step.Scroll:
                return LocalizationKeys.TutorialScroll;
            case Step.Arrange:
                return LocalizationKeys.TutorialArrange;
            case Step.Crouch:
                return LocalizationKeys.TutorialCrouch;
            default:
                return null;
        }
    }

    void RefreshLayoutIfNeeded()
    {
        if (root == null || !root.activeSelf || _fitter == null || label == null)
            return;

        if (label.text == _laidOutText)
            return;

        _laidOutText = label.text;
        _fitter.Relayout();
    }

    void OnLanguageChanged()
    {
        if (root == null || !root.activeSelf)
            return;

        ApplyStepCopy();
        _laidOutText = null;
        if (_fitter != null)
            _fitter.Relayout();
    }

    static bool HasMoveInput()
    {
        if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.15f)
            return true;
        if (Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.15f)
            return true;

        return Input.GetKey(KeyCode.W)
               || Input.GetKey(KeyCode.A)
               || Input.GetKey(KeyCode.S)
               || Input.GetKey(KeyCode.D);
    }

    static bool HasPickupInput()
    {
        PlayerCardHand hand = PlayerCardHand.Instance;
        return hand != null && hand.OccupiedHandSlots > 0;
    }

    static bool HasDropInput()
    {
        return Input.GetKeyDown(KeyCode.Q);
    }

    static bool IsPackFlowBlockingDropHint()
    {
        PlayerCardHand hand = PlayerCardHand.Instance;
        if (hand == null)
            return false;

        return hand.HasHeldPack
               || hand.IsOpeningPack
               || hand.IsAwaitingRevealCollect
               || hand.IsPackOpenMovementLocked;
    }

    static bool HasScrollInput()
    {
        if (Mathf.Approximately(Input.mouseScrollDelta.y, 0f))
            return false;

        PlayerCardHand hand = PlayerCardHand.Instance;
        return hand != null && hand.Count >= 2;
    }

    static bool HasArrangedMatchingRow()
    {
        CardShelf[] shelves = Object.FindObjectsByType<CardShelf>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < shelves.Length; i++)
        {
            CardShelf shelf = shelves[i];
            if (shelf != null && shelf.HasCompletedSeriesRow())
                return true;
        }

        PsaCabinet[] cabinets = Object.FindObjectsByType<PsaCabinet>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < cabinets.Length; i++)
        {
            PsaCabinet cabinet = cabinets[i];
            if (cabinet != null && cabinet.IsComplete())
                return true;
        }

        return false;
    }

    static void RememberCrouchKeyIfPressed()
    {
        if (WasCrouchKeyPressedThisFrame())
            _usedCrouchBeforeHint = true;
    }

    static bool HasCrouchInput()
    {
        return WasCrouchKeyPressedThisFrame();
    }

    static bool WasCrouchKeyPressedThisFrame()
    {
        return Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
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
