using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CoopLobbyPanel : MonoBehaviour
{
    const int RoomsPerPage = 4;
    GameObject overlay;
    RectTransform panel, contents;
    CanvasGroup menuGroup;
    TMP_Text menuLabel, heading;
    bool browsing, viewingFriends, revealCode;
    string codeDraft = string.Empty;
    readonly List<CoopFriend> friends = new List<CoopFriend>();
    readonly HashSet<ulong> invitations = new HashSet<ulong>();
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
        browsing = viewingFriends = revealCode = false;
        codeDraft = string.Empty;
        page = 0;
        CoopSession.ShowLobby(true);
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
        if (viewingFriends) { viewingFriends = false; page = 0; MarkDirty(); return; }
        if (preview) { preview = false; MarkDirty(); return; }
        var rooms = CoopSession.Rooms;
        if (creating || rooms?.NeedsPassword == true)
        {
            creating = false;
            formPassword = string.Empty;
            rooms?.Leave();
            if (browsing) rooms?.Search();
            MarkDirty();
            return;
        }
        if (rooms != null && rooms.InRoom)
        {
            revealCode = browsing = false;
            invitations.Clear();
            rooms.Leave();
            return;
        }
        if (browsing)
        {
            browsing = false;
            rooms?.Leave();
            MarkDirty();
            return;
        }
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
        heading = Text(panel, string.Empty, new Vector2(760, 80), new Vector2(0, 326), 64);
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
        heading.text = T(LocalizationKeys.CoopTitle);
        if (preview || room?.InRoom == true)
        {
            creating = browsing = false;
            codeDraft = formPassword = string.Empty;
            if (viewingFriends && room is ICoopRoomInvites inviteList) DrawFriends(room, inviteList);
            else DrawLobby(room);
            return;
        }
        viewingFriends = revealCode = false;
        invitations.Clear();
        if (prompt || creating)
        {
            DrawRoomForm(room, prompt);
            return;
        }

        bool online = room?.Online == true;
        if (browsing && online)
        {
            heading.text = T("coop.browse");
            Button(T(LocalizationKeys.CoopRefresh), 0, 207, 360, () => { page = 0; room.Search(); }, !room.Busy);
            Paginate(room.PublicRooms.Count);
            for (int i = page * RoomsPerPage; i < Mathf.Min((page + 1) * RoomsPerPage, room.PublicRooms.Count); i++)
            {
                var item = room.PublicRooms[i];
                string lockLabel = item.PasswordRequired ? " · " + T(LocalizationKeys.CoopLocked) : string.Empty;
                Button(item.HostName + "   " + item.Players + " / " + item.Capacity + lockLabel, 0, 111 - (i % RoomsPerPage) * 74, 670, () => room.Join(item.Id), !room.Busy);
            }
            if (!string.IsNullOrEmpty(room.StatusKey)) Note(room.StatusKey);
        }
        else
        {
            bool canUse = online && !room.Busy;
            Button(T(LocalizationKeys.CoopCreate), -190, 184, 355, () => OpenCreate(false), canUse);
            Button(T("coop.browse"), 190, 184, 355, () => { browsing = true; page = 0; room.Search(); MarkDirty(); }, canUse);
            var invites = room as ICoopRoomInvites;
            Text(contents, T("coop.room_code"), new Vector2(660, 50), new Vector2(0, 75), 34);
            var input = InputField(canUse && invites != null, false);
            var join = Button(T(LocalizationKeys.CoopJoin), 0, -88, 340, () => invites?.JoinByCode(codeDraft),
                canUse && invites != null && !string.IsNullOrWhiteSpace(codeDraft));
            input.onValueChanged.AddListener(value =>
            {
                codeDraft = value;
                join.interactable = canUse && invites != null && !string.IsNullOrWhiteSpace(value);
            });
            input.onSubmit.AddListener(_ => { if (join.IsInteractable()) invites.JoinByCode(codeDraft); });
            if (room is ICoopRoomControls c && c.CanReconnect)
                Button(T("coop.reconnect"), 0, -171, 425, c.Reconnect, !room.Busy);
#if UNITY_EDITOR
            if (!online) Button(T(LocalizationKeys.CoopPreview), 0, -171, 455, () => OpenCreate(true));
#endif
            if (!online) Note(LocalizationKeys.CoopOffline);
            else if (!string.IsNullOrEmpty(room.StatusKey)) Note(room.StatusKey);
        }
        Button(T(LocalizationKeys.CoopBack), 0, -306, 340, Back, backButton: true);
    }

    void DrawLobby(ICoopRooms room)
    {
        var controls = room as ICoopRoomControls;
        var invites = room as ICoopRoomInvites;
        bool host = preview || room.IsHost;
        int count = preview ? 1 : room.Members.Count;
        string access = T((preview ? inviteOnly : room.IsPrivate) ? LocalizationKeys.CoopPrivate : LocalizationKeys.CoopPublic);
        if (preview ? previewLocked : room.IsLocked) access += " · " + T(LocalizationKeys.CoopLocked);
        Text(contents, access + "   " + count + " / " + CoopSession.Capacity, new Vector2(750, 53), new Vector2(0, 245), 38);
        string code = preview ? null : invites?.RoomCode;
        if (!string.IsNullOrEmpty(code))
        {
            Button(revealCode ? code : T("coop.show_code"), -110, 177, 475, () => { revealCode = !revealCode; MarkDirty(); });
            Button(T("coop.copy"), 255, 177, 212, () => GUIUtility.systemCopyBuffer = code);
        }
        for (int i = 0; i < count; i++)
        {
            string name = preview ? T(LocalizationKeys.CoopPreview) : room.Members[i];
            bool ready = !preview && controls != null && controls.Ready(controls.Players[i]);
            if (ready) name += "  · " + T("coop.ready");
            float y = 107 - i * 45;
            var portrait = !preview && controls != null && room is ICoopMemberPortraits profiles
                ? profiles.GetPortrait(controls.Players[i]) : null;
            if (portrait)
            {
                var image = Rect("Steam avatar", contents, new Vector2(39, 39), new Vector2(-288, y)).gameObject.AddComponent<RawImage>();
                image.texture = portrait;
                image.uvRect = new Rect(0, 1, 1, -1);
                image.raycastTarget = false;
            }
            var label = Text(contents, name, new Vector2(550, 44), new Vector2(22, y), 33);
            if (ready) label.color = new Color(.56f, .94f, .62f);
        }
        if (!preview && controls != null && (!host || controls.Ready(controls.LocalId)))
            Button(T(controls.Ready(controls.LocalId) ? "coop.unready" : "coop.ready"), 0, -88, 400, controls.ToggleReady);
        else if (host)
        {
            bool canResume = !preview && controls?.CanResume == true;
            Button(T(canResume ? LocalizationKeys.CoopStart : "coop.ready"), canResume ? -196 : 0, -88, 370,
                () => { if (preview) CoopSession.BeginShop(); else room.StartShop(); });
            if (canResume) Button(T("coop.continue"), 196, -88, 370, controls.ContinueShop);
        }
        if (!preview)
            Button(T(LocalizationKeys.CoopInvite), 0, -166, 455, () =>
            {
                if (invites == null) room.Invite();
                else { viewingFriends = true; page = 0; MarkDirty(); }
            });
        if (!string.IsNullOrEmpty(room?.StatusKey)) Note(room.StatusKey);
        else if (!preview && controls?.Ready(controls.LocalId) == true) Note("coop.wait_ready");
        Button(T(LocalizationKeys.CoopLeave), 0, -306, 340, Back, backButton: true);
    }

    void DrawFriends(ICoopRooms room, ICoopRoomInvites invites)
    {
        heading.text = T(LocalizationKeys.CoopInvite);
        invites.GetOnlineFriends(friends);
        Paginate(friends.Count);
        if (friends.Count == 0) Text(contents, T("coop.no_friends"), new Vector2(730, 85), new Vector2(0, 100), 34);
        for (int i = page * RoomsPerPage; i < Mathf.Min((page + 1) * RoomsPerPage, friends.Count); i++)
        {
            var friend = friends[i];
            bool sent = invitations.Contains(friend.Id);
            Button(friend.Name + (sent ? "  · " + T("coop.invited") : ""), 0, 208 - (i % RoomsPerPage) * 75, 670, () =>
            {
                if (invites.InviteFriend(friend.Id)) invitations.Add(friend.Id);
                MarkDirty();
            }, !sent);
        }
        Button("Steam", 0, -113, 365, room.Invite);
        if (!string.IsNullOrEmpty(room.StatusKey)) Note(room.StatusKey);
        Button(T(LocalizationKeys.CoopBack), 0, -306, 340, Back, backButton: true);
    }

    void Paginate(int count)
    {
        int pages = Mathf.Max(1, (count + RoomsPerPage - 1) / RoomsPerPage);
        page = Mathf.Clamp(page, 0, pages - 1);
        if (pages == 1) return;
        Button("‹", -150, -182, 85, () => { page--; MarkDirty(); }, page > 0);
        Text(contents, (page + 1) + " / " + pages, new Vector2(170, 48), new Vector2(0, -182), 30);
        Button("›", 150, -182, 85, () => { page++; MarkDirty(); }, page < pages - 1);
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
        var input = InputField(available && !busy, true);
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

    TMP_InputField InputField(bool enabled, bool password)
    {
        var box = Rect(password ? "Password" : "Room code", contents, new Vector2(610, 66), new Vector2(0, password ? -40 : -2));
        var background = box.gameObject.AddComponent<Image>();
        background.color = new Color(.08f, .12f, .24f);
        var viewport = Rect("Viewport", box, new Vector2(574, 60), Vector2.zero);
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
        input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
        input.asteriskChar = '*';
        input.characterLimit = CoopSession.MaxPasswordLength;
        input.SetTextWithoutNotify(password ? formPassword : codeDraft);
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
