using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authored Load Game overlay. Shows/hides the panel; save data wiring comes later.
/// </summary>
public class LoadGamePanelView : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] Button cancelButton;
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] LoadGameSlotView[] slots;

    public bool IsOpen => root != null && root.activeSelf;

    void Awake()
    {
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(Hide);
            cancelButton.onClick.AddListener(Hide);
        }

        BindPlaceholderSlots();
        ApplyValueTextStyleFromCancelButton();
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

    public void Show()
    {
        if (root != null)
            root.SetActive(true);
        else
            gameObject.SetActive(true);

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
        if (root != null)
            root.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    void BindPlaceholderSlots()
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].BindPlaceholder(i + 1);
        }
    }
}
