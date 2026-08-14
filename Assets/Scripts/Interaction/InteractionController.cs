using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Raycast from screen center, show prompt, interact with E.
/// Ground cards are found via math raycast (no mass physics colliders).
/// Only the aimed card keeps its collider enabled.
/// </summary>
public class InteractionController : MonoBehaviour
{
    [SerializeField] Camera viewCamera;
    [SerializeField] float interactDistance = 3f;
    [SerializeField] LayerMask interactMask = ~0;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [Tooltip("Seconds to look at a card before the inspect preview and Press [E] prompt appear. 0 = instant.")]
    [SerializeField] float inspectPreviewDelay = 0.15f;

    static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

    Canvas _canvas;
    GameObject _promptRoot;
    Text _promptText;
    IInteractable _currentTarget;
    IInteractionHighlight _currentHighlight;
    CardInspectPreview _inspectPreview;
    WorldCard _raycastAimedCard;
    WorldCard _inspectPreviewTarget;
    IInteractable _pendingCardPromptTarget;
    float _inspectPreviewTimer;

    void Awake()
    {
        if (viewCamera == null)
            viewCamera = GetComponent<Camera>();

        CardLayers.EnsureInitialized();
        interactMask &= ~CardLayers.WorldCardMask;

        _inspectPreview = CardInspectPreview.EnsureOn(viewCamera);
        BuildPromptUI();
    }

    void Update()
    {
        if (viewCamera == null || Cursor.lockState != CursorLockMode.Locked)
        {
            _raycastAimedCard = null;
            ClearTarget();
            return;
        }

        UpdateTarget();
        UpdateInspectPreview();
        HandleInput();
    }

    void UpdateTarget()
    {
        Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        WorldCard aimedCard = null;
        float aimedCardDistance = float.MaxValue;
        if (CardGroundQuery.TryRaycastWorldCard(ray, interactDistance, out WorldCard cardHit, out float cardDistance))
        {
            aimedCard = cardHit;
            aimedCardDistance = cardDistance;
        }

        _raycastAimedCard = aimedCard != null && !aimedCard.IsInHand ? aimedCard : null;

        int hitCount = Physics.RaycastNonAlloc(
            ray,
            HitBuffer,
            interactDistance,
            interactMask,
            QueryTriggerInteraction.Ignore);

        IInteractable interactable = ResolveBestInteractable(HitBuffer, hitCount, aimedCard, aimedCardDistance);
        UpdateCardFocus(_raycastAimedCard);

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

        if (interactable is WorldCard && inspectPreviewDelay > 0f)
        {
            if (!ReferenceEquals(interactable, _pendingCardPromptTarget))
            {
                if (_currentTarget is CardShelf previousShelf)
                    previousShelf.ClearAim();

                _pendingCardPromptTarget = interactable;
                ClearPromptAndHighlight();
            }

            return;
        }

        _pendingCardPromptTarget = null;
        ShowPrompt(interactable, prompt);
    }

    static void UpdateCardFocus(WorldCard aimedCard)
    {
        if (aimedCard != null && !aimedCard.IsInHand)
            CardInteractionFocus.SetFocusedCard(aimedCard);
        else
            CardInteractionFocus.ClearFocus();
    }

    IInteractable ResolveBestInteractable(
        RaycastHit[] hits,
        int hitCount,
        WorldCard aimedCard,
        float aimedCardDistance)
    {
        PlayerCardHand hand = GetComponentInParent<PlayerCardHand>();
        if (hand == null)
            hand = transform.root.GetComponentInChildren<PlayerCardHand>();

        CardShelf bestShelf = null;
        float bestShelfDistance = float.MaxValue;

        IInteractable bestOther = null;
        float bestOtherDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null)
                continue;

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

        if (hand != null && hand.HasSelectedHeldCard() && bestShelf != null)
        {
            Vector3 aim = hitCount > 0
                ? FindShelfHit(hits, hitCount, bestShelf).point
                : bestShelf.transform.position;

            if (!bestShelf.IsAimOnOccupiedSlot(aim))
                return bestShelf;
        }

        if (aimedCard != null)
            return aimedCard;

        if (bestShelf != null)
            return bestShelf;

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

        return default;
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

        _pendingCardPromptTarget = null;
        CardInteractionFocus.ClearFocus();
        ClearPromptAndHighlight();
    }

    void ClearPromptAndHighlight()
    {
        ClearHighlight();
        _currentTarget = null;
        if (_promptRoot != null)
            _promptRoot.SetActive(false);
    }

    void ShowPrompt(IInteractable interactable, string prompt)
    {
        if (interactable == null || string.IsNullOrEmpty(prompt))
            return;

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
        }
        else if (_promptText != null)
        {
            _promptText.text = prompt;
        }
    }

    void UpdateInspectPreview()
    {
        WorldCard aimedCard = _raycastAimedCard;
        if (aimedCard == null)
        {
            if (_inspectPreviewTarget != null)
            {
                _inspectPreview?.Hide();
                _inspectPreviewTarget = null;
                _inspectPreviewTimer = 0f;
            }

            return;
        }

        if (!ReferenceEquals(aimedCard, _inspectPreviewTarget))
        {
            _inspectPreviewTarget = aimedCard;
            _inspectPreviewTimer = 0f;
            if (inspectPreviewDelay > 0f)
                _inspectPreview?.Hide();
        }

        if (inspectPreviewDelay <= 0f)
        {
            ShowDelayedCardUi(aimedCard);
            return;
        }

        _inspectPreviewTimer += Time.deltaTime;
        if (_inspectPreviewTimer < inspectPreviewDelay)
            return;

        ShowDelayedCardUi(aimedCard);
    }

    void ShowDelayedCardUi(WorldCard aimedCard)
    {
        if (_inspectPreview == null)
            _inspectPreview = CardInspectPreview.EnsureOn(viewCamera);

        _inspectPreview.Show(aimedCard);

        if (_pendingCardPromptTarget == null || !ReferenceEquals(aimedCard, _pendingCardPromptTarget))
            return;

        ShowPrompt(aimedCard, aimedCard.GetPromptText());
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
