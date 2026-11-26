using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CoopTeamPanel : MonoBehaviour
{
    RectTransform panel;
    bool dirty;
    public static CoopTeamPanel Create(Transform parent)
    {
        var root = new GameObject("Co-op team", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(.035f,.055f,.09f,.97f);
        return root.AddComponent<CoopTeamPanel>();
    }
    void OnEnable() { dirty = true; CoopSession.Changed += Refresh; Localization.LanguageChanged += Refresh; }
    void OnDisable() { CoopSession.Changed -= Refresh; Localization.LanguageChanged -= Refresh; }
    void Refresh() => dirty = true;
    void Update()
    {
        if (!dirty || !(CoopSession.Rooms is ICoopRoomControls room)) return;
        dirty = false;
        if (panel) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
        panel = Rect("Team", transform, 0, 0, 880, 650);
        Label(panel, Localization.Get("coop.team"), Color.white, 0, 240, 860, 52);
        for (int i = 0; i < room.Players.Count; i++)
        {
            ulong id = room.Players[i];
            float y = 145 - 64*i;
            Label(panel, CoopSession.Rooms.Members[i], CoopPlayerColors.For(id, room.Players), -170, y, 410, 34);
            if (id != room.LocalId && CoopSession.Rooms is ICoopVoiceSettings voice)
                Button(voice.IsMuted(id) ? "coop.unmute" : "coop.mute", 73, y, 177, () => voice.ToggleMute(id));
            if (id != room.LocalId && room.LocalId == room.HostId)
                Button("coop.remove", 290, y, 180, () => room.RemovePlayer(id));
        }
        if (CoopSession.Rooms is ICoopVoiceSettings settings)
            Button(settings.VoiceEnabled ? "coop.voice_on" : "coop.voice_off", 0, -76, 720, () => settings.SetVoiceEnabled(!settings.VoiceEnabled));
        if (room.LocalId == room.HostId)
            Button(room.AllowJoin ? "coop.close_entries" : "coop.open_entries", 0, -145, 485, room.ToggleJoin);
        if (room.AllowJoin && CoopSession.Rooms is ICoopRoomInvites invites && !string.IsNullOrEmpty(invites.RoomCode))
        {
            Button("coop.copy", -185, -215, 345, () => GUIUtility.systemCopyBuffer = invites.RoomCode);
            Button(LocalizationKeys.CoopInvite, 185, -215, 345, CoopSession.Rooms.Invite);
        }
        Button(LocalizationKeys.CoopBack, 0, -293, 255, () => gameObject.SetActive(false));
    }

    static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var t = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        t.SetParent(parent, false);
        t.sizeDelta = new Vector2(w,h); t.anchoredPosition = new Vector2(x,y);
        return t;
    }

    static void Label(Transform parent, string value, Color color, float x, float y, float w, float size)
    {
        var text = Rect("Label", parent, x, y, w, size + 16).gameObject.AddComponent<TextMeshProUGUI>();
        UiMenuFont.ApplyControl(text);
        text.richText = false; text.raycastTarget = false;
        text.fontSizeMax = size; text.fontSizeMin = size * .6f; text.enableAutoSizing = true;
        text.alignment = TextAlignmentOptions.Center; text.overflowMode = TextOverflowModes.Ellipsis;
        text.color = color; text.text = value;
    }

    void Button(string key, float x, float y, float w, Action action)
    {
        var rect = Rect(key, panel, x, y, w, 54);
        var image = rect.gameObject.AddComponent<Image>();
        SettingsArt.Apply(image, SettingsArt.ButtonSave, new Color(.22f,.52f,.73f));
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => { action(); Refresh(); });
        Label(rect, Localization.Get(key), Color.white, 0, 0, w - 16, 28);
    }
}
