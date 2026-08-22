using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Raycast from screen center, show prompt, interact with E or left mouse click.
/// Ground cards are found via math raycast (no mass physics colliders).
/// Only the aimed card keeps its collider enabled.
/// </summary>
public class InteractionController : MonoBehaviour
{
    [SerializeField] Camera viewCamera;
    [SerializeField] float interactDistance = 4.5f;
    [SerializeField] LayerMask interactMask = ~0;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [Tooltip("Seconds to look at a card before the inspect preview and interact prompt appear. 0 = instant.")]
    [SerializeField] float inspectPreviewDelay = 0.15f;

    static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

    const float DefaultPromptAnchoredY = 220f;
    const float RevealCollectPromptAnchoredY = -50f;
    static readonly Color DefaultPromptBackground = new Color(0f, 0f, 0f, 0.55f);
    static readonly Color PackOpenWarningPromptBackground = new Color(0.95f, 0.78f, 0.08f, 0.82f);
    const float PromptPulsePeakScale = 1.1f;
    const float PromptPulseHalfDuration = 0.08f;

    Canvas _canvas;
    GameObject _promptRoot;
    RectTransform _promptPanelRect;
    Image _promptBackground;
    Text _promptText;
    IInteractable _currentTarget;
    IInteractionHighlight _currentHighlight;
    CardInspectPreview _inspectPreview;
    PackInspectPreview _packInspectPreview;
    PsaInspectPreview _psaInspectPreview;
    WorldCard _raycastAimedCard;
    WorldBoosterPack _raycastAimedPack;
    WorldCard _inspectPreviewTarget;
    WorldBoosterPack _packInspectPreviewTarget;
    IInteractable _pendingCardPromptTarget;
    PlayerCardHand _playerHand;
    IInteractable _pendingPackPromptTarget;
    float _inspectPreviewTimer;
    Coroutine _promptPulseRoutine;

    void Awake()
    {
        if (viewCamera == null)
            viewCamera = GetComponent<Camera>();

        CardLayers.EnsureInitialized();
        interactMask &= ~CardLayers.WorldCardMask;

        _inspectPreview = CardInspectPreview.EnsureOn(viewCamera);
        _packInspectPreview = PackInspectPreview.EnsureOn(viewCamera);
        _psaInspectPreview = PsaInspectPreview.EnsureOn(viewCamera);
        _playerHand = PlayerCardHandResolver.FromTransformHierarchy(transform);
        BuildPromptUI();
    }

    PlayerCardHand ResolveHand() =>
        _playerHand != null ? _playerHand : (_playerHand = PlayerCardHandResolver.FromTransformHierarchy(transform));

