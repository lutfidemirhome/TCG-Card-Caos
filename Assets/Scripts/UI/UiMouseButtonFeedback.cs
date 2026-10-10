using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Discovers mouse-clicked buttons on demand, including dynamically created menus.</summary>
[DefaultExecutionOrder(-10000)]
public sealed class UiMouseButtonFeedback : MonoBehaviour
{
    readonly List<RaycastResult> _hits = new List<RaycastResult>(32);
    PointerEventData _pointer;
    EventSystem _eventSystem;
    SkillPanelButtonFeedback _pressed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        var obj = new GameObject("UI Mouse Button Feedback");
        DontDestroyOnLoad(obj);
        obj.AddComponent<UiMouseButtonFeedback>();
    }

    void Update()
    {
        if (_pressed != null && !Input.GetMouseButton(0)) Release();
        if (!Input.GetMouseButtonDown(0) || Cursor.lockState == CursorLockMode.Locked) return;
        EventSystem current = EventSystem.current;
        if (current == null) return;
        if (_eventSystem != current || _pointer == null)
        {
            _eventSystem = current;
            _pointer = new PointerEventData(current);
        }
        _pointer.Reset();
        _pointer.button = PointerEventData.InputButton.Left;
        _pointer.position = Input.mousePosition;
        _hits.Clear();
        current.RaycastAll(_pointer, _hits);
        // Only the foremost hit receives input; never animate buttons behind a modal.
        if (_hits.Count > 0)
        {
            var control = _hits[0].gameObject.GetComponentInParent<Selectable>();
            if (control != null && control.IsActive() && control.IsInteractable()
                && (control is Button || control is Toggle || control is Dropdown || control is TMP_Dropdown)
                && !IsSkillHotbar(control.transform))
            {
                _pressed = SkillPanelButtonFeedback.Ensure(control);
                _pressed.OnPointerDown(_pointer);
            }
        }
        _hits.Clear();
    }

    static bool IsSkillHotbar(Transform target)
    {
        for (var parent = target; parent != null; parent = parent.parent)
            if (parent.name == "Hotbar" && parent.GetComponentInParent<SkillPanelView>() != null)
                return true;
        return false;
    }

    void Release()
    {
        if (_pressed != null && _pointer != null) _pressed.OnPointerUp(_pointer);
        _pressed = null;
    }

    void OnApplicationFocus(bool focused) { if (!focused) Release(); }
    void OnDisable() => Release();
}
