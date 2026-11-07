using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Keyboard-only dog shortcut and status indicator above the hand counter.</summary>
public sealed class DogHudToggle : MonoBehaviour
{
    PawIconGraphic _paw;
    GameObject _slash;
    bool _altWasHeld, _tapArmed;

    public static DogHudToggle Ensure(Transform hud)
    {
        var existing = hud.Find("DogIndicator");
        if (existing != null) return existing.GetComponent<DogHudToggle>();
        var rect = NewRect("DogIndicator", hud, new Vector2(48, 48));
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(-16, 158);
        var view = rect.gameObject.AddComponent<DogHudToggle>();
        view.Build(hud.Find("SkillUI/Hotbar/Skill0")?.GetComponent<Image>());
        return view;
    }

    void Build(Image source)
    {
        var background = gameObject.AddComponent<Image>();
        if (source != null)
        {
            background.sprite = source.sprite;
            background.type = source.type;
            background.color = Color.white;
        }
        else background.color = new Color(0, 0, 0, .6f);
        background.raycastTarget = false;
        var icon = NewRect("Paw", transform, new Vector2(34, 34));
        _paw = icon.gameObject.AddComponent<PawIconGraphic>();
        _paw.raycastTarget = false;
        var slash = NewRect("OffSlash", transform, new Vector2(34, 3));
        slash.localRotation = Quaternion.Euler(0, 0, 45);
        var slashImage = slash.gameObject.AddComponent<Image>();
        slashImage.color = new Color32(235, 235, 235, 255);
        slashImage.raycastTarget = false;
        _slash = slash.gameObject;
        var hint = NewRect("ShortcutHint", transform, new Vector2(64, 28));
        hint.anchoredPosition = new Vector2(0, -35);
        var label = hint.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = UiMenuFont.Font;
        label.fontSharedMaterial = UiMenuFont.Font.material;
        label.fontSize = 16; label.enableAutoSizing = true;
        label.fontSizeMin = 14; label.fontSizeMax = 16;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        // Use the physical key name, like the other HUD shortcuts, in every language.
        label.text = Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer
            ? "Option" : "Alt";
        Refresh();
    }

    static RectTransform NewRect(string name, Transform parent, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        return rect;
    }

    void OnEnable()
    {
        GameSettings.Changed += Refresh;
        Refresh();
    }

    void Refresh()
    {
        if (_paw == null) return;
        bool visible = GameSettings.Current.showDog;
        _paw.color = visible ? Color.white : new Color32(130, 130, 130, 255);
        _slash.SetActive(!visible);
    }

    static bool GameplayAvailable => !GamePause.IsPaused && !GameSceneLoader.IsLoading
        && CardInstancedRenderManager.IsGameplayReady && !WelcomePopupView.IsWaitingForStart
        && Cursor.lockState == CursorLockMode.Locked;

    void Update()
    {
        bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        if (!altHeld && !_altWasHeld) return;
        bool altDown = Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt);
        bool combination = (Input.anyKeyDown && !altDown) || Input.GetKey(KeyCode.Tab) || Input.GetKey(KeyCode.Escape)
            || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
            || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand)
            || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        ProcessShortcut(altHeld, combination, Application.isFocused, GameplayAvailable);
    }

    void ProcessShortcut(bool altHeld, bool combination, bool focused, bool available)
    {
        // A standalone tap toggles once on release. Holding cannot repeat, and Alt+Tab,
        // focus loss or menu shortcuts cannot accidentally save a different dog preference.
        if (!focused || !available) _tapArmed = false;
        else if (altHeld && !_altWasHeld) _tapArmed = true;
        if (combination) _tapArmed = false;
        if (!altHeld && _altWasHeld)
        {
            if (_tapArmed) GameSettings.SetDogVisible(!GameSettings.Current.showDog);
            _tapArmed = false;
        }
        _altWasHeld = altHeld;
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused) _tapArmed = false;
    }

    void OnDisable()
    {
        GameSettings.Changed -= Refresh;
        _altWasHeld = _tapArmed = false;
    }
}