    void Update()
    {
        if (GamePause.IsPaused)
        {
            _raycastAimedCard = null;
            ClearTarget();
            return;
        }

        if (viewCamera == null || Cursor.lockState != CursorLockMode.Locked)
        {
            _raycastAimedCard = null;
            ClearTarget();
            return;
        }

        PlayerCardHand hand = ResolveHand();
        if (hand != null && hand.IsHandInputLocked)
        {
            _raycastAimedCard = null;
            _raycastAimedPack = null;

            if (hand.IsAwaitingRevealCollect)
            {
                UpdateSelectedPackPrompt();
                HandleInput();
            }
            else
            {
                ClearTarget();
            }

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

        if (aimedCard != null && InteractionOcclusion.IsOccluded(ray, aimedCardDistance, interactDistance))
        {
            aimedCard = null;
            aimedCardDistance = float.MaxValue;
        }

        WorldBoosterPack aimedPack = null;
        float aimedPackDistance = float.MaxValue;
        if (CardGroundQuery.TryRaycastWorldPack(ray, interactDistance, out WorldBoosterPack packHit, out float packDistance))
        {
            aimedPack = packHit;
            aimedPackDistance = packDistance;
        }

        if (aimedPack != null && InteractionOcclusion.IsOccluded(ray, aimedPackDistance, interactDistance))
        {
            aimedPack = null;
            aimedPackDistance = float.MaxValue;
        }

        _raycastAimedCard = aimedCard != null && !aimedCard.IsInHand ? aimedCard : null;

        int hitCount = Physics.RaycastNonAlloc(
            ray,
            HitBuffer,
            interactDistance,
            interactMask,
            QueryTriggerInteraction.Ignore);

        IInteractable interactable = ResolveBestInteractable(
            ray,
            HitBuffer,
            hitCount,
            aimedCard,
            aimedCardDistance,
            aimedPack,
            aimedPackDistance);

        if (interactable == null)
        {
            _raycastAimedPack = null;
            ClearDelayedInspectUiState();
            ClearActivePlacementAims();
            UpdateSelectedPackPrompt();
            return;
        }

        _raycastAimedPack = interactable is WorldBoosterPack selectedPack && !selectedPack.IsInHand
            ? selectedPack
            : null;

        if (interactable is WorldBoosterPack)
            UpdateSelectedPackPrompt(clearOnly: true);

        if (interactable is PsaCabinetSlot psaSlot)
        {
            if (_currentTarget is CardShelf previousShelf)
                previousShelf.ClearAim();
            if (_currentTarget is PsaCabinetSlot previousPsaSlot && !ReferenceEquals(previousPsaSlot, psaSlot))
                previousPsaSlot.ClearAim();

            ClearDelayedInspectUiState();
            psaSlot.SetAimHit(FindPsaSlotHit(HitBuffer, hitCount, psaSlot));
        }
        else if (interactable is CardShelf shelf)
        {
            if (_currentTarget is CardShelf previousShelf && !ReferenceEquals(previousShelf, shelf))
                previousShelf.ClearAim();
            if (_currentTarget is PsaCabinetSlot previousPsaSlot)
                previousPsaSlot.ClearAim();

            ClearDelayedInspectUiState();
            shelf.SetAimHit(FindShelfHit(HitBuffer, hitCount, shelf));
        }
        else
        {
            ClearActivePlacementAims();
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
            if (interactable is PsaCabinetSlot emptyPsaSlot)
                emptyPsaSlot.ClearAim();
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
            _psaInspectPreview?.Hide();
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
            if (_inspectPreviewTarget.UsesPsaSlab)
                _psaInspectPreview?.Hide();
            else
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

        if (worldCard.UsesPsaSlab)
        {
            if (_psaInspectPreview == null)
                _psaInspectPreview = PsaInspectPreview.EnsureOn(viewCamera);

            _inspectPreview?.Hide();
            _psaInspectPreview.Show(worldCard);
        }
        else
        {
            if (_inspectPreview == null)
                _inspectPreview = CardInspectPreview.EnsureOn(viewCamera);

            _psaInspectPreview?.Hide();
            _inspectPreview.Show(worldCard);
        }

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
        float aimedCardDistance,
        WorldBoosterPack aimedPack,
        float aimedPackDistance)
    {
        PlayerCardHand hand = ResolveHand();

        CardShelf bestShelf = null;
        float bestShelfDistance = float.MaxValue;

        PsaCabinetSlot bestPsaSlot = null;
        float bestPsaSlotDistance = float.MaxValue;

        IInteractable bestOther = null;
        float bestOtherDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null)
                continue;

            PsaCabinetSlot psaSlot = collider.GetComponentInParent<PsaCabinetSlot>();
            if (psaSlot != null)
            {
                if (psaSlot.IsAimCollider(collider) && hits[i].distance < bestPsaSlotDistance)
                {
                    bestPsaSlotDistance = hits[i].distance;
                    bestPsaSlot = psaSlot;
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
            if (interactable == null || interactable is WorldBoosterPack)
                continue;

            if (hits[i].distance < bestOtherDistance)
            {
                bestOtherDistance = hits[i].distance;
                bestOther = interactable;
            }
        }

        if (hand != null && hand.HasSelectedHeldCard())
        {
            WorldCard heldCard = hand.SelectedHeldCard;
            if (heldCard != null && heldCard.UsesPsaSlab && bestPsaSlot != null)
            {
                if (!InteractionOcclusion.IsOccluded(ray, bestPsaSlotDistance, interactDistance)
                    && bestPsaSlot.CanPlaceHeldCard(heldCard))
                {
                    return bestPsaSlot;
                }
            }
        }

        if (hand != null && hand.HasSelectedHeldCard() && bestShelf != null)
        {
            WorldCard heldCard = hand.SelectedHeldCard;
            if (heldCard == null || !heldCard.UsesPsaSlab)
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
        }

        if (aimedPack != null)
        {
            if (aimedCard != null)
                return aimedPackDistance < aimedCardDistance ? aimedPack : aimedCard;

            if (bestShelf == null || aimedPackDistance < bestShelfDistance)
                return aimedPack;
        }

        if (aimedCard != null)
            return aimedCard;

        if (bestShelf != null && !InteractionOcclusion.IsOccluded(ray, bestShelfDistance, interactDistance))
            return bestShelf;

        if (bestOther != null && !InteractionOcclusion.IsOccluded(ray, bestOtherDistance, interactDistance))
            return bestOther;

        return null;
    }

    void ClearActivePlacementAims()
    {
        if (_currentTarget is CardShelf shelf)
            shelf.ClearAim();
        if (_currentTarget is PsaCabinetSlot psaSlot)
            psaSlot.ClearAim();
    }

    void ClearActiveShelfAim() => ClearActivePlacementAims();

    void ClearShelfAimForNonShelfTarget(IInteractable interactable)
    {
        if (interactable is WorldCard worldCard)
            worldCard.GetComponentInParent<CardShelf>()?.ClearAim();
    }

    static RaycastHit FindPsaSlotHit(RaycastHit[] hits, int hitCount, PsaCabinetSlot slot)
    {
        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = hits[i].collider;
            if (collider != null
                && collider.GetComponentInParent<PsaCabinetSlot>() == slot
                && slot.IsAimCollider(collider))
            {
                return hits[i];
            }
        }

        return default;
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
        PlayerCardHand hand = ResolveHand();

        if (hand != null && PlayerCardHand.IsPackActionKeyDown())
        {
            if (hand.IsAwaitingRevealCollect)
            {
                hand.RequestRevealCollect();
                return;
            }

            if (hand.HasHeldPack)
            {
                if (hand.TryOpenHeldPackFromInput())
                    ClearTarget();
                else if (hand.IsSelectedPackOpenBlocked())
                    ShowPackOpenBlockedFeedback(hand);

                return;
            }
        }

        if (WasInteractPressedThisFrame() && ShouldUseCurrentTargetForInteract())
        {
            _currentTarget.Interact(gameObject.transform.root.gameObject);
            ClearTarget();
            return;
        }
    }

    bool WasInteractPressedThisFrame() =>
        Input.GetKeyDown(interactKey) || Input.GetMouseButtonDown(0);

    bool ShouldUseCurrentTargetForInteract()
    {
        if (_currentTarget == null || _currentTarget is SelectedPackPromptTarget)
            return false;

        if (_currentTarget is WorldCard || _currentTarget is WorldBoosterPack)
            return true;

        return !string.IsNullOrEmpty(_currentTarget.GetPromptText());
    }

    void UpdateSelectedPackPrompt(bool clearOnly = false)
    {
        PlayerCardHand hand = ResolveHand();
        if (!clearOnly && hand != null && hand.IsAwaitingRevealCollect)
        {
            string revealPrompt = hand.GetRevealCollectPromptText();
            if (!string.IsNullOrEmpty(revealPrompt))
            {
                ShowPrompt(SelectedPackPromptTarget.Instance, revealPrompt, RevealCollectPromptAnchoredY, pivotCenter: true);
                return;
            }
        }

        if (!clearOnly && hand != null && hand.IsPackSelected)
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

    void ShowPackOpenBlockedFeedback(PlayerCardHand hand)
    {
        string prompt = hand.GetSelectedPackPromptText();
        if (string.IsNullOrEmpty(prompt))
            return;

        StopPromptPulse();

        ShowPrompt(SelectedPackPromptTarget.Instance, prompt, warningBackground: true);
        _promptPulseRoutine = StartCoroutine(PulsePackOpenWarningPrompt());
    }

    IEnumerator PulsePackOpenWarningPrompt()
    {
        if (_promptPanelRect == null)
        {
            _promptPulseRoutine = null;
            yield break;
        }

        Vector3 baseScale = Vector3.one;
        _promptPanelRect.localScale = baseScale;

        float elapsed = 0f;
        while (elapsed < PromptPulseHalfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / PromptPulseHalfDuration);
            float scale = Mathf.Lerp(1f, PromptPulsePeakScale, t);
            _promptPanelRect.localScale = baseScale * scale;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < PromptPulseHalfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / PromptPulseHalfDuration);
            float scale = Mathf.Lerp(PromptPulsePeakScale, 1f, t);
            _promptPanelRect.localScale = baseScale * scale;
            yield return null;
        }

        _promptPanelRect.localScale = baseScale;

        if (_promptBackground != null)
            _promptBackground.color = DefaultPromptBackground;

        _promptPulseRoutine = null;
    }

