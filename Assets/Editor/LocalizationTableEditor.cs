using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for <see cref="LocalizationTable"/>. The default array drawer labels the 12 language
/// slots "Element 0..11", which makes filling translations guesswork, so this draws them by language
/// name and reports how complete each language is.
/// </summary>
[CustomEditor(typeof(LocalizationTable))]
public class LocalizationTableEditor : Editor
{
    const float LanguageLabelWidth = 168f;

    SerializedProperty _entries;
    readonly HashSet<string> _expandedKeys = new HashSet<string>();
    readonly int[] _filledPerLanguage = new int[GameLanguages.Count];

    string _search = string.Empty;
    string _newKey = string.Empty;
    bool _showCompleteness = true;
    bool _onlyIncomplete;

    void OnEnable()
    {
        _entries = serializedObject.FindProperty("entries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        RecountCompleteness();
        DrawHeader(out int visibleCount);
        DrawCompleteness();
        DrawEntries();
        DrawAddKey();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "Empty values fall back to English at runtime, so a missing translation shows English "
            + "instead of blank text.\n\nBulk editing: TCG Card Chaos → Localization → Export/Import "
            + "Translations CSV.",
            MessageType.Info);

        if (visibleCount == 0 && _entries.arraySize > 0)
            EditorGUILayout.HelpBox("No keys match the current filter.", MessageType.None);

        serializedObject.ApplyModifiedProperties();
    }

