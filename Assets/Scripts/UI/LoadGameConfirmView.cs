using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Load Game confirmation overlay. Dress Images under Panel_LoadConfirm; logic stays here.
/// </summary>
public class LoadGameConfirmView : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] Button yesButton;
    [SerializeField] Button noButton;
    [SerializeField] TMP_Text messageText;

    public bool IsOpen => root != null && root.activeSelf;

    public Button YesButton => yesButton;
    public Button NoButton => noButton;

    public void Configure(GameObject rootObject, Button yes, Button no, TMP_Text message)
    {
        root = rootObject;
        yesButton = yes;
        noButton = no;
        messageText = message;
        DressFromResources();
    }

    public void DressFromResources()
    {
        if (root == null)
            root = gameObject;

        Transform band = root.transform.Find("Band");
        if (band != null)
        {
            Image bandImage = band.GetComponent<Image>();
            Sprite bandSprite = LoadGameConfirmArt.Band;
            if (bandImage != null && bandSprite != null)
            {
                bandImage.sprite = bandSprite;
                bandImage.type = Image.Type.Sliced;
                bandImage.color = Color.white;
            }

            RectTransform bandRect = band as RectTransform;
            if (bandRect != null)
                bandRect.sizeDelta = new Vector2(0f, 395f);
        }

        ApplyButtonSprite(root.transform, "Band/ButtonRow/Button_Yes", LoadGameConfirmArt.YesButton);
        ApplyButtonSprite(root.transform, "Band/ButtonRow/Button_No", LoadGameConfirmArt.NoButton);
        ApplyButtonSprite(root.transform, "Button_Yes", LoadGameConfirmArt.YesButton);
        ApplyButtonSprite(root.transform, "Button_No", LoadGameConfirmArt.NoButton);
    }

    static void ApplyButtonSprite(Transform root, string path, Sprite sprite)
    {
        if (root == null || sprite == null)
            return;

        Transform target = root.Find(path);
        if (target == null)
            return;

        Image image = target.GetComponent<Image>();
        if (image == null)
            return;

        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.preserveAspect = true;

        Transform label = target.Find("Label");
        if (label != null)
            label.gameObject.SetActive(false);
    }

    public void Show()
    {
        DressFromResources();

        if (messageText != null)
            messageText.text = Localization.Get(LocalizationKeys.LoadGameConfirmMessage);

        if (root != null)
            root.SetActive(true);
        else
            gameObject.SetActive(true);

        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
        else
            gameObject.SetActive(false);
    }
}
