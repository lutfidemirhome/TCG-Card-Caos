using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CoopSpeakingHud : MonoBehaviour
{
    readonly TMP_Text[] labels = new TMP_Text[CoopSession.Capacity];
    float nextRefresh;
    static Texture2D microphone;

    public static RawImage AddMicrophone(Transform parent, Vector2 position, float size)
    {
        var image = new GameObject("Microphone", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        image.rectTransform.SetParent(parent, false);
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = Vector2.one * size;
        if (!microphone) microphone = Resources.Load<Texture2D>("UI/CoopMic");
        image.texture = microphone;
        image.raycastTarget = false;
        return image;
    }

    void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .1f;
        int visible = 0;
        var rooms = CoopSession.Rooms;
        if (!GamePause.IsPaused && rooms is ICoopRoomControls roster && rooms is ICoopVoiceSettings voice)
        {
            for (int i = 0; i < roster.Players.Count && visible < labels.Length; i++)
            {
                ulong id = roster.Players[i];
                if (!voice.IsSpeaking(id)) continue;
                var label = labels[visible] ? labels[visible] : (labels[visible] = MakeLabel(visible));
                label.text = rooms.Members[i];
                label.color = CoopPlayerColors.For(id, roster.Players);
                label.gameObject.SetActive(true);
                visible++;
            }
        }
        for (int i = visible; i < labels.Length; i++)
            if (labels[i] && labels[i].gameObject.activeSelf) labels[i].gameObject.SetActive(false);
    }

    TMP_Text MakeLabel(int index)
    {
        var label = new GameObject("Speaking player", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        var rect = label.rectTransform;
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-66f, -30f - index * 48f);
        rect.sizeDelta = new Vector2(320f, 42f);
        UiMenuFont.ApplyControl(label);
        label.fontSize = 30f;
        label.alignment = TextAlignmentOptions.MidlineRight;
        label.richText = label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        AddMicrophone(rect, new Vector2(182f, 0), 30f);
        return label;
    }
}
