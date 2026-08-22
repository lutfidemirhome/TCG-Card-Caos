using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One save row on the Load Game list. Art lives on the child Images; this only fills text/thumb.
/// </summary>
public class LoadGameSlotView : MonoBehaviour
{
    [SerializeField] Button selectButton;
    [SerializeField] RawImage thumbnail;
    [SerializeField] TMP_Text saveNameText;
    [SerializeField] TMP_Text dateValueText;
    [SerializeField] TMP_Text playTimeValueText;
    [SerializeField] TMP_Text cardsValueText;
    [SerializeField] TMP_Text shelvesValueText;

    public Button SelectButton => selectButton;

    public void BindPlaceholder(int autoSaveIndex)
    {
        Bind(
            saveName: "Auto Save " + autoSaveIndex,
            dateText: "—",
            playTimeText: "00:00",
            cardsPlaced: 0,
            cardsCapacity: 400,
            shelvesPlaced: 0,
            shelvesCapacity: 100,
            thumbnailTexture: null);
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
        SetText(saveNameText, saveName);
        SetText(dateValueText, dateText);
        SetText(playTimeValueText, playTimeText);
        SetText(cardsValueText, cardsPlaced + " / " + cardsCapacity);
        SetText(shelvesValueText, shelvesPlaced + " / " + shelvesCapacity);

        if (thumbnail != null)
        {
            thumbnail.texture = thumbnailTexture;
            thumbnail.color = thumbnailTexture != null
                ? Color.white
                : new Color(0.22f, 0.26f, 0.34f, 1f);
        }
    }

    static void SetText(TMP_Text label, string value)
    {
        if (label == null)
            return;

        label.text = value ?? string.Empty;
        UiTextFit.Apply(label);
    }
}
