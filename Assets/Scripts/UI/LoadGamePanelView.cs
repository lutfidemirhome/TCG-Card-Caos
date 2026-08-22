using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authored Load Game overlay. Lists save slots; clicking one asks for confirm, Yes loads that save.
/// Layout and art live in MenuScene under Panel_LoadGame (edit in Hierarchy like other menu panels).
/// </summary>
public class LoadGamePanelView : MonoBehaviour
{
    const string MissingConfirmHint =
        "Panel_LoadConfirm not found under Panel_LoadGame. "
        + "Run: TCG Card Caos → UI → Add Load Game Confirm Dialog";

    [SerializeField] GameObject root;
    [SerializeField] Button cancelButton;
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] LoadGameSlotView[] slots;
    [SerializeField] LoadGameConfirmView confirmView;

    string _pendingSlotId;
    bool _slotClicksWired;

    public bool IsOpen => root != null && root.activeSelf;

    void Awake()
    {
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(Hide);
            cancelButton.onClick.AddListener(Hide);
        }

        EnsureCancelLabel();
        EnsureConfirmView();
        WireConfirmButtons();
        WireSlotClicks();

        if (confirmView != null)
            confirmView.Hide();
    }

    void EnsureConfirmView()
    {
        if (confirmView == null)
            confirmView = GetComponentInChildren<LoadGameConfirmView>(true);

        if (confirmView == null)
            Debug.LogWarning("[LoadGamePanelView] " + MissingConfirmHint);
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

        LocalizedText localized = label.GetComponent<LocalizedText>();
        if (localized == null)
            localized = label.gameObject.AddComponent<LocalizedText>();

        if (string.IsNullOrEmpty(localized.Key))
            localized.SetKey(LocalizationKeys.LoadGameCancel);
        else if (string.IsNullOrEmpty(label.text))
            localized.SetKey(localized.Key);
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
        if (slots == null || _slotClicksWired)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            LoadGameSlotView slot = slots[i];
            if (slot == null || slot.SelectButton == null)
                continue;

            LoadGameSlotView captured = slot;
            slot.SelectButton.onClick.RemoveAllListeners();
            slot.SelectButton.onClick.AddListener(() => OnSlotClicked(captured));
        }

        _slotClicksWired = true;
    }

    void EnsureSlotsArray()
    {
        if (slots != null && slots.Length > 0)
            return;

        slots = GetComponentsInChildren<LoadGameSlotView>(true);
    }

    public void Show()
    {
        if (root != null)
            root.SetActive(true);
        else
            gameObject.SetActive(true);

        transform.SetAsLastSibling();
        EnsureCancelLabel();
        HideConfirm();
        RefreshSlotList();
        RefreshScroll();
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

    public void Hide()
    {
        HideConfirm();
        _pendingSlotId = null;

        if (root != null)
            root.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    void RefreshSlotList()
    {
        EnsureSlotsArray();
        WireSlotClicks();

        if (slots == null || slots.Length == 0)
            return;

        List<SaveSlotMetadata> saves = GameSaveManager.GetSaveSlots();
        int saveCount = saves != null ? saves.Count : 0;

        for (int i = 0; i < slots.Length; i++)
        {
            LoadGameSlotView slot = slots[i];
            if (slot == null)
                continue;

            if (i < saveCount && saves[i] != null && !string.IsNullOrEmpty(saves[i].slotId))
            {
                slot.gameObject.SetActive(true);
                slot.Bind(saves[i], null);
            }
            else
            {
                slot.ClearBinding();
                slot.gameObject.SetActive(false);
            }
        }
    }

    void OnSlotClicked(LoadGameSlotView slot)
    {
        if (slot == null || string.IsNullOrEmpty(slot.BoundSlotId))
            return;

        _pendingSlotId = slot.BoundSlotId;
        if (confirmView != null)
            confirmView.Show();
        else
            Debug.LogWarning("[LoadGamePanelView] " + MissingConfirmHint);
    }

    void OnConfirmYes()
    {
        string slotId = _pendingSlotId;
        HideConfirm();
        _pendingSlotId = null;

        if (string.IsNullOrEmpty(slotId))
            return;

        GameSceneLoader.LoadSaveSlot(slotId);
    }

    void OnConfirmNo()
    {
        HideConfirm();
        _pendingSlotId = null;
    }

    void HideConfirm()
    {
        if (confirmView != null)
            confirmView.Hide();
    }

}
