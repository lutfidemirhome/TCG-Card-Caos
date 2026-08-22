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
    }

    public void Show()
    {
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
