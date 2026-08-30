using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom-left skill slot. Press 1 to arm Double Jump (Space uses 5x height).
/// </summary>
public class SkillBarView : MonoBehaviour
{
    const string ButtonName = "Button_DoubleJump";
    const float ButtonSize = 108f;
    const float Margin = 28f;

    static readonly Color IdleColor = new Color(0.08f, 0.08f, 0.09f, 0.82f);
    static readonly Color ArmedColor = new Color(0.22f, 0.42f, 0.28f, 0.92f);

    [SerializeField] Image doubleJumpBackground;

    void Awake()
    {
        PlayerJumpSkill.DoubleJumpArmed = false;
        EnsureButton();
        RefreshVisual();
    }

    void Update()
    {
        if (GamePause.IsPaused)
            return;

        if (!Input.GetKeyDown(KeyCode.Alpha1) && !Input.GetKeyDown(KeyCode.Keypad1))
            return;

        PlayerJumpSkill.DoubleJumpArmed = !PlayerJumpSkill.DoubleJumpArmed;
        RefreshVisual();
    }

    void EnsureButton()
    {
        if (doubleJumpBackground != null)
            return;

        Transform existing = transform.Find(ButtonName);
        if (existing == null)
            existing = CreateButton(transform);

        doubleJumpBackground = existing.GetComponent<Image>();
        EnsureKeyHint(existing);
    }

    void RefreshVisual()
    {
        if (doubleJumpBackground != null)
            doubleJumpBackground.color = PlayerJumpSkill.DoubleJumpArmed ? ArmedColor : IdleColor;
    }

    static Transform CreateButton(Transform canvas)
    {
        var buttonGo = new GameObject(ButtonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        buttonGo.transform.SetParent(canvas, false);

        var rect = (RectTransform)buttonGo.transform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(Margin, Margin);
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
        text.text = "Double Jump";
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = 22f;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        if (UiMenuFont.Font != null)
            text.font = UiMenuFont.Font;

        EnsureKeyHint(buttonGo.transform);
        return buttonGo.transform;
    }

    static void EnsureKeyHint(Transform button)
    {
        if (button == null || button.Find("KeyHint") != null)
            return;

        var hintGo = new GameObject("KeyHint", typeof(RectTransform), typeof(CanvasRenderer));
        hintGo.transform.SetParent(button, false);
        var hintRect = (RectTransform)hintGo.transform;
        hintRect.anchorMin = new Vector2(0.5f, 1f);
        hintRect.anchorMax = new Vector2(0.5f, 1f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 4f);
        hintRect.sizeDelta = new Vector2(ButtonSize, 22f);

        var hint = hintGo.AddComponent<TextMeshProUGUI>();
        hint.text = "1";
        hint.alignment = TextAlignmentOptions.Center;
        hint.color = new Color(1f, 1f, 1f, 0.85f);
        hint.fontSize = 16f;
        hint.enableAutoSizing = false;
        hint.raycastTarget = false;
        if (UiMenuFont.Font != null)
            hint.font = UiMenuFont.Font;
    }
}