    void DrawHeader(out int visibleCount)
    {
        visibleCount = 0;

        EditorGUILayout.LabelField(
            _entries.arraySize + " keys · " + GameLanguages.Count + " languages",
            EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        _search = EditorGUILayout.TextField("Search key", _search);
        if (GUILayout.Button("Clear", GUILayout.Width(52f)))
        {
            _search = string.Empty;
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        _onlyIncomplete = EditorGUILayout.ToggleLeft(
            "Only keys with missing translations", _onlyIncomplete);

        if (GUILayout.Button("Expand", GUILayout.Width(64f)))
            SetAllExpanded(true);

        if (GUILayout.Button("Collapse", GUILayout.Width(70f)))
            SetAllExpanded(false);
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < _entries.arraySize; i++)
        {
            if (PassesFilter(_entries.GetArrayElementAtIndex(i)))
                visibleCount++;
        }
    }

    void DrawCompleteness()
    {
        EditorGUILayout.Space(4f);
        _showCompleteness = EditorGUILayout.Foldout(_showCompleteness, "Completeness", true);
        if (!_showCompleteness)
            return;

        int total = _entries.arraySize;
        EditorGUI.indentLevel++;

        for (int language = 0; language < GameLanguages.Count; language++)
        {
            int filled = _filledPerLanguage[language];
            float ratio = total == 0 ? 0f : (float)filled / total;

            Rect row = EditorGUILayout.GetControlRect();
            Rect labelRect = new Rect(row.x, row.y, LanguageLabelWidth, row.height);
            Rect barRect = new Rect(
                row.x + LanguageLabelWidth, row.y, row.width - LanguageLabelWidth, row.height);

            EditorGUI.LabelField(labelRect, LanguageLabel(language));
            EditorGUI.ProgressBar(barRect, ratio, filled + " / " + total);
        }

        EditorGUI.indentLevel--;
    }

    void DrawEntries()
    {
        EditorGUILayout.Space(6f);

        int removeIndex = -1;

        for (int i = 0; i < _entries.arraySize; i++)
        {
            SerializedProperty entry = _entries.GetArrayElementAtIndex(i);
            if (!PassesFilter(entry))
                continue;

            SerializedProperty keyProperty = entry.FindPropertyRelative("key");
            SerializedProperty values = EnsureValueCount(entry);

            string key = keyProperty.stringValue;
            int filled = CountFilled(values);
            bool expanded = _expandedKeys.Contains(key);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            bool nowExpanded = EditorGUILayout.Foldout(
                expanded,
                string.IsNullOrEmpty(key) ? "(no key)" : key,
                true,
                EditorStyles.foldoutHeader);

            GUILayout.Label(
                filled + "/" + GameLanguages.Count,
                filled == GameLanguages.Count ? EditorStyles.miniLabel : EditorStyles.miniBoldLabel,
                GUILayout.Width(44f));
            EditorGUILayout.EndHorizontal();

            if (nowExpanded != expanded)
            {
                if (nowExpanded)
                    _expandedKeys.Add(key);
                else
                    _expandedKeys.Remove(key);
            }

            if (nowExpanded)
            {
                EditorGUILayout.PropertyField(keyProperty, new GUIContent("Key"));
                EditorGUILayout.Space(2f);

                for (int language = 0; language < GameLanguages.Count; language++)
                {
                    SerializedProperty value = values.GetArrayElementAtIndex(language);
                    EditorGUILayout.PropertyField(value, new GUIContent(LanguageLabel(language)));
                }

                EditorGUILayout.Space(2f);
                if (GUILayout.Button("Remove This Key"))
                {
                    bool confirmed = EditorUtility.DisplayDialog(
                        "Remove key",
                        "Remove \"" + key + "\" and all " + GameLanguages.Count + " translations?",
                        "Remove", "Cancel");

                    if (confirmed)
                        removeIndex = i;
                }
            }

            EditorGUILayout.EndVertical();
        }

        if (removeIndex >= 0)
            _entries.DeleteArrayElementAtIndex(removeIndex);
    }

    void DrawAddKey()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Add Key", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        _newKey = EditorGUILayout.TextField(_newKey);

        bool valid = !string.IsNullOrWhiteSpace(_newKey) && !KeyExists(_newKey.Trim());
        using (new EditorGUI.DisabledScope(!valid))
        {
            if (GUILayout.Button("Add", GUILayout.Width(52f)))
            {
                AddKey(_newKey.Trim());
                _newKey = string.Empty;
                GUI.FocusControl(null);
            }
        }
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrWhiteSpace(_newKey) && !valid)
            EditorGUILayout.HelpBox("That key already exists.", MessageType.Warning);
        else
            EditorGUILayout.LabelField(
                "Also add a matching constant in LocalizationKeys.cs.", EditorStyles.miniLabel);
    }

    void AddKey(string key)
    {
        _entries.arraySize++;
        SerializedProperty entry = _entries.GetArrayElementAtIndex(_entries.arraySize - 1);
        entry.FindPropertyRelative("key").stringValue = key;

        // A new array element inherits the previous element's data, so clear every language slot.
        SerializedProperty values = entry.FindPropertyRelative("values");
        values.arraySize = GameLanguages.Count;
        for (int i = 0; i < values.arraySize; i++)
            values.GetArrayElementAtIndex(i).stringValue = string.Empty;

        _expandedKeys.Add(key);
    }

    bool KeyExists(string key)
    {
        for (int i = 0; i < _entries.arraySize; i++)
        {
            SerializedProperty keyProperty =
                _entries.GetArrayElementAtIndex(i).FindPropertyRelative("key");

            if (keyProperty.stringValue == key)
                return true;
        }

        return false;
    }

    bool PassesFilter(SerializedProperty entry)
    {
        string key = entry.FindPropertyRelative("key").stringValue ?? string.Empty;

        if (!string.IsNullOrEmpty(_search)
            && key.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        if (_onlyIncomplete && CountFilled(EnsureValueCount(entry)) == GameLanguages.Count)
            return false;

        return true;
    }

    void SetAllExpanded(bool expanded)
    {
        _expandedKeys.Clear();
        if (!expanded)
            return;

        for (int i = 0; i < _entries.arraySize; i++)
        {
            SerializedProperty entry = _entries.GetArrayElementAtIndex(i);
            if (PassesFilter(entry))
                _expandedKeys.Add(entry.FindPropertyRelative("key").stringValue);
        }
    }

    void RecountCompleteness()
    {
        for (int i = 0; i < _filledPerLanguage.Length; i++)
            _filledPerLanguage[i] = 0;

        for (int i = 0; i < _entries.arraySize; i++)
        {
            SerializedProperty values = EnsureValueCount(_entries.GetArrayElementAtIndex(i));

            for (int language = 0; language < GameLanguages.Count; language++)
            {
                if (!string.IsNullOrWhiteSpace(values.GetArrayElementAtIndex(language).stringValue))
                    _filledPerLanguage[language]++;
            }
        }
    }

    static SerializedProperty EnsureValueCount(SerializedProperty entry)
    {
        SerializedProperty values = entry.FindPropertyRelative("values");
        if (values.arraySize != GameLanguages.Count)
            values.arraySize = GameLanguages.Count;

        return values;
    }

    static int CountFilled(SerializedProperty values)
    {
        int filled = 0;
        for (int i = 0; i < values.arraySize; i++)
        {
            if (!string.IsNullOrWhiteSpace(values.GetArrayElementAtIndex(i).stringValue))
                filled++;
        }

        return filled;
    }

    static string LanguageLabel(int index)
    {
        var language = (GameLanguage)index;
        return language + " (" + GameLanguages.GetCultureCode(language) + ")";
    }
}
