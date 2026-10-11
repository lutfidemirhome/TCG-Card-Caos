using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Unlocked movement controls, above the existing skill bar. No input or raycasts.</summary>
public sealed class MinorMovementHud : MonoBehaviour
{
    GameObject _run, _jump;
    Image _runIcon, _jumpIcon;
    FirstPersonController _player;
    PlayerCardHand _playerHand;
    bool _hasUnlockedControl, _wasRunning, _wasJumping;

    public void Configure(SkillPanelView skills, Image background)
    {
        _run = CreateControl("Run", 0f, "LShift", skills.GetMinorSkillIcon(MinorSkillProgress.Run), background, out _runIcon);
        _jump = CreateControl("LongJump", 80f, "Space", skills.GetMinorSkillIcon(MinorSkillProgress.LongJump), background, out _jumpIcon);
        RefreshUnlocks();
    }

    GameObject CreateControl(string name, float x, string key, Sprite icon, Image background, out Image iconImage)
    {
        var rect = MakeRect(name, transform, new Vector2(x,104f), new Vector2(63.75f,68.85f));
        var bg = rect.gameObject.AddComponent<Image>();
        bg.sprite = background.sprite; bg.type = background.type; bg.color = Color.white; bg.raycastTarget = false;
        var art = MakeRect("Icon", rect, new Vector2(8f,10f), new Vector2(47.75f,48.85f));
        iconImage = art.gameObject.AddComponent<Image>(); iconImage.sprite = icon;
        iconImage.preserveAspect = true; iconImage.raycastTarget = false;
        var labelRect = MakeRect("Key", rect, new Vector2(-3f,-26f), new Vector2(69.75f,24f));
        var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = UiMenuFont.Font; label.fontSharedMaterial = UiMenuFont.Font.material;
        label.text = key; label.fontSize = 20f; label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white; label.raycastTarget = false;
        return rect.gameObject;
    }

    static RectTransform MakeRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }

    void OnEnable()
    {
        MinorSkillProgress.Changed += RefreshUnlocks;
        RefreshUnlocks();
    }

    void OnDisable() { MinorSkillProgress.Changed -= RefreshUnlocks; }

    void RefreshUnlocks()
    {
        if (_run == null) return;
        bool runUnlocked = MinorSkillProgress.IsUnlocked(MinorSkillProgress.Run);
        bool jumpUnlocked = MinorSkillProgress.IsUnlocked(MinorSkillProgress.LongJump);
        _hasUnlockedControl = runUnlocked || jumpUnlocked;
        SetVisible(_run, runUnlocked);
        SetVisible(_jump, jumpUnlocked);
        RefreshMovementState();
    }

    void LateUpdate()
    {
        if (_hasUnlockedControl) RefreshMovementState();
    }

    void RefreshMovementState()
    {
        // Resolve the controller once per player, including replacement after a load.
        PlayerCardHand hand = PlayerCardHand.Instance;
        if (hand != _playerHand)
        {
            _playerHand = hand;
            _player = hand != null ? hand.GetComponentInParent<FirstPersonController>() : null;
        }
        bool jumping = _player != null && _player.IsLongJumpInProgress;
        if (jumping != _wasJumping)
        {
            _wasJumping = jumping;
            _jumpIcon.color = jumping ? new Color32(105,255,90,255) : Color.white;
        }
        bool running = _player != null && _player.IsRunning;
        if (running != _wasRunning)
        {
            _wasRunning = running;
            _runIcon.color = running ? new Color32(105,255,90,255) : Color.white;
        }
    }
    static void SetVisible(GameObject target, bool visible)
    {
        if (target.activeSelf != visible) target.SetActive(visible);
    }
}
