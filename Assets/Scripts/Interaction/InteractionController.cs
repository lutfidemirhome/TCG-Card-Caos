using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Raycast from screen center, show prompt, interact with E.
/// Looking at a ground card also shows a large readable inspect preview on the middle-right.
/// </summary>
public class InteractionController : MonoBehaviour
{
    [SerializeField] Camera viewCamera;
    [SerializeField] float interactDistance = 3f;
    [SerializeField] LayerMask interactMask = ~0;
    [SerializeField] KeyCode interactKey = KeyCode.E;

    static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

    Canvas _canvas;
    GameObject _promptRoot;
    Text _promptText;
    IInteractable _currentTarget;
    IInteractionHighlight _currentHighlight;
    CardInspectPreview _inspectPreview;

    void Awake()
    {
        if (viewCamera == null)
            viewCamera = GetComponent<Camera>();

        _inspectPreview = CardInspectPreview.EnsureOn(viewCamera);
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
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            HitBuffer,
            interactDistance,
            interactMask,
            QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
        {
            ClearTarget();
            return;
        }

        IInteractable interactable = ResolveBestInteractable(HitBuffer, hitCount);
        if (interactable == null)
        {
            ClearTarget();
            return;
        }

        if (interactable is CardShelf shelf)
        {
            shelf.SetAimHit(FindShelfHit(HitBuffer, hitCount, shelf));
        }
        else
        {
            ClearShelfAimForNonShelfTarget(interactable);
        }

        string prompt = interactable.GetPromptText();
        if (string.IsNullOrEmpty(prompt))
        {
            if (interactable is CardShelf emptyShelf)
                emptyShelf.ClearAim();
            ClearTarget();
            return;
        }

        if (!ReferenceEquals(interactable, _currentTarget))
        {
            if (_currentTarget is CardShelf previousShelf)
                previousShelf.ClearAim();

            ClearHighlight();
            _currentTarget = interactable;
            _currentHighlight = GetHighlight(interactable);
            _currentHighlight?.SetInteractionHighlight(true);
            _promptText.text = prompt;
            _promptRoot.SetActive(true);
            RefreshInspectPreview();
        }
        else if (_promptText != null)
        {
            _promptText.text = prompt;
        }
    }

    IInteractable ResolveBestInteractable(RaycastHit[] hits, int hitCount)
    {
        PlayerCardHand hand = GetComponentInParent<PlayerCardHand>();
        if (hand == null)
            hand = transform.root.GetComponentInChildren<PlayerCardHand>();

        WorldCard bestCard = null;
        float bestCardDistance = float.MaxValue;

        CardShelf bestShelf = null;
        float bestShelfDistance = float.MaxValue;

        IInteractable bestOther = null;
        float bestOtherDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null)
                continue;

            WorldCard worldCard = collider.GetComponentInParent<WorldCard>();
            if (worldCard != null && !worldCard.IsInHand)
            {
                if (hits[i].distance < bestCardDistance)
                {
                    bestCardDistance = hits[i].distance;
                    bestCard = worldCard;
                }

                continue;
            }

            CardShelf shelf = collider.GetComponentInParent<CardShelf>();
            if (shelf != null)
            {
                if (hits[i].distance < bestShelfDistance)
                {
                    bestShelfDistance = hits[i].distance;
                    bestShelf = shelf;
                }

                continue;
            }

            IInteractable interactable = collider.GetComponentInParent<IInteractable>();
            if (interactable == null)
                continue;

            if (hits[i].distance < bestOtherDistance)
            {
                bestOtherDistance = hits[i].distance;
                bestOther = interactable;
            }
        }

        if (bestCard != null)
            return bestCard;

        if (bestShelf != null && hand != null)
        {
            Vector3 aim = FindShelfHit(hits, hitCount, bestShelf).point;

            if (hand.HasSelectedHeldCard() && !bestShelf.IsAimOnOccupiedSlot(aim))
                return bestShelf;
        }

        return bestOther;
    }

    void ClearShelfAimForNonShelfTarget(IInteractable interactable)
    {
        if (interactable is WorldCard worldCard)
            worldCard.GetComponentInParent<CardShelf>()?.ClearAim();
    }

    static RaycastHit FindShelfHit(RaycastHit[] hits, int hitCount, CardShelf shelf)
    {
        for (int i = 0; i < hitCount; i++)
        {
            if (hits[i].collider != null && hits[i].collider.GetComponentInParent<CardShelf>() == shelf)
                return hits[i];
        }

        return hits[0];
    }

    IInteractable ResolveInteractable(RaycastHit hit)
    {
        // While holding a card, prefer placing on a shelf even if the ray hits a card on it.
        PlayerCardHand hand = GetComponentInParent<PlayerCardHand>();
        if (hand == null)
            hand = transform.root.GetComponentInChildren<PlayerCardHand>();

        if (hand != null && hand.HasSelectedHeldCard())
        {
            CardShelf shelf = hit.collider.GetComponentInParent<CardShelf>();
            if (shelf != null)
                return shelf;
        }

        return hit.collider.GetComponentInParent<IInteractable>();
    }

    void HandleInput()
    {
        if (_currentTarget == null || !Input.GetKeyDown(interactKey))
            return;

        _currentTarget.Interact(gameObject.transform.root.gameObject);
        ClearTarget();
    }

    void ClearTarget()
    {
        if (_currentTarget is CardShelf shelf)
            shelf.ClearAim();

        ClearHighlight();
        _currentTarget = null;
        if (_promptRoot != null)
            _promptRoot.SetActive(false);

        _inspectPreview?.Hide();
    }

    void RefreshInspectPreview()
    {
        if (_inspectPreview == null)
            _inspectPreview = CardInspectPreview.EnsureOn(viewCamera);

        if (_currentTarget is WorldCard worldCard && !worldCard.IsInHand)
            _inspectPreview.Show(worldCard);
        else
            _inspectPreview.Hide();
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
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 220f);
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
