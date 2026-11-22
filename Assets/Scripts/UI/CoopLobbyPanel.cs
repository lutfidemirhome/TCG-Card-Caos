using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CoopLobbyPanel : MonoBehaviour
{
    const int RoomsPerPage = 4;
    GameObject overlay;
    RectTransform panel, contents;
    CanvasGroup menuGroup;
    TMP_Text menuLabel;
    bool previousInteractable, preview, dirty;
    bool creating, previewSetup, inviteOnly, previewLocked, wasPrompt;
    string formPassword = string.Empty;
    int page;

    public static void Install(MainMenuView menu, Button entry)
    {
        var view = menu.gameObject.AddComponent<CoopLobbyPanel>();
        view.menuLabel = entry.GetComponentInChildren<TMP_Text>(true);
        view.menuGroup = menu.GetComponent<CanvasGroup>();
        if (!view.menuGroup) view.menuGroup = menu.gameObject.AddComponent<CanvasGroup>();
        entry.onClick.AddListener(view.Open);
    }

    void OnEnable()
    {
        CoopSession.Changed += MarkDirty;
        Localization.LanguageChanged += MarkDirty;
        dirty = true;
    }

    void OnDisable()
    {
        formPassword = string.Empty;
        CoopSession.Changed -= MarkDirty;
        Localization.LanguageChanged -= MarkDirty;
        if (overlay && overlay.activeSelf && menuGroup)
        {
            overlay.SetActive(false);
            menuGroup.interactable = previousInteractable;
        }
    }

    void MarkDirty() => dirty = true;

    void Open()
    {
        if (!CoopSession.CanEnter) return;
        page = 0;
        CoopSession.ShowLobby(true);
        CoopSession.Rooms?.Search();
    }

    void Update()
    {
        if (GameSceneLoader.IsLoading) return;
        if (dirty)
        {
            dirty = false;
            if (CoopSession.ShowLobbyOnMenu) Render();
            else if (overlay && overlay.activeSelf)
            {
                overlay.SetActive(false);
                menuGroup.interactable = previousInteractable;
            }
        }
        if (overlay && overlay.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Back();
    }

    void Back()
    {
        if (preview) { preview = false; MarkDirty(); return; }
        var rooms = CoopSession.Rooms;
        if (creating || rooms?.NeedsPassword == true)
        {
            creating = false;
            formPassword = string.Empty;
            rooms?.Leave();
            MarkDirty();
            return;
        }
        if (rooms != null && rooms.InRoom) { rooms.Leave(); return; }
        rooms?.Leave();
        CoopSession.ShowLobby(false);
    }

    void BuildPanel()
    {
        RectTransform shade = Rect("Co-op", transform, Vector2.zero, Vector2.zero);
        shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one;
        shade.offsetMin = shade.offsetMax = Vector2.zero;
        overlay = shade.gameObject;
        overlay.AddComponent<CanvasGroup>().ignoreParentGroups = true;
        var blocker = overlay.AddComponent<Image>();
        blocker.color = new Color(.035f, .055f, .09f, .9f);
        panel = Rect("Panel", shade, new Vector2(940, 820), Vector2.zero);
        var image = panel.gameObject.AddComponent<Image>();
        SettingsArt.Apply(image, SettingsArt.Panel, new Color(.14f, .20f, .28f));
        image.preserveAspect = false;
        Text(panel, "Co-op", new Vector2(760, 80), new Vector2(0, 326), 64);
        overlay.SetActive(false);
    }

    void Render()
    {
        if (!menuGroup) return;
        if (!overlay) BuildPanel();
        if (!overlay.activeSelf)
        {
            previousInteractable = menuGroup.interactable;
            menuGroup.interactable = false;
            overlay.SetActive(true);
            overlay.transform.SetAsLastSibling();
        }
        if (contents)
        {
            contents.gameObject.SetActive(false);
            Destroy(contents.gameObject);
        }
        contents = Rect("Room controls", panel, panel.sizeDelta, Vector2.zero);
        var room = CoopSession.Rooms;
        bool prompt = room?.NeedsPassword == true;
        if (prompt != wasPrompt)
        {
            formPassword = string.Empty;
            wasPrompt = prompt;
            if (prompt) creating = false;
        }
        if (preview || (room != null && room.InRoom))
        {
            creating = false;
            formPassword = string.Empty;
            string title = preview ? T(LocalizationKeys.CoopPreview)
                : T(LocalizationKeys.CoopMembers) + "   " + room.Members.Count + " / " + CoopSession.Capacity;
            Text(contents, title, new Vector2(740, 60), new Vector2(0, 240), 40);
            string access = T((preview ? inviteOnly : room.IsPrivate) ? LocalizationKeys.CoopPrivate : LocalizationKeys.CoopPublic);
            if (preview ? previewLocked : room.IsLocked) access += " · " + T(LocalizationKeys.CoopLocked);
            Text(contents, access, new Vector2(740, 38), new Vector2(0, 195), 26);
            if (preview)
                Text(contents, "1 / " + CoopSession.Capacity, new Vector2(680, 55), new Vector2(0, 133), 36);
            else
                for (int i = 0; i < room.Members.Count; i++)
                    Text(contents, room.Members[i], new Vector2(680, 48), new Vector2(0, 160 - i * 54), 34);
            bool host = preview || room.IsHost;
            if (!preview) Button(T(LocalizationKeys.CoopInvite), -198, -140, 345, room.Invite);
            if (host) Button(T(LocalizationKeys.CoopStart), preview ? 0 : 198, -140, 345,
                () => { if (preview) CoopSession.BeginShop(); else room.StartShop(); });
            string status = room?.StatusKey;
            if (!string.IsNullOrEmpty(status) || !host)
                Note(!string.IsNullOrEmpty(status) ? status : LocalizationKeys.CoopWaitHost);
            Button(T(LocalizationKeys.CoopLeave), 0, -306, 340, Back, backButton: true);
            return;
        }
        if (prompt || creating)
        {
            DrawRoomForm(room, prompt);
            return;
        }

        if (room == null || !room.Online)
        {
            Text(contents, T(LocalizationKeys.CoopOffline), new Vector2(750, 130), new Vector2(0, 98), 36);
#if UNITY_EDITOR
            Button(T(LocalizationKeys.CoopPreview), 0, -85, 460, () => OpenCreate(true));
#endif
        }
        else
        {
            Button(T(LocalizationKeys.CoopCreate), -198, 206, 340, () => OpenCreate(false), !room.Busy);
            Button(T(LocalizationKeys.CoopRefresh), 198, 206, 340, () => { page = 0; room.Search(); }, !room.Busy);
            int pages = Mathf.Max(1, (room.PublicRooms.Count + RoomsPerPage - 1) / RoomsPerPage);
            page = Mathf.Clamp(page, 0, pages - 1);
            for (int i = page * RoomsPerPage; i < Mathf.Min((page + 1) * RoomsPerPage, room.PublicRooms.Count); i++)
            {
                var item = room.PublicRooms[i];
                string lockLabel = item.PasswordRequired ? " · " + T(LocalizationKeys.CoopLocked) : string.Empty;
                Button(item.HostName + "   " + item.Players + " / " + item.Capacity + lockLabel, 0, 111 - (i % RoomsPerPage) * 74, 670, () => room.Join(item.Id), !room.Busy);
            }
            if (pages > 1)
            {
                Button("‹", -150, -191, 85, () => { page--; MarkDirty(); }, page > 0);
                Text(contents, (page + 1) + " / " + pages, new Vector2(170, 48), new Vector2(0, -191), 30);
                Button("›", 150, -191, 85, () => { page++; MarkDirty(); }, page < pages - 1);
            }
            if (!string.IsNullOrEmpty(room.StatusKey)) Note(room.StatusKey);
        }
        Button(T(LocalizationKeys.CoopBack), 0, -306, 340, Back, backButton: true);
    }

    void OpenCreate(bool localPreview)
    {
        creating = true;
        previewSetup = localPreview;
        inviteOnly = false;
        formPassword = string.Empty;
        MarkDirty();
    }

    void DrawRoomForm(ICoopRooms room, bool joining)
    {
        bool busy = room?.Busy == true;
        bool available = room?.Online == true || (!joining && previewSetup);
        Text(contents, T(joining ? LocalizationKeys.CoopLocked : LocalizationKeys.CoopCreate),
            new Vector2(750, 65), new Vector2(0, 224), 42);
        if (!joining)
        {
            Button(T(LocalizationKeys.CoopPublic), -192, 120, 350, () => { inviteOnly = false; MarkDirty(); }, inviteOnly && !busy);
            Button(T(LocalizationKeys.CoopPrivate), 192, 120, 350, () => { inviteOnly = true; MarkDirty(); }, !inviteOnly && !busy);
            var selection = Rect("Selected visibility", contents, new Vector2(112, 5), new Vector2(inviteOnly ? 192 : -192, 81));
            var underline = selection.gameObject.AddComponent<Image>();
            underline.color = new Color(.95f, .82f, .29f);
            underline.raycastTarget = false;
        }
        Text(contents, T(joining ? LocalizationKeys.CoopPassword : LocalizationKeys.CoopOptionalPassword),
            new Vector2(740, 54), new Vector2(0, 24), 32);
        var input = PasswordField(available && !busy);
        Action submit = () =>
        {
            if (joining)
            {
                if (!string.IsNullOrEmpty(formPassword)) room.SubmitPassword(formPassword);
                return;
            }
            if (previewSetup)
            {
                preview = true;
                previewLocked = formPassword.Length > 0;
                formPassword = string.Empty;
                MarkDirty();
            }
            else room?.Host(inviteOnly, formPassword);
        };
        var confirm = Button(T(joining ? LocalizationKeys.CoopJoin : LocalizationKeys.CoopCreate),
            0, -155, 380, submit, available && !busy && (!joining || formPassword.Length > 0));
        input.onValueChanged.AddListener(value =>
        {
            formPassword = value;
            confirm.interactable = available && !busy && (!joining || value.Length > 0);
        });
        input.onSubmit.AddListener(_ => { if (confirm.IsInteractable()) submit(); });
        if (!available) Note(LocalizationKeys.CoopOffline);
        else if (room?.StatusKey == LocalizationKeys.CoopFailed || room?.StatusKey == LocalizationKeys.CoopWorking
            || room?.StatusKey == LocalizationKeys.CoopWrongPassword) Note(room.StatusKey);
        Button(T(LocalizationKeys.CoopBack), 0, -306, 340, Back, backButton: true);
    }

    TMP_InputField PasswordField(bool enabled)
    {
        var box = Rect("Password", contents, new Vector2(550, 66), new Vector2(0, -40));
        var background = box.gameObject.AddComponent<Image>();
        background.color = new Color(.08f, .12f, .24f);
        var viewport = Rect("Viewport", box, new Vector2(510, 60), Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();
        var label = Text(viewport, string.Empty, viewport.sizeDelta, Vector2.zero, 34);
        label.enableAutoSizing = false;
        label.fontSize = 34;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        var input = box.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = background;
        input.textViewport = viewport;
        input.textComponent = label;
        input.fontAsset = label.font;
        input.contentType = TMP_InputField.ContentType.Password;
        input.asteriskChar = '*';
        input.characterLimit = CoopSession.MaxPasswordLength;
        input.SetTextWithoutNotify(formPassword);
        input.interactable = enabled;
        return input;
    }

    static string T(string key) => Localization.Get(key);
    void Note(string key) => Text(contents, T(key), new Vector2(790, 55), new Vector2(0, -244), 28);

    Button Button(string caption, float x, float y, float width, Action click, bool enabled = true, bool backButton = false)
    {
        RectTransform rect = Rect("Button", contents, new Vector2(width, 66), new Vector2(x, y));
        var image = rect.gameObject.AddComponent<Image>();
        SettingsArt.Apply(image, backButton ? SettingsArt.ButtonCancel : SettingsArt.ButtonSave, new Color(.22f, .52f, .73f));
        image.preserveAspect = false;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.interactable = enabled;
        button.onClick.AddListener(() => click());
        Text(rect, caption, new Vector2(width - 36, 55), Vector2.zero, 34);
        return button;
    }

    TMP_Text Text(Transform parent, string value, Vector2 size, Vector2 position, float fontSize)
    {
        var label = Rect("Label", parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
        if (menuLabel && menuLabel.font)
        {
            label.font = menuLabel.font;
            label.fontSharedMaterial = menuLabel.fontSharedMaterial;
        }
        else UiMenuFont.ApplyControl(label);
        label.richText = false;
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = fontSize * .65f;
        label.fontSizeMax = fontSize;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.text = value;
        return label;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }
}
