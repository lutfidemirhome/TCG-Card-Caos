using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// In-game Save Game overlay. Same layout as Load Game: filled saves on top, empty slots below.
/// Click empty to write that slot; click a filled row to overwrite after confirm.
/// </summary>
public class SaveGamePanelView : MonoBehaviour
{
    int EmptySlotRows => GameSaveStore.ManualSlotCount;
    const string ConfirmRootName = "Panel_SaveConfirm";
    const float SpinnerSpeed = 220f;
    const float MinSavingHintSeconds = 1.25f;

    [SerializeField] GameObject root;
    [SerializeField] Button cancelButton;
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] LoadGameSlotView[] slots;
    [SerializeField] LoadGameConfirmView confirmView;
    [SerializeField] Image deleteHintIcon;
    [SerializeField] GameObject savingHint;
    [SerializeField] RectTransform savingSpinner;
    [SerializeField] TMP_Text savingLabel;

    enum ConfirmMode
    {
        None,
        Overwrite,
        Delete
    }

    string _pendingSlotId;
    ConfirmMode _confirmMode;
    bool _savedThisOpen;
    float _savingShownAt = -1f;
    Coroutine _savingHintRoutine;

    public bool IsOpen => root != null && root.activeSelf;

    void Awake()
    {
        EnsureReferences();
        EnsureCancelLabel();
        WireCancelButton();
        ApplySaveCopy();
        WireConfirmButtons();
        WireSlotClicks();

        if (confirmView != null)
            confirmView.Hide();

        HideSavingHint();
    }

    void LateUpdate()
    {
        if (savingHint == null || !savingHint.activeSelf || savingSpinner == null)
            return;

        float angle = (Time.unscaledTime - _savingShownAt) * SpinnerSpeed;
        savingSpinner.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    void OnEnable()
    {
        GameSaveEvents.SaveCompleted += OnSaveCompleted;
        Localization.LanguageChanged += OnLanguageChanged;
    }

    void OnDisable()
    {
        GameSaveEvents.SaveCompleted -= OnSaveCompleted;
        Localization.LanguageChanged -= OnLanguageChanged;
    }

    void OnLanguageChanged()
    {
        if (!IsOpen)
            return;

        RefreshSlotList();
        ApplyEmptyClickLock();
        if (savingHint == null || !savingHint.activeSelf || savingLabel == null)
            return;

        bool saving = savingSpinner != null && savingSpinner.gameObject.activeSelf;
        savingLabel.text = Localization.Get(saving
            ? LocalizationKeys.SaveGameSaving
            : LocalizationKeys.SaveGameSaved);
    }

    void EnsureReferences()
    {
        if (root == null)
            root = gameObject;

        if (cancelButton == null)
        {
            Transform cancel = transform.Find("Button_Cancel");
            if (cancel != null)
                cancelButton = cancel.GetComponent<Button>();
        }

        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);

        if (confirmView == null)
            confirmView = GetComponentInChildren<LoadGameConfirmView>(true);

        if (deleteHintIcon == null)
        {
            Transform icon = transform.Find("DeleteHint/Icon");
            if (icon != null)
                deleteHintIcon = icon.GetComponent<Image>();
        }

        EnsureSlotsArray();
    }

    void EnsureSlotsArray()
    {
        slots = SaveSlotListLayout.CollectInOrder(SaveSlotListLayout.FindContent(transform));
    }

    void WireCancelButton()
    {
        if (cancelButton == null)
            return;

        cancelButton.onClick.RemoveListener(Hide);
        cancelButton.onClick.AddListener(Hide);
    }

    void EnsureCancelLabel()
    {
        if (cancelButton == null)
            return;

        TMP_Text label = cancelButton.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(cancelButton.transform, false);
            RectTransform labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(14f, 8f);
            labelRect.offsetMax = new Vector2(-14f, -10f);

            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 36f;
            tmp.fontStyle = FontStyles.Normal;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            UiMenuFont.Apply(tmp);
            label = tmp;
        }

        label.fontStyle = FontStyles.Normal;
        if (string.IsNullOrEmpty(label.text))
            label.text = "Cancel";

        LocalizedText localized = label.GetComponent<LocalizedText>();
        if (localized == null)
            localized = label.gameObject.AddComponent<LocalizedText>();
        localized.SetKey(LocalizationKeys.LoadGameCancel);
    }

    void ApplySaveCopy()
    {
        Transform titleArt = transform.Find("TitleArt");
        if (titleArt != null)
            titleArt.gameObject.SetActive(false);

        Transform title = transform.Find("Title");
        if (title != null)
        {
            title.gameObject.SetActive(true);
            LocalizedText localized = title.GetComponent<LocalizedText>();
            if (localized == null)
                localized = title.gameObject.AddComponent<LocalizedText>();
            localized.SetKey(LocalizationKeys.SaveGameTitle);
        }

        ApplyDeleteHint();
        EnsureSavingHint();

        Transform leftoverEmpty = transform.Find("EmptyHint");
        if (leftoverEmpty != null)
            leftoverEmpty.gameObject.SetActive(false);

        Transform confirmRoot = transform.Find(ConfirmRootName);
        if (confirmRoot == null)
            confirmRoot = transform.Find("Panel_LoadConfirm");

        Transform message = confirmRoot != null ? confirmRoot.Find("Band/Message") : null;
        if (message != null)
        {
            LocalizedText localized = message.GetComponent<LocalizedText>();
            if (localized == null)
                localized = message.gameObject.AddComponent<LocalizedText>();
            localized.SetKey(LocalizationKeys.SaveGameOverwriteConfirm);
        }

        Transform deleteLabel = transform.Find("DeleteHint/Label");
        if (deleteLabel != null)
        {
            LocalizedText localized = deleteLabel.GetComponent<LocalizedText>();
            if (localized == null)
                localized = deleteLabel.gameObject.AddComponent<LocalizedText>();
            localized.SetKey(LocalizationKeys.SaveGameDeleteHint);
        }
    }

    void WireConfirmButtons()
    {
        if (confirmView == null)
            return;

        if (confirmView.YesButton != null)
        {
            confirmView.YesButton.onClick.RemoveListener(OnConfirmYes);
            confirmView.YesButton.onClick.AddListener(OnConfirmYes);
        }

        if (confirmView.NoButton != null)
        {
            confirmView.NoButton.onClick.RemoveListener(OnConfirmNo);
            confirmView.NoButton.onClick.AddListener(OnConfirmNo);
        }
    }

    void WireSlotClicks()
    {
        EnsureSlotsArray();
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            LoadGameSlotView slot = slots[i];
            if (slot == null || slot.SelectButton == null)
                continue;

            LoadGameSlotView captured = slot;
            slot.SelectButton.onClick.RemoveAllListeners();
            slot.SelectButton.onClick.AddListener(() => OnSlotClicked(captured));
            slot.RightClicked = OnSlotRightClicked;
        }
    }

    public void Show()
    {
        EnsureReferences();
        EnsureCancelLabel();
        WireCancelButton();
        ApplySaveCopy();

        if (root != null)
            root.SetActive(true);
        else
            gameObject.SetActive(true);

        transform.SetAsLastSibling();
        HideConfirm();
        _savedThisOpen = false;
        RefreshSlotList();
        ApplyEmptyClickLock();
        RefreshScroll();
    }

    public void Hide()
    {
        HideConfirm();
        HideSavingHint();

        if (root != null)
            root.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    void RefreshScroll()
    {
        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);

        if (scrollRect == null || scrollRect.content == null)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
        scrollRect.verticalNormalizedPosition = 1f;
    }

    void RefreshSlotList()
    {
        List<SaveSlotMetadata> saves = GameSaveStore.GetSaveSlots();
        if (saves == null)
            saves = new List<SaveSlotMetadata>();

        int filledCount = 0;
        for (int i = 0; i < saves.Count; i++)
        {
            if (saves[i] != null && !string.IsNullOrEmpty(saves[i].slotId))
                filledCount++;
        }

        slots = SaveSlotListLayout.EnsureRows(transform, filledCount + EmptySlotRows);
        EnsureSlotsArray();
        WireSlotClicks();
        if (slots == null || slots.Length == 0)
            return;

        Texture filledThumb = SaveGameArt.FilledSlotThumbnail;
        Texture emptyThumb = SaveGameArt.EmptySlotThumbnail;

        int viewIndex = 0;
        for (int i = 0; i < saves.Count; i++)
        {
            SaveSlotMetadata save = saves[i];
            if (save == null || string.IsNullOrEmpty(save.slotId))
                continue;

            if (viewIndex >= slots.Length)
                break;

            LoadGameSlotView slot = slots[viewIndex++];
            if (slot == null)
                continue;

            slot.gameObject.SetActive(true);
            slot.BindSaveRow(save, filledThumb);
        }

        for (int empty = 0; empty < EmptySlotRows && viewIndex < slots.Length; empty++)
        {
            LoadGameSlotView slot = slots[viewIndex++];
            if (slot == null)
                continue;

            slot.gameObject.SetActive(true);
            slot.BindEmpty(string.Empty, emptyThumb);
        }

        for (int i = viewIndex; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;

            slots[i].ClearBinding();
            slots[i].gameObject.SetActive(false);
        }

        ApplyEmptyClickLock();
    }

    void ApplyEmptyClickLock()
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            LoadGameSlotView slot = slots[i];
            if (slot == null || slot.SelectButton == null)
                continue;

            slot.SelectButton.interactable = !slot.IsEmpty || !_savedThisOpen;
        }
    }

    void OnSlotClicked(LoadGameSlotView slot)
    {
        if (slot == null)
            return;

        if (slot.IsEmpty)
        {
            if (_savedThisOpen)
                return;

            _savedThisOpen = true;
            ApplyEmptyClickLock();
            ShowSavingHint();
            GameSaveStore.SaveManual();
            return;
        }

        if (string.IsNullOrEmpty(slot.BoundSlotId))
            return;

        ShowConfirm(ConfirmMode.Overwrite, slot.BoundSlotId);
    }

    void OnSlotRightClicked(LoadGameSlotView slot)
    {
        if (slot == null || slot.IsEmpty || string.IsNullOrEmpty(slot.BoundSlotId))
            return;

        ShowConfirm(ConfirmMode.Delete, slot.BoundSlotId);
    }

    void ShowConfirm(ConfirmMode mode, string slotId)
    {
        _confirmMode = mode;
        _pendingSlotId = slotId;
        SetConfirmMessage(mode == ConfirmMode.Delete
            ? LocalizationKeys.SaveGameDeleteConfirm
            : LocalizationKeys.SaveGameOverwriteConfirm);

        if (confirmView != null)
            confirmView.Show();
    }

    void SetConfirmMessage(string key)
    {
        Transform confirmRoot = transform.Find(ConfirmRootName);
        if (confirmRoot == null)
            confirmRoot = transform.Find("Panel_LoadConfirm");

        Transform message = confirmRoot != null ? confirmRoot.Find("Band/Message") : null;
        if (message == null)
            return;

        LocalizedText localized = message.GetComponent<LocalizedText>();
        if (localized == null)
            localized = message.gameObject.AddComponent<LocalizedText>();
        localized.SetKey(key);
    }

    void OnConfirmYes()
    {
        string slotId = _pendingSlotId;
        ConfirmMode mode = _confirmMode;
        HideConfirm();
        if (string.IsNullOrEmpty(slotId))
            return;

        if (mode == ConfirmMode.Delete)
        {
            GameSaveStore.DeleteSaveSlot(slotId);
            RefreshSlotList();
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null && scrollRect.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            return;
        }

        ShowSavingHint();
        GameSaveStore.SaveManual(slotId);
    }

    void OnConfirmNo()
    {
        HideConfirm();
    }

    void HideConfirm()
    {
        _pendingSlotId = null;
        _confirmMode = ConfirmMode.None;
        if (confirmView != null)
            confirmView.Hide();
    }

    void OnSaveCompleted(SaveSlotMetadata metadata)
    {
        if (!IsOpen)
            return;

        HideConfirm();
        _savedThisOpen = true;
        RefreshSlotList();
        ApplyEmptyClickLock();
        RefreshScroll();

        if (_savingHintRoutine != null)
            StopCoroutine(_savingHintRoutine);
        _savingHintRoutine = StartCoroutine(HideSavingHintWhenDue());
    }

    void ShowSavingHint()
    {
        EnsureSavingHint();
        if (savingHint == null)
            return;

        _savingShownAt = Time.unscaledTime;
        savingHint.SetActive(true);
        savingHint.transform.SetAsLastSibling();
        if (savingSpinner != null)
            savingSpinner.gameObject.SetActive(true);
        if (savingLabel != null)
            savingLabel.text = Localization.Get(LocalizationKeys.SaveGameSaving);
    }

    void HideSavingHint()
    {
        if (_savingHintRoutine != null)
        {
            StopCoroutine(_savingHintRoutine);
            _savingHintRoutine = null;
        }

        _savingShownAt = -1f;
        if (savingHint != null)
            savingHint.SetActive(false);
    }

    IEnumerator HideSavingHintWhenDue()
    {
        while (Time.unscaledTime - _savingShownAt < MinSavingHintSeconds)
            yield return null;

        if (savingSpinner != null)
            savingSpinner.gameObject.SetActive(false);

        if (savingLabel != null)
            savingLabel.text = Localization.Get(LocalizationKeys.SaveGameSaved);

        float savedHold = MinSavingHintSeconds * 0.5f;
        float savedAt = Time.unscaledTime;
        while (Time.unscaledTime - savedAt < savedHold)
            yield return null;

        if (savingHint != null)
            savingHint.SetActive(false);
        if (savingSpinner != null)
            savingSpinner.gameObject.SetActive(true);
        _savingHintRoutine = null;
    }

    void EnsureSavingHint()
    {
        if (savingHint == null)
        {
            Transform existing = transform.Find("SavingHint");
            if (existing != null)
                savingHint = existing.gameObject;
        }

        if (savingHint == null)
        {
            var hintGo = new GameObject("SavingHint", typeof(RectTransform));
            hintGo.transform.SetParent(transform, false);
            var hintRect = (RectTransform)hintGo.transform;
            hintRect.anchorMin = new Vector2(1f, 1f);
            hintRect.anchorMax = new Vector2(1f, 1f);
            hintRect.pivot = new Vector2(1f, 1f);
            hintRect.anchoredPosition = new Vector2(-48f, -36f);
            hintRect.sizeDelta = new Vector2(320f, 64f);
            savingHint = hintGo;
        }

        if (savingSpinner == null)
        {
            Transform spinner = savingHint.transform.Find("Spinner");
            if (spinner != null)
                savingSpinner = spinner as RectTransform;
        }

        if (savingSpinner == null)
        {
            var spinnerGo = new GameObject("Spinner", typeof(RectTransform), typeof(Image));
            spinnerGo.transform.SetParent(savingHint.transform, false);
            savingSpinner = (RectTransform)spinnerGo.transform;
        }

        savingSpinner.anchorMin = new Vector2(0f, 0.5f);
        savingSpinner.anchorMax = new Vector2(0f, 0.5f);
        savingSpinner.pivot = new Vector2(0.5f, 0.5f);
        savingSpinner.sizeDelta = new Vector2(52f, 52f);
        savingSpinner.anchoredPosition = new Vector2(26f, 0f);

        var spinnerImage = savingSpinner.GetComponent<Image>();
        if (spinnerImage == null)
            spinnerImage = savingSpinner.gameObject.AddComponent<Image>();
        Sprite spinnerSprite = SaveGameArt.Spinner;
        if (spinnerSprite != null)
            spinnerImage.sprite = spinnerSprite;
        spinnerImage.color = Color.white;
        spinnerImage.preserveAspect = true;
        spinnerImage.raycastTarget = false;

        if (savingLabel == null)
        {
            Transform label = savingHint.transform.Find("Label");
            if (label != null)
                savingLabel = label.GetComponent<TMP_Text>();
        }

        if (savingLabel == null)
        {
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(savingHint.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(1f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(64f, 0f);
            labelRect.sizeDelta = new Vector2(-64f, 48f);
            savingLabel = labelGo.AddComponent<TextMeshProUGUI>();
            savingLabel.alignment = TextAlignmentOptions.Left;
            savingLabel.fontSize = 32f;
            savingLabel.color = Color.white;
            savingLabel.raycastTarget = false;
            UiMenuFont.Apply(savingLabel);
        }

        savingHint.SetActive(false);
    }

    /// <summary>
    /// Builds a dressable Save panel from the authored Load Game overlay without touching pause layout.
    /// </summary>
    public static SaveGamePanelView CreateFromLoadPanel(LoadGamePanelView loadPanel, Transform parent)
    {
        if (loadPanel == null || parent == null)
            return null;

        GameObject clone = Instantiate(loadPanel.gameObject, parent, false);
        clone.name = "Panel_SaveGame";
        clone.SetActive(false);

        LoadGamePanelView loadView = clone.GetComponent<LoadGamePanelView>();
        if (loadView != null)
            DestroyImmediate(loadView);

        Transform confirm = clone.transform.Find("Panel_LoadConfirm");
        if (confirm != null)
            confirm.name = ConfirmRootName;

        SaveSlotListLayout.EnsureRows(clone.transform, GameSaveStore.ManualSlotCount + GameSaveSettings.AutosaveSlotCount);
        EnsureDressingSlots(clone.transform);

        var saveView = clone.GetComponent<SaveGamePanelView>();
        if (saveView == null)
            saveView = clone.AddComponent<SaveGamePanelView>();

        saveView.root = clone;
        saveView.EnsureReferences();
        saveView.ApplySaveCopy();
        return saveView;
    }

    void ApplyDeleteHint()
    {
        Transform hint = transform.Find("DeleteHint");
        if (hint == null)
            return;

        if (deleteHintIcon == null)
        {
            Transform icon = hint.Find("Icon");
            if (icon != null)
                deleteHintIcon = icon.GetComponent<Image>();
        }

        Sprite sprite = SaveGameArt.DeleteHint;
        if (deleteHintIcon != null && sprite != null)
        {
            deleteHintIcon.sprite = sprite;
            deleteHintIcon.color = Color.white;
            deleteHintIcon.preserveAspect = true;
            var iconRect = deleteHintIcon.transform as RectTransform;
            if (iconRect != null)
                iconRect.sizeDelta = new Vector2(80f, 80f);
        }
    }

    static void EnsureDressingSlots(Transform saveRoot)
    {
        Transform leftoverTitleArt = saveRoot.Find("TitleArt");
        if (leftoverTitleArt != null)
            leftoverTitleArt.gameObject.SetActive(false);

        if (saveRoot.Find("DeleteHint") != null)
            return;

        var hintGo = new GameObject("DeleteHint", typeof(RectTransform));
        hintGo.transform.SetParent(saveRoot, false);
        var hintRect = (RectTransform)hintGo.transform;
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(0f, 0f);
        hintRect.pivot = new Vector2(0f, 0.5f);
        hintRect.anchoredPosition = new Vector2(72f, 78f);
        hintRect.sizeDelta = new Vector2(280f, 64f);

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(hintGo.transform, false);
        var iconRect = (RectTransform)iconGo.transform;
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(64f, 64f);
        var iconImage = iconGo.GetComponent<Image>();
        iconImage.color = new Color(1f, 1f, 1f, 0f);
        iconImage.raycastTarget = false;
        iconImage.preserveAspect = true;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(hintGo.transform, false);
        var labelRect = (RectTransform)labelGo.transform;
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(76f, 0f);
        labelRect.sizeDelta = new Vector2(180f, 48f);
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Left;
        label.fontSize = 32f;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = "Delete";
        UiMenuFont.Apply(label);
        var localized = labelGo.AddComponent<LocalizedText>();
        localized.SetKey(LocalizationKeys.SaveGameDeleteHint);
    }
}
