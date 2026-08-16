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

    const float DefaultPromptAnchoredY = 220f;
    const float RevealCollectPromptAnchoredY = -50f;

    Canvas _canvas;
    GameObject _promptRoot;
    RectTransform _promptPanelRect;
    Text _promptText;
    IInteractable _currentTarget;
    IInteractionHighlight _currentHighlight;
    CardInspectPreview _inspectPreview;
    PackInspectPreview _packInspectPreview;
    WorldCard _raycastAimedCard;
    WorldBoosterPack _raycastAimedPack;
    WorldCard _inspectPreviewTarget;
    WorldBoosterPack _packInspectPreviewTarget;
    IInteractable _pendingCardPromptTarget;
    IInteractable _pendingPackPromptTarget;
    float _inspectPreviewTimer;

    void Awake()
    {
        if (viewCamera == null)
            viewCamera = GetComponent<Camera>();

        CardLayers.EnsureInitialized();
        interactMask &= ~CardLayers.WorldCardMask;

        _inspectPreview = CardInspectPreview.EnsureOn(viewCamera);
        _packInspectPreview = PackInspectPreview.EnsureOn(viewCamera);
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
        PlayerCardHand hand = PlayerCardHandResolver.FromTransformHierarchy(transform);
        if (hand != null && hand.IsAwaitingRevealCollect)
        {
            UpdateSelectedPackPrompt();
            return;
        }

        Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        WorldCard aimedCard = null;
        float aimedCardDistance = float.MaxValue;
        if (CardGroundQuery.TryRaycastWorldCard(ray, interactDistance, out WorldCard cardHit, out float cardDistance))
        {
            aimedCard = cardHit;
            aimedCardDistance = cardDistance;
        }

        if (aimedCard != null && InteractionOcclusion.IsOccluded(ray, aimedCardDistance, interactDistance))
        {
            aimedCard = null;
            aimedCardDistance = float.MaxValue;
        }

        _raycastAimedCard = aimedCard != null && !aimedCard.IsInHand ? aimedCard : null;

        int hitCount = Physics.RaycastNonAlloc(
            ray,
            HitBuffer,
            interactDistance,
            interactMask,
            QueryTriggerInteraction.Ignore);

        IInteractable interactable = ResolveBestInteractable(ray, HitBuffer, hitCount, aimedCard, aimedCardDistance);

        if (interactable == null)
        {
            _raycastAimedPack = null;
            ClearDelayedInspectUiState();
            UpdateSelectedPackPrompt();
            return;
        }

        _raycastAimedPack = interactable is WorldBoosterPack aimedPack && !aimedPack.IsInHand
            ? aimedPack
            : null;

        if (interactable is WorldBoosterPack)
            UpdateSelectedPackPrompt(clearOnly: true);

        if (interactable is CardShelf shelf)
        {
            ClearDelayedInspectUiState();
            shelf.SetAimHit(FindShelfHit(HitBuffer, hitCount, shelf));
        }
        else
        {
            ClearShelfAimForNonShelfTarget(interactable);
        }

        if (interactable is WorldCard worldCard)
        {
            HandleGroundCardTarget(worldCard);
            return;
        }

        if (interactable is WorldBoosterPack worldPack)
        {
            HandleGroundPackTarget(worldPack);
            return;
        }

        ClearDelayedInspectUiState();

        string prompt = interactable.GetPromptText();
        if (string.IsNullOrEmpty(prompt))
        {
            if (interactable is CardShelf emptyShelf)
                emptyShelf.ClearAim();
            ClearTarget();
            return;
        }

        _pendingCardPromptTarget = null;
        ShowPrompt(interactable, prompt);
    }

    void HandleGroundCardTarget(WorldCard worldCard)
    {
        if (worldCard == null || worldCard.IsInHand)
        {
            ClearDelayedInspectUiState();
            return;
        }

        ClearDelayedPackUiState();

        if (inspectPreviewDelay <= 0f)
        {
            _pendingCardPromptTarget = null;
            ShowGroundCardUi(worldCard);
            return;
        }

        if (!ReferenceEquals(worldCard, _pendingCardPromptTarget))
        {
            if (_currentTarget is CardShelf previousShelf)
                previousShelf.ClearAim();

            _pendingCardPromptTarget = worldCard;
            _inspectPreviewTarget = worldCard;
            _inspectPreviewTimer = 0f;
            _inspectPreview?.Hide();
            ClearPromptAndHighlight();
            CardInteractionFocus.ClearFocus();
        }
    }

    void HandleGroundPackTarget(WorldBoosterPack worldPack)
    {
        if (worldPack == null || worldPack.IsInHand)
        {
            ClearDelayedInspectUiState();
            return;
        }

        ClearDelayedCardUiState();

        if (inspectPreviewDelay <= 0f)
        {
            _pendingPackPromptTarget = null;
            ShowGroundPackUi(worldPack);
            return;
        }

        if (!ReferenceEquals(worldPack, _pendingPackPromptTarget))
        {
            if (_currentTarget is CardShelf previousShelf)
                previousShelf.ClearAim();

            _pendingPackPromptTarget = worldPack;
            _packInspectPreviewTarget = worldPack;
            _inspectPreviewTimer = 0f;
            _packInspectPreview?.Hide();
            ClearPromptAndHighlight();
        }
    }

    void ClearDelayedInspectUiState()
    {
        ClearDelayedCardUiState();
        ClearDelayedPackUiState();
    }

    void ClearDelayedCardUiState()
    {
        _pendingCardPromptTarget = null;

        if (_inspectPreviewTarget != null)
        {
            _inspectPreview?.Hide();
            _inspectPreviewTarget = null;
            _inspectPreviewTimer = 0f;
        }

        CardInteractionFocus.ClearFocus();

        if (_currentTarget is WorldCard)
            ClearPromptAndHighlight();
    }

    void ClearDelayedPackUiState()
    {
        _pendingPackPromptTarget = null;

        if (_packInspectPreviewTarget != null)
        {
            _packInspectPreview?.Hide();
            _packInspectPreviewTarget = null;
            _inspectPreviewTimer = 0f;
        }

        if (_currentTarget is WorldBoosterPack)
            ClearPromptAndHighlight();
    }

    void ShowGroundCardUi(WorldCard worldCard)
    {
        if (worldCard == null || worldCard.IsInHand)
            return;

        CardInteractionFocus.SetFocusedCard(worldCard);

        if (_inspectPreview == null)
            _inspectPreview = CardInspectPreview.EnsureOn(viewCamera);

        _inspectPreview.Show(worldCard);
        ShowPrompt(worldCard, worldCard.GetPromptText());
    }

    void ShowGroundPackUi(WorldBoosterPack worldPack)
    {
        if (worldPack == null || worldPack.IsInHand)
            return;

        if (_packInspectPreview == null)
            _packInspectPreview = PackInspectPreview.EnsureOn(viewCamera);

        _packInspectPreview.Show(worldPack);
        ShowPrompt(worldPack, worldPack.GetPromptText());
    }

    IInteractable ResolveBestInteractable(
        Ray ray,
        RaycastHit[] hits,
        int hitCount,
        WorldCard aimedCard,
        float aimedCardDistance)
    {
        PlayerCardHand hand = PlayerCardHandResolver.FromTransformHierarchy(transform);

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
            if (!InteractionOcclusion.IsOccluded(ray, bestShelfDistance, interactDistance))
            {
                Vector3 aim = hitCount > 0
                    ? FindShelfHit(hits, hitCount, bestShelf).point
                    : bestShelf.transform.position;

                if (!bestShelf.IsAimOnOccupiedSlot(aim))
                    return bestShelf;
            }
        }

        WorldBoosterPack aimedPack = bestOther as WorldBoosterPack;
        if (aimedCard != null && aimedPack != null)
        {
            if (bestOtherDistance < aimedCardDistance)
                return aimedPack;

            return aimedCard;
        }

        if (aimedCard != null)
            return aimedCard;

        if (bestShelf != null && !InteractionOcclusion.IsOccluded(ray, bestShelfDistance, interactDistance))
            return bestShelf;

        if (bestOther != null && !InteractionOcclusion.IsOccluded(ray, bestOtherDistance, interactDistance))
            return bestOther;

        return null;
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
        PlayerCardHand hand = PlayerCardHandResolver.FromTransformHierarchy(transform);

        if (hand != null && hand.IsAwaitingRevealCollect)
        {
            if (Input.GetKeyDown(KeyCode.Return))
            {
                hand.RequestRevealCollect();
                return;
            }
        }

        if (hand != null && hand.HasHeldPack)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                hand.TryOpenHeldPackFromInput();
                ClearTarget();
                return;
            }
        }

        if (_currentTarget == null || !Input.GetKeyDown(interactKey))
            return;

        _currentTarget.Interact(gameObject.transform.root.gameObject);
        ClearTarget();
    }

    void UpdateSelectedPackPrompt(bool clearOnly = false)
    {
        PlayerCardHand hand = PlayerCardHandResolver.FromTransformHierarchy(transform);
        if (!clearOnly && hand != null && hand.IsAwaitingRevealCollect)
        {
            string revealPrompt = hand.GetRevealCollectPromptText();
            if (!string.IsNullOrEmpty(revealPrompt))
            {
                ShowPrompt(SelectedPackPromptTarget.Instance, revealPrompt, RevealCollectPromptAnchoredY, pivotCenter: true);
                return;
            }
        }

        if (!clearOnly && hand != null && (hand.IsPackSelected || hand.HasHeldPack))
        {
            string prompt = hand.GetSelectedPackPromptText();
            if (!string.IsNullOrEmpty(prompt))
            {
                ShowPrompt(SelectedPackPromptTarget.Instance, prompt);
                return;
            }
        }

        if (_currentTarget is SelectedPackPromptTarget)
            ClearTarget();
    }

    sealed class SelectedPackPromptTarget : IInteractable
    {
        public static readonly SelectedPackPromptTarget Instance = new SelectedPackPromptTarget();

        SelectedPackPromptTarget() { }

        public string GetPromptText() => string.Empty;

        public void Interact(GameObject interactor)
        {
            // Pack open / reveal collect use Enter only (see HandleInput).
        }
    }

    void ClearTarget()
    {
        if (_currentTarget is CardShelf shelf)
            shelf.ClearAim();

        ClearDelayedInspectUiState();
        ClearPromptAndHighlight();
    }

    void ClearPromptAndHighlight()
    {
        ClearHighlight();
        _currentTarget = null;
        if (_promptRoot != null)
            _promptRoot.SetActive(false);
    }

    void ShowPrompt(
        IInteractable interactable,
        string prompt,
        float anchoredY = DefaultPromptAnchoredY,
        bool pivotCenter = false)
    {
        if (interactable == null || string.IsNullOrEmpty(prompt))
            return;

        if (_promptPanelRect != null)
        {
            _promptPanelRect.pivot = pivotCenter ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);
            _promptPanelRect.anchoredPosition = new Vector2(0f, anchoredY);
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
        }
        else if (_promptText != null)
        {
            _promptText.text = prompt;
        }
    }

    void UpdateInspectPreview()
    {
        if (_pendingCardPromptTarget is WorldCard aimedCard
            && aimedCard != null
            && !aimedCard.IsInHand
            && ReferenceEquals(aimedCard, _raycastAimedCard))
        {
            UpdateCardInspectPreview(aimedCard);
            return;
        }

        if (_pendingPackPromptTarget is WorldBoosterPack aimedPack
            && aimedPack != null
            && !aimedPack.IsInHand
            && ReferenceEquals(aimedPack, _raycastAimedPack))
        {
            UpdatePackInspectPreview(aimedPack);
            return;
        }

        ClearDelayedInspectUiState();
    }

    void UpdateCardInspectPreview(WorldCard aimedCard)
    {
        if (aimedCard == null || aimedCard.IsInHand)
        {
            ClearDelayedInspectUiState();
            return;
        }

        if (!ReferenceEquals(aimedCard, _inspectPreviewTarget))
        {
            _inspectPreviewTarget = aimedCard;
            _inspectPreviewTimer = 0f;
            if (inspectPreviewDelay > 0f)
            {
                _inspectPreview?.Hide();
                ClearPromptAndHighlight();
                CardInteractionFocus.ClearFocus();
            }
        }

        if (inspectPreviewDelay <= 0f)
        {
            ShowGroundCardUi(aimedCard);
            return;
        }

        _inspectPreviewTimer += Time.deltaTime;
        if (_inspectPreviewTimer < inspectPreviewDelay)
            return;

        ShowGroundCardUi(aimedCard);
    }

    void UpdatePackInspectPreview(WorldBoosterPack aimedPack)
    {
        if (aimedPack == null || aimedPack.IsInHand)
        {
            ClearDelayedInspectUiState();
            return;
        }

        if (!ReferenceEquals(aimedPack, _packInspectPreviewTarget))
        {
            _packInspectPreviewTarget = aimedPack;
            _inspectPreviewTimer = 0f;
            if (inspectPreviewDelay > 0f)
            {
                _packInspectPreview?.Hide();
                ClearPromptAndHighlight();
            }
        }

        if (inspectPreviewDelay <= 0f)
        {
            ShowGroundPackUi(aimedPack);
            return;
        }

        _inspectPreviewTimer += Time.deltaTime;
        if (_inspectPreviewTimer < inspectPreviewDelay)
            return;

        ShowGroundPackUi(aimedPack);
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
        _canvas = RuntimeOverlayCanvasFactory.Create(transform, "InteractionPromptCanvas", sortingOrder: 90);

        _promptRoot = new GameObject("PromptPanel");
        _promptRoot.transform.SetParent(_canvas.transform, false);

        var background = _promptRoot.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.55f);
        background.raycastTarget = false;

        RectTransform panelRect = background.rectTransform;
        _promptPanelRect = panelRect;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, DefaultPromptAnchoredY);
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
