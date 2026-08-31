using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom-left skill slot for video builds: Hand only (key 1). Double Jump hidden.
/// </summary>
public class SkillBarView : MonoBehaviour
{
    const float ButtonSize = 108f;
    const float Margin = 28f;

    static readonly Color IdleColor = new Color(0.08f, 0.08f, 0.09f, 0.82f);

    void Awake()
    {
        PlayerJumpSkill.DoubleJumpArmed = false;
        HideLegacyJumpButton();
        EnsureSlot(
            "Button_Hand",
            "Hand",
            "1",
            new Vector2(Margin, Margin));
    }

    void Update()
    {
        if (GamePause.IsPaused)
            return;

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)
            || Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            PlayerHandSkill.TryActivate();
    }

    void HideLegacyJumpButton()
    {
        Transform jump = transform.Find("Button_DoubleJump");
        if (jump != null)
            jump.gameObject.SetActive(false);
    }

    Image EnsureSlot(string objectName, string label, string keyHint, Vector2 anchoredPosition)
    {
        Transform existing = transform.Find(objectName);
        if (existing == null)
            existing = CreateSlot(transform, objectName, label, keyHint, anchoredPosition);
        else
        {
            EnsureKeyHint(existing, keyHint);
            RepositionSlot(existing, anchoredPosition);
        }

        return existing.GetComponent<Image>();
    }

    static void RepositionSlot(Transform button, Vector2 anchoredPosition)
    {
        if (button == null)
            return;

        var rect = button as RectTransform;
        if (rect == null)
            rect = button.GetComponent<RectTransform>();

        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
    }

    static Transform CreateSlot(
        Transform canvas,
        string objectName,
        string label,
        string keyHint,
        Vector2 anchoredPosition)
    {
        var buttonGo = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        buttonGo.transform.SetParent(canvas, false);

        var rect = (RectTransform)buttonGo.transform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);

        var image = buttonGo.GetComponent<Image>();
        image.color = IdleColor;
        image.raycastTarget = false;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
        labelGo.transform.SetParent(buttonGo.transform, false);
        var labelRect = (RectTransform)labelGo.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 8f);
        labelRect.offsetMax = new Vector2(-8f, -8f);

        var text = labelGo.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = 22f;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        if (UiMenuFont.Font != null)
            text.font = UiMenuFont.Font;

        EnsureKeyHint(buttonGo.transform, keyHint);
        return buttonGo.transform;
    }

    static void EnsureKeyHint(Transform button, string keyHint)
    {
        if (button == null)
            return;

        Transform existing = button.Find("KeyHint");
        if (existing != null)
        {
            TMP_Text existingText = existing.GetComponent<TMP_Text>();
            if (existingText != null)
                existingText.text = keyHint;
            return;
        }

        var hintGo = new GameObject("KeyHint", typeof(RectTransform), typeof(CanvasRenderer));
        hintGo.transform.SetParent(button, false);
        var hintRect = (RectTransform)hintGo.transform;
        hintRect.anchorMin = new Vector2(0.5f, 1f);
        hintRect.anchorMax = new Vector2(0.5f, 1f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 4f);
        hintRect.sizeDelta = new Vector2(ButtonSize, 22f);

        var hint = hintGo.AddComponent<TextMeshProUGUI>();
        hint.text = keyHint;
        hint.alignment = TextAlignmentOptions.Center;
        hint.color = new Color(1f, 1f, 1f, 0.85f);
        hint.fontSize = 16f;
        hint.enableAutoSizing = false;
        hint.raycastTarget = false;
        if (UiMenuFont.Font != null)
            hint.font = UiMenuFont.Font;
    }
}
