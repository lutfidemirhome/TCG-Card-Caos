using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Raycast from screen center, show prompt, interact with E.
/// </summary>
public class InteractionController : MonoBehaviour
{
    [SerializeField] Camera viewCamera;
    [SerializeField] float interactDistance = 3f;
    [SerializeField] LayerMask interactMask = ~0;
    [SerializeField] KeyCode interactKey = KeyCode.E;

    Canvas _canvas;
    GameObject _promptRoot;
    Text _promptText;
    IInteractable _currentTarget;
    IInteractionHighlight _currentHighlight;

    void Awake()
    {
        if (viewCamera == null)
            viewCamera = GetComponent<Camera>();

        BuildPromptUI();
    }

    void Update()
    {
        if (viewCamera == null || Cursor.lockState != CursorLockMode.Locked)
        {
            ClearTarget();
            return;
        }

        UpdateTarget();
        HandleInput();
    }

    void UpdateTarget()
    {
        Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask, QueryTriggerInteraction.Ignore))
        {
            ClearTarget();
            return;
        }

        IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
        if (interactable == null)
        {
            ClearTarget();
            return;
        }

        string prompt = interactable.GetPromptText();
        if (string.IsNullOrEmpty(prompt))
        {
            ClearTarget();
            return;
        }

        if (!ReferenceEquals(interactable, _currentTarget))
        {
            ClearHighlight();
            _currentTarget = interactable;
            _currentHighlight = GetHighlight(interactable);
            _currentHighlight?.SetInteractionHighlight(true);
            _promptText.text = prompt;
            _promptRoot.SetActive(true);
        }
    }

    void HandleInput()
    {
        if (_currentTarget == null || !Input.GetKeyDown(interactKey))
            return;

        _currentTarget.Interact(gameObject.transform.root.gameObject);
    }

    void ClearTarget()
    {
        ClearHighlight();
        _currentTarget = null;
        if (_promptRoot != null)
            _promptRoot.SetActive(false);
    }

    static IInteractionHighlight GetHighlight(IInteractable interactable)
    {
        if (interactable is not MonoBehaviour behaviour)
            return null;

        if (behaviour is IInteractionHighlight highlight)
            return highlight;

        return behaviour.GetComponent<IInteractionHighlight>();
    }

    void ClearHighlight()
    {
        _currentHighlight?.SetInteractionHighlight(false);
        _currentHighlight = null;
    }

    void BuildPromptUI()
    {
        var canvasGo = new GameObject("InteractionPromptCanvas");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 90;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGo.AddComponent<GraphicRaycaster>();

        _promptRoot = new GameObject("PromptPanel");
        _promptRoot.transform.SetParent(canvasGo.transform, false);

        var background = _promptRoot.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.55f);
        background.raycastTarget = false;

        RectTransform panelRect = background.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 72f);
        panelRect.sizeDelta = new Vector2(460f, 52f);

        var textGo = new GameObject("PromptText");
        textGo.transform.SetParent(_promptRoot.transform, false);

        _promptText = textGo.AddComponent<Text>();
        _promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _promptText.fontSize = 22;
        _promptText.alignment = TextAnchor.MiddleCenter;
        _promptText.color = Color.white;
        _promptText.raycastTarget = false;
        _promptText.text = "Press [E] To Action";

        RectTransform textRect = _promptText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        _promptRoot.SetActive(false);
    }
}
