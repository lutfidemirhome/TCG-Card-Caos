using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Mouse press feedback shared by UI buttons; skill selection is optional. No idle Update.</summary>
[DisallowMultipleComponent]
public sealed class SkillPanelButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    const float SelectedScale = 1.06f;
    const float PressScale = .95f;
    const float Duration = .12f;
    Selectable _button;
    Vector3 _baseScale;
    Coroutine _animation;
    bool _selected, _pressed;

    public static SkillPanelButtonFeedback Ensure(Selectable button)
    {
        var feedback = button.GetComponent<SkillPanelButtonFeedback>();
        return feedback != null ? feedback : button.gameObject.AddComponent<SkillPanelButtonFeedback>();
    }

    void Awake()
    {
        _button = GetComponent<Selectable>();
        _baseScale = transform.localScale;
    }

    void OnEnable() => transform.localScale = TargetScale();

    void OnDisable()
    {
        if (_animation != null) StopCoroutine(_animation);
        _animation = null;
        _pressed = false;
        transform.localScale = _baseScale;
    }

    public void SetSelected(bool selected)
    {
        if (_selected == selected) return;
        _selected = selected;
        Animate();
    }

    public void OnPointerDown(PointerEventData data)
    {
        if (_pressed || data.button != PointerEventData.InputButton.Left || _button == null || !_button.IsInteractable()) return;
        _pressed = true;
        Animate();
    }

    public void OnPointerUp(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left) ReleasePress();
    }

    public void OnPointerExit(PointerEventData data) => ReleasePress();

    void ReleasePress()
    {
        if (!_pressed) return;
        _pressed = false;
        Animate();
    }

    Vector3 TargetScale() => _baseScale * (_selected ? SelectedScale : 1f) * (_pressed ? PressScale : 1f);

    void Animate()
    {
        if (_animation != null) StopCoroutine(_animation);
        _animation = null;
        if (isActiveAndEnabled) _animation = StartCoroutine(ScaleRoutine());
    }

    IEnumerator ScaleRoutine()
    {
        Vector3 start = transform.localScale;
        Vector3 target = TargetScale();
        float elapsed = 0f;
        while (elapsed < Duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            t = 1f - (1f - t) * (1f - t) * (1f - t);
            transform.localScale = Vector3.LerpUnclamped(start, target, t);
            yield return null;
        }
        transform.localScale = target;
        _animation = null;
    }
}
