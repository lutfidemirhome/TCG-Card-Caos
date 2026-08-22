using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authored Load Game overlay. Shows/hides the panel; save data wiring comes later.
/// </summary>
public class LoadGamePanelView : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] Button cancelButton;
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
        Hide();
    }

    public void Show()
    {
        if (root != null)
            root.SetActive(true);
        else
            gameObject.SetActive(true);
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
