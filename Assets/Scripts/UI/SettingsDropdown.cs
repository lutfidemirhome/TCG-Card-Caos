using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Always expands downward from the header. Avoids Unity Dropdown flipping the list.
/// </summary>
public class SettingsDropdown : MonoBehaviour
{
    const float OptionHeight = 40f;
    const float OptionSpacing = 2f;
    const float ListPadding = 8f;
    const float MaxListHeight = 260f;

    [SerializeField] Button headerButton;
    [SerializeField] TMP_Text headerLabel;
    [SerializeField] RectTransform listRoot;
    [SerializeField] RectTransform content;

    string[] _options = Array.Empty<string>();
    int _selected;
    Action<int> _changed;
    Action<SettingsDropdown> _opened;
    bool _expandToFit;

    public bool IsOpen => listRoot != null && listRoot.gameObject.activeSelf;
    public int SelectedIndex => _selected;

    public void Bind(
        Button header,
        TMP_Text label,
        RectTransform list,
        RectTransform listContent,
        Action<SettingsDropdown> opened)
    {
        headerButton = header;
        headerLabel = label;
        listRoot = list;
        content = listContent;
        _opened = opened;

        if (headerButton != null)
        {
            headerButton.onClick.RemoveListener(Toggle);
            headerButton.onClick.AddListener(Toggle);
        }

        Close();
    }

    public void SetExpandToFit(bool expand)
    {
        _expandToFit = expand;
    }

    public void SetOptions(string[] options, int selected, Action<int> changed)
    {
        string[] next = options ?? Array.Empty<string>();
        _changed = changed;
        _selected = Mathf.Clamp(selected, 0, Mathf.Max(0, next.Length - 1));

        bool same = AreSameOptions(_options, next);
        _options = next;
        if (!same)
            RebuildOptions();

        RefreshHeader();
        Close();
    }

    /// <summary>
    /// Updates existing option text (e.g. after a language change) without destroying the list.
    /// Destroy+recreate in the same frame left stale children and placeholder rows.
    /// </summary>
    public void RefreshLabels(string[] options)
    {
        if (options == null || options.Length != _options.Length)
        {
            SetOptions(options, _selected, _changed);
            return;
        }

        _options = options;
        RefreshHeader();
        if (content == null)
            return;

        int count = Mathf.Min(content.childCount, _options.Length);
        for (int i = 0; i < count; i++)
        {
            TMP_Text label = content.GetChild(i).GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = _options[i];
        }
    }

    public void SetSelected(int index, bool notify)
    {
        if (_options.Length == 0)
            return;

        _selected = Mathf.Clamp(index, 0, _options.Length - 1);
        RefreshHeader();
        if (notify)
            _changed?.Invoke(_selected);
    }

    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
            return;
        }

        Open();
    }

    public void Open()
    {
        if (listRoot == null)
            return;

        _opened?.Invoke(this);
        PlaceListBelowHeader();
        ApplyScrollMode();
        listRoot.gameObject.SetActive(true);
        listRoot.SetAsLastSibling();
    }

    public void Close()
    {
        if (listRoot != null)
            listRoot.gameObject.SetActive(false);
    }

    void PlaceListBelowHeader()
    {
        if (headerButton == null || listRoot == null)
            return;

        RectTransform header = headerButton.transform as RectTransform;
        RectTransform parent = listRoot.parent as RectTransform;
        if (header == null || parent == null)
            return;

        Canvas.ForceUpdateCanvases();

        Vector3[] corners = new Vector3[4];
        header.GetWorldCorners(corners);
        Vector3 bottomCenter = (corners[0] + corners[3]) * 0.5f;
        Vector3 local = parent.InverseTransformPoint(bottomCenter);

        float width = header.rect.width;
        int count = Mathf.Max(1, _options.Length);
        float height = ListPadding + OptionHeight * count + OptionSpacing * Mathf.Max(0, count - 1);
        if (!_expandToFit)
            height = Mathf.Min(MaxListHeight, height);

        listRoot.localScale = Vector3.one;
        listRoot.localRotation = Quaternion.identity;
        listRoot.pivot = new Vector2(0.5f, 1f);
        listRoot.anchorMin = new Vector2(0.5f, 0.5f);
        listRoot.anchorMax = new Vector2(0.5f, 0.5f);
        listRoot.sizeDelta = new Vector2(width, height);
        listRoot.anchoredPosition = new Vector2(local.x, local.y - 2f);
    }

    void ApplyScrollMode()
    {
        if (listRoot == null)
            return;

        ScrollRect scroll = listRoot.GetComponent<ScrollRect>();
        if (scroll == null)
            return;

        scroll.vertical = !_expandToFit;
        if (_expandToFit)
            scroll.verticalNormalizedPosition = 1f;
    }

    void RebuildOptions()
    {
        if (content == null)
            return;

        for (int i = content.childCount - 1; i >= 0; i--)
            DestroyImmediate(content.GetChild(i).gameObject);

        for (int i = 0; i < _options.Length; i++)
        {
            int index = i;
            var optionGo = new GameObject("Option_" + i, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            optionGo.transform.SetParent(content, false);
            optionGo.GetComponent<LayoutElement>().preferredHeight = OptionHeight;
            optionGo.GetComponent<LayoutElement>().minHeight = OptionHeight;

            var image = optionGo.GetComponent<Image>();
            image.color = new Color(0.78f, 0.8f, 0.84f, 1f);

            var button = optionGo.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                SetSelected(index, notify: true);
                Close();
            });

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(optionGo.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 2f);
            textRect.offsetMax = new Vector2(-12f, -2f);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = _options[i];
            tmp.raycastTarget = false;
            if (headerLabel != null)
            {
                tmp.font = headerLabel.font;
                tmp.fontSharedMaterial = headerLabel.fontSharedMaterial;
                tmp.fontSize = headerLabel.fontSize;
                tmp.color = headerLabel.color;
                tmp.alignment = headerLabel.alignment;
            }
            else
            {
                tmp.fontSize = 26f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.black;
            }
        }
    }

    void RefreshHeader()
    {
        if (headerLabel == null)
            return;

        headerLabel.text = _options.Length == 0 ? string.Empty : _options[_selected];
    }

    static bool AreSameOptions(string[] current, string[] next)
    {
        if (current == next)
            return true;
        if (current == null || next == null || current.Length != next.Length)
            return false;

        for (int i = 0; i < current.Length; i++)
        {
            if (current[i] != next[i])
                return false;
        }

        return true;
    }
}
