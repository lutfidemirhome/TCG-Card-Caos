using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One save row on the Load Game list. Art lives on the child Images; this only fills text/thumb.
/// </summary>
public class LoadGameSlotView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Button selectButton;
    [SerializeField] RawImage thumbnail;
    [SerializeField] TMP_Text saveNameText;
    [SerializeField] TMP_Text dateValueText;
    [SerializeField] TMP_Text playTimeValueText;
    [SerializeField] TMP_Text cardsValueText;
    [SerializeField] TMP_Text shelvesValueText;

    string _boundSlotId;
    bool _isEmpty;
    SaveSlotMetadata _boundMetadata;
    Texture _boundThumbnail;
    bool _boundAsSaveRow;
    bool _boundAsEmpty;
    int _placeholderIndex;

    public Button SelectButton => selectButton;
    public string BoundSlotId => _boundSlotId;
    public bool IsEmpty => _isEmpty;
    public Action<LoadGameSlotView> RightClicked;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Right)
            return;

        RightClicked?.Invoke(this);
    }

    void OnEnable()
    {
        Localization.LanguageChanged += RefreshBoundText;
    }

    void OnDisable()
    {
        Localization.LanguageChanged -= RefreshBoundText;
    }

    public void ClearBinding()
    {
        _boundSlotId = null;
        _isEmpty = true;
        _boundMetadata = null;
        _boundAsEmpty = false;
        BindPlaceholder(1);
    }

    public void BindEmpty(string slotId, Texture emptyThumbnail = null)
    {
        _boundSlotId = slotId;
        _isEmpty = true;
        _boundMetadata = null;
        _boundAsSaveRow = false;
        _boundAsEmpty = true;
        _boundThumbnail = emptyThumbnail;
        string na = Localization.Get(LocalizationKeys.SaveGameNotAvailable);
        Bind(
            saveName: Localization.Get(LocalizationKeys.SaveGameEmptySlot),
            dateText: na,
            playTimeText: na,
            cardsText: na,
            shelvesText: na,
            thumbnailTexture: emptyThumbnail,
            emptyThumbnail: emptyThumbnail == null);
    }

    public void BindPlaceholder(int autoSaveIndex)
    {
        _boundSlotId = null;
        _boundMetadata = null;
        _boundAsEmpty = false;
        _boundAsSaveRow = false;
        _placeholderIndex = autoSaveIndex;
        Bind(
            saveName: Localization.Format(LocalizationKeys.SaveGameAutoName, autoSaveIndex),
            dateText: "—",
            playTimeText: "00:00",
            cardsPlaced: 0,
            cardsCapacity: 400,
            shelvesPlaced: 0,
            shelvesCapacity: 100,
            thumbnailTexture: null);
    }

    public void Bind(SaveSlotMetadata metadata, Texture thumbnailTexture)
    {
        if (metadata == null)
        {
            ClearBinding();
            return;
        }

        _isEmpty = false;
        _boundSlotId = metadata.slotId;
        _boundMetadata = metadata;
        _boundThumbnail = thumbnailTexture;
        _boundAsSaveRow = false;
        _boundAsEmpty = false;
        Bind(
            saveName: FormatSaveName(metadata),
            dateText: FormatDate(metadata),
            playTimeText: FormatPlayTime(metadata.playTimeSeconds),
            cardsText: metadata.cardsPlaced + " / " + Mathf.Max(metadata.totalCards, metadata.cardsPlaced),
            shelvesText: metadata.shelvesCompleted + " / " + Mathf.Max(GameHudLimits.MaxShelves, metadata.shelvesCompleted),
            thumbnailTexture: thumbnailTexture,
            emptyThumbnail: thumbnailTexture == null && _isEmpty);
    }

    public void BindSaveRow(SaveSlotMetadata metadata, Texture thumbnailTexture)
    {
        if (metadata == null)
        {
            ClearBinding();
            return;
        }

        _isEmpty = false;
        _boundSlotId = metadata.slotId;
        _boundMetadata = metadata;
        _boundThumbnail = thumbnailTexture;
        _boundAsSaveRow = true;
        _boundAsEmpty = false;
        Bind(
            saveName: FormatSaveName(metadata),
            dateText: FormatDateLong(metadata),
            playTimeText: FormatPlayTime(metadata.playTimeSeconds),
            cardsText: metadata.cardsPlaced + " / " + Mathf.Max(metadata.totalCards, metadata.cardsPlaced),
            shelvesText: metadata.shelvesCompleted + " / " + Mathf.Max(GameHudLimits.MaxShelves, metadata.shelvesCompleted),
            thumbnailTexture: thumbnailTexture,
            emptyThumbnail: false);
    }

    public void Bind(
        string saveName,
        string dateText,
        string playTimeText,
        int cardsPlaced,
        int cardsCapacity,
        int shelvesPlaced,
        int shelvesCapacity,
        Texture thumbnailTexture)
    {
        Bind(
            saveName,
            dateText,
            playTimeText,
            cardsPlaced + " / " + cardsCapacity,
            shelvesPlaced + " / " + shelvesCapacity,
            thumbnailTexture,
            emptyThumbnail: false);
    }

    void Bind(
        string saveName,
        string dateText,
        string playTimeText,
        string cardsText,
        string shelvesText,
        Texture thumbnailTexture,
        bool emptyThumbnail)
    {
        SetText(saveNameText, saveName);
        SetText(dateValueText, dateText);
        SetText(playTimeValueText, playTimeText);
        SetText(cardsValueText, cardsText);
        SetText(shelvesValueText, shelvesText);

        if (thumbnail == null)
            return;

        if (emptyThumbnail)
        {
            thumbnail.texture = thumbnailTexture;
            thumbnail.color = new Color(0.32f, 0.38f, 0.46f, 1f);
            return;
        }

        if (thumbnailTexture != null)
            thumbnail.texture = thumbnailTexture;
        thumbnail.color = Color.white;
    }

    void RefreshBoundText()
    {
        if (_boundAsEmpty)
        {
            BindEmpty(_boundSlotId, _boundThumbnail);
            return;
        }

        if (_boundMetadata != null)
        {
            if (_boundAsSaveRow)
                BindSaveRow(_boundMetadata, _boundThumbnail);
            else
                Bind(_boundMetadata, _boundThumbnail);
            return;
        }

        if (_placeholderIndex > 0)
            BindPlaceholder(_placeholderIndex);
    }

    static string FormatSaveName(SaveSlotMetadata metadata)
    {
        int displayIndex = metadata.slotIndex + 1;
        if (metadata.slotType == SaveSlotType.Auto)
            return Localization.Format(LocalizationKeys.SaveGameAutoName, displayIndex);

        return Localization.Format(LocalizationKeys.SaveGameManualName, displayIndex);
    }

    static string FormatDate(SaveSlotMetadata metadata)
    {
        try
        {
            DateTimeOffset local = metadata.TimestampUtc.ToLocalTime();
            return local.ToString("yyyy-MM-dd HH:mm");
        }
        catch (Exception)
        {
            return "—";
        }
    }

    static string FormatDateLong(SaveSlotMetadata metadata)
    {
        try
        {
            DateTimeOffset local = metadata.TimestampUtc.ToLocalTime();
            return local.ToString("MMM d, yyyy, h:mm:ss tt");
        }
        catch (Exception)
        {
            return Localization.Get(LocalizationKeys.SaveGameNotAvailable);
        }
    }

    static string FormatPlayTime(double playTimeSeconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt((float)playTimeSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        if (minutes >= 60)
        {
            int hours = minutes / 60;
            minutes %= 60;
            return hours.ToString("00") + ":" + minutes.ToString("00") + ":" + seconds.ToString("00");
        }

        return minutes.ToString("00") + ":" + seconds.ToString("00");
    }

    static void SetText(TMP_Text label, string value)
    {
        if (label == null)
            return;

        label.text = value ?? string.Empty;
    }

    public void ApplyValueTextStyle(TMP_Text styleSource)
    {
        if (styleSource == null)
            return;

        Material material = styleSource.fontSharedMaterial;
        Color color = styleSource.color;

        ApplyValueTextStyle(dateValueText, material, color);
        ApplyValueTextStyle(playTimeValueText, material, color);
        ApplyValueTextStyle(cardsValueText, material, color);
        ApplyValueTextStyle(shelvesValueText, material, color);
    }

    static void ApplyValueTextStyle(TMP_Text label, Material material, Color color)
    {
        if (label == null || material == null)
            return;

        label.fontSharedMaterial = material;
        label.color = color;
    }
}
