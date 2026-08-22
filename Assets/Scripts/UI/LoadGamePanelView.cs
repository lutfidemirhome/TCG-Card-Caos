using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authored Load Game overlay. Lists save slots; clicking one asks for confirm, Yes loads that save.
/// </summary>
public class LoadGamePanelView : MonoBehaviour
{
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

    void Start()
    {
        if (confirmView != null)
            confirmView.DressFromResources();
    }

    void EnsureConfirmView()
    {
        if (confirmView == null)
            confirmView = GetComponentInChildren<LoadGameConfirmView>(true);

        if (confirmView != null)
            return;

        confirmView = BuildRuntimeConfirmOverlay();
    }

    LoadGameConfirmView BuildRuntimeConfirmOverlay()
    {
        var rootGo = new GameObject("Panel_LoadConfirm", typeof(RectTransform));
        rootGo.transform.SetParent(transform, false);
        RectTransform root = (RectTransform)rootGo.transform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.SetAsLastSibling();

        var view = rootGo.AddComponent<LoadGameConfirmView>();

        var dimmerGo = new GameObject("Dimmer", typeof(RectTransform));
        dimmerGo.transform.SetParent(root, false);
        RectTransform dimmer = (RectTransform)dimmerGo.transform;
        dimmer.anchorMin = Vector2.zero;
        dimmer.anchorMax = Vector2.one;
        dimmer.offsetMin = Vector2.zero;
        dimmer.offsetMax = Vector2.zero;
        var dimmerImage = dimmerGo.AddComponent<Image>();
        dimmerImage.color = new Color(0.02f, 0.04f, 0.10f, 0.82f);
        dimmerImage.raycastTarget = true;

        var bandGo = new GameObject("Band", typeof(RectTransform));
        bandGo.transform.SetParent(root, false);
        RectTransform band = (RectTransform)bandGo.transform;
        band.anchorMin = new Vector2(0f, 0.5f);
        band.anchorMax = new Vector2(1f, 0.5f);
        band.pivot = new Vector2(0.5f, 0.5f);
        band.anchoredPosition = Vector2.zero;
        band.sizeDelta = new Vector2(0f, 395f);
        var bandImage = bandGo.AddComponent<Image>();
        Sprite bandSprite = LoadGameConfirmArt.Band;
        bandImage.sprite = bandSprite;
        bandImage.type = bandSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        bandImage.color = bandSprite != null ? Color.white : new Color(0.12f, 0.22f, 0.42f, 1f);
        bandImage.raycastTarget = true;

        var messageGo = new GameObject("Message", typeof(RectTransform));
        messageGo.transform.SetParent(band, false);
        RectTransform messageRect = (RectTransform)messageGo.transform;
        messageRect.anchorMin = messageRect.anchorMax = messageRect.pivot = new Vector2(0.5f, 0.5f);
        messageRect.anchoredPosition = new Vector2(0f, 92f);
        messageRect.sizeDelta = new Vector2(1500f, 90f);
        var message = messageGo.AddComponent<TextMeshProUGUI>();
        message.text = Localization.Get(LocalizationKeys.LoadGameConfirmMessage);
        message.alignment = TextAlignmentOptions.Center;
        message.fontSize = 52f;
        message.color = Color.white;
        message.fontStyle = FontStyles.Bold;
        message.outlineWidth = 0.28f;
        message.outlineColor = Color.black;
        message.raycastTarget = false;

        var rowGo = new GameObject("ButtonRow", typeof(RectTransform));
        rowGo.transform.SetParent(band, false);
        RectTransform row = (RectTransform)rowGo.transform;
        row.anchorMin = row.anchorMax = row.pivot = new Vector2(0.5f, 0.5f);
        row.anchoredPosition = new Vector2(0f, -82f);
        row.sizeDelta = new Vector2(560f, 95f);
        var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 72f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        Button yes = BuildRuntimeChoiceButton(row, "Button_Yes", LoadGameConfirmArt.YesButton);
        Button no = BuildRuntimeChoiceButton(row, "Button_No", LoadGameConfirmArt.NoButton);

        view.Configure(rootGo, yes, no, message);
        rootGo.SetActive(false);
        return view;
    }

    static Button BuildRuntimeChoiceButton(Transform parent, string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        var layout = go.AddComponent<LayoutElement>();
        layout.preferredWidth = 244f;
        layout.preferredHeight = 95f;
        layout.minWidth = 244f;
        layout.minHeight = 95f;
        rect.sizeDelta = new Vector2(244f, 95f);

        var image = go.AddComponent<Image>();
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            image.preserveAspect = true;
        }
        else
        {
            image.color = name.Contains("Yes")
                ? new Color(0.45f, 0.85f, 0.20f, 1f)
                : new Color(0.95f, 0.42f, 0.12f, 1f);
        }

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
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
            OnConfirmYes();
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