    sealed class SelectedPackPromptTarget : IInteractable
    {
        public static readonly SelectedPackPromptTarget Instance = new SelectedPackPromptTarget();

        SelectedPackPromptTarget() { }

        public string GetPromptText() => string.Empty;

        public void Interact(GameObject interactor)
        {
            // Pack open / reveal collect use F or right click (see HandleInput).
        }
    }

    void ClearTarget()
    {
        if (_currentTarget is CardShelf shelf)
            shelf.ClearAim();
        if (_currentTarget is PsaCabinetSlot psaSlot)
            psaSlot.ClearAim();

        ClearDelayedInspectUiState();
        ClearPromptAndHighlight();
    }

    void ClearPromptAndHighlight()
    {
        StopPromptPulse();
        ClearHighlight();
        _currentTarget = null;
        if (_promptRoot != null)
            _promptRoot.SetActive(false);
    }

    void StopPromptPulse()
    {
        if (_promptPulseRoutine != null)
        {
            StopCoroutine(_promptPulseRoutine);
            _promptPulseRoutine = null;
        }

        if (_promptPanelRect != null)
            _promptPanelRect.localScale = Vector3.one;

        if (_promptBackground != null)
            _promptBackground.color = DefaultPromptBackground;
    }

    void ShowPrompt(
        IInteractable interactable,
        string prompt,
        float anchoredY = DefaultPromptAnchoredY,
        bool pivotCenter = false,
        bool warningBackground = false)
    {
        if (interactable == null || string.IsNullOrEmpty(prompt))
            return;

        if (_promptPanelRect != null)
        {
            _promptPanelRect.pivot = pivotCenter ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);
            _promptPanelRect.anchoredPosition = new Vector2(0f, anchoredY);
        }

        if (_promptBackground != null && _promptPulseRoutine == null)
        {
            _promptBackground.color = warningBackground
                ? PackOpenWarningPromptBackground
                : DefaultPromptBackground;
        }

        if (!ReferenceEquals(interactable, _currentTarget))
        {
            if (_currentTarget is CardShelf previousShelf)
                previousShelf.ClearAim();
            if (_currentTarget is PsaCabinetSlot previousPsaSlot)
                previousPsaSlot.ClearAim();

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
        background.color = DefaultPromptBackground;
        background.raycastTarget = false;
        _promptBackground = background;

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
        _promptText.text = InteractPrompt.Format("Action");

        RectTransform textRect = _promptText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        _promptRoot.SetActive(false);
    }
}
