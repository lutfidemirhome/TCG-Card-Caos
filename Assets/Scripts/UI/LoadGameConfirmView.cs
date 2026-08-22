using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Load Game confirmation overlay. Visuals live in the scene hierarchy under Panel_LoadConfirm;
/// this component only toggles visibility and exposes the Yes/No buttons.
/// </summary>
public class LoadGameConfirmView : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] Button yesButton;
    [SerializeField] Button noButton;

    public bool IsOpen => root != null && root.activeSelf;

    public Button YesButton => yesButton;
    public Button NoButton => noButton;

    void Awake()
    {
        EnsureReferences();
    }

    void EnsureReferences()
    {
        if (root == null)
            root = gameObject;

        Transform searchRoot = root.transform;
        if (yesButton == null)
            yesButton = searchRoot.Find("Band/ButtonRow/Button_Yes")?.GetComponent<Button>();
        if (noButton == null)
            noButton = searchRoot.Find("Band/ButtonRow/Button_No")?.GetComponent<Button>();
    }

    public void Show()
    {
        EnsureReferences();

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
