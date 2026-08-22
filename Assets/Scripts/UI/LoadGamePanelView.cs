using System;
using System.Collections.Generic;
using System.IO;
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
    readonly List<Texture2D> _ownedThumbnails = new List<Texture2D>(8);
    bool _slotClicksWired;

    public bool IsOpen => root != null && root.activeSelf;

    void Awake()
    {
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(Hide);
            cancelButton.onClick.AddListener(Hide);
        }

        EnsureConfirmView();
        WireConfirmButtons();
        WireSlotClicks();
        ApplyValueTextStyleFromCancelButton();

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

    void OnDestroy()
    {
        ReleaseOwnedThumbnails();
    }

    void ApplyValueTextStyleFromCancelButton()
    {
        if (cancelButton == null || slots == null)
            return;

        TMP_Text cancelLabel = cancelButton.GetComponentInChildren<TMP_Text>(true);
        if (cancelLabel == null)
            return;

        for (int i = 0; i < slots.Length; i++)
            slots[i]?.ApplyValueTextStyle(cancelLabel);
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
        ReleaseOwnedThumbnails();

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
                Texture2D thumb = TryLoadThumbnailTexture(saves[i]);
                if (thumb != null)
                    _ownedThumbnails.Add(thumb);
                slot.Bind(saves[i], thumb);
            }
            else
            {
                slot.ClearBinding();
                slot.gameObject.SetActive(false);
            }
        }

        ApplyValueTextStyleFromCancelButton();
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

    static Texture2D TryLoadThumbnailTexture(SaveSlotMetadata metadata)
    {
        if (metadata == null || !metadata.thumbnailAvailable)
            return null;

        string path = SaveFileIO.GetThumbnailPath(metadata.slotId);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes == null || bytes.Length == 0)
                return null;

            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!texture.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            return texture;
        }
        catch (Exception)
        {
            return null;
        }
    }

    void ReleaseOwnedThumbnails()
    {
        for (int i = 0; i < _ownedThumbnails.Count; i++)
        {
            if (_ownedThumbnails[i] != null)
                Destroy(_ownedThumbnails[i]);
        }

        _ownedThumbnails.Clear();
    }
}
