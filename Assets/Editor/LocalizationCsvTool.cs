using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Round-trips the localization table through CSV so translations can be handed to translators or
/// edited in a spreadsheet. Columns are matched by the culture code in the header row, so reordering
/// or omitting language columns is safe.
/// Menu: TCG Card Caos → Localization → Export / Import Translations CSV
/// </summary>
public static class LocalizationCsvTool
{
    [MenuItem("TCG Card Caos/Localization/Export Translations CSV")]
    public static void ExportCsv()
    {
        LocalizationTable table = LocalizationEditorUtility.FindTable();
        if (table == null)
            return;

        string path = EditorUtility.SaveFilePanel(
            "Export translations", "", "TCGCardCaos-Translations.csv", "csv");

        if (string.IsNullOrEmpty(path))
            return;

        var csv = new StringBuilder();

        csv.Append("key");
        for (int language = 0; language < GameLanguages.Count; language++)
            csv.Append(',').Append(LocalizationEditorUtility.CultureCode(language));
        csv.Append('\n');

        IReadOnlyList<LocalizationTable.Entry> entries = table.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            LocalizationTable.Entry entry = entries[i];
            if (entry == null || string.IsNullOrEmpty(entry.key))
                continue;

            csv.Append(Escape(entry.key));

            for (int language = 0; language < GameLanguages.Count; language++)
            {
                string value = entry.values != null && language < entry.values.Length
                    ? entry.values[language]
                    : string.Empty;

                csv.Append(',').Append(Escape(value));
            }

            csv.Append('\n');
        }

        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));

        Debug.Log(
            "[Localization] Exported " + entries.Count + " keys to " + path
            + "\nOpen it in Sheets/Excel, fill the language columns, then run Import Translations CSV.");
    }

    [MenuItem("TCG Card Caos/Localization/Import Translations CSV")]
    public static void ImportCsv()
    {
        LocalizationTable table = LocalizationEditorUtility.FindTable();
        if (table == null)
            return;

        string path = EditorUtility.OpenFilePanel("Import translations", "", "csv");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        List<List<string>> rows = ParseCsv(File.ReadAllText(path));
        if (rows.Count < 2)
        {
            Debug.LogError("[Localization] CSV has no data rows.");
            return;
        }

        List<string> header = rows[0];
        if (header.Count == 0 || header[0].Trim().ToLowerInvariant() != "key")
        {
            Debug.LogError("[Localization] First CSV column must be named \"key\".");
            return;
        }

        int[] columnToLanguage = MapHeaderColumns(header, out List<string> unknownColumns);

        var serialized = new SerializedObject(table);
        SerializedProperty entries = serialized.FindProperty("entries");
        Dictionary<string, int> keyToIndex = BuildKeyIndex(entries);

        int addedKeys = 0;
        int updatedValues = 0;

        for (int row = 1; row < rows.Count; row++)
        {
            List<string> cells = rows[row];
            if (cells.Count == 0)
                continue;

            string key = cells[0].Trim();
            if (string.IsNullOrEmpty(key))
                continue;

            if (!keyToIndex.TryGetValue(key, out int index))
            {
                index = CreateEntry(entries, key);
                keyToIndex[key] = index;
                addedKeys++;
            }

            SerializedProperty values = entries.GetArrayElementAtIndex(index)
                .FindPropertyRelative("values");

            if (values.arraySize != GameLanguages.Count)
                values.arraySize = GameLanguages.Count;

            for (int column = 1; column < cells.Count && column < columnToLanguage.Length; column++)
            {
                int language = columnToLanguage[column];
                if (language < 0)
                    continue;

                // Blank cells keep the existing translation so a partial CSV cannot wipe data.
                string incoming = cells[column];
                if (string.IsNullOrWhiteSpace(incoming))
                    continue;

                SerializedProperty value = values.GetArrayElementAtIndex(language);
                if (value.stringValue == incoming)
                    continue;

                value.stringValue = incoming;
                updatedValues++;
            }
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();

        var message = new StringBuilder();
        message.Append("[Localization] Imported ").Append(path).Append('\n');
        message.Append("  Values updated: ").Append(updatedValues).Append('\n');
        message.Append("  New keys added: ").Append(addedKeys);

        if (unknownColumns.Count > 0)
            message.Append('\n').Append("  Ignored columns: ").Append(string.Join(", ", unknownColumns));

        if (addedKeys > 0)
            message.Append('\n').Append("  Remember to declare new keys in LocalizationKeys.cs.");

        Debug.Log(message.ToString());
    }

    static int[] MapHeaderColumns(List<string> header, out List<string> unknownColumns)
    {
        var columnToLanguage = new int[header.Count];
        unknownColumns = new List<string>();

        var codeToLanguage = new Dictionary<string, int>();
        for (int language = 0; language < GameLanguages.Count; language++)
        {
            codeToLanguage[LocalizationEditorUtility.CultureCode(language).ToLowerInvariant()] = language;
            codeToLanguage[((GameLanguage)language).ToString().ToLowerInvariant()] = language;
        }

        columnToLanguage[0] = -1;

        for (int column = 1; column < header.Count; column++)
        {
            string name = header[column].Trim().ToLowerInvariant();

            if (codeToLanguage.TryGetValue(name, out int language))
            {
                columnToLanguage[column] = language;
            }
            else
            {
                columnToLanguage[column] = -1;
                if (!string.IsNullOrEmpty(name))
                    unknownColumns.Add(header[column].Trim());
            }
        }

        return columnToLanguage;
    }

    static Dictionary<string, int> BuildKeyIndex(SerializedProperty entries)
    {
        var keyToIndex = new Dictionary<string, int>(entries.arraySize);

        for (int i = 0; i < entries.arraySize; i++)
        {
            string key = entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue;
            if (!string.IsNullOrEmpty(key))
                keyToIndex[key] = i;
        }

        return keyToIndex;
    }

    static int CreateEntry(SerializedProperty entries, string key)
    {
        entries.arraySize++;
        int index = entries.arraySize - 1;

        SerializedProperty entry = entries.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("key").stringValue = key;

        SerializedProperty values = entry.FindPropertyRelative("values");
        values.arraySize = GameLanguages.Count;
        for (int i = 0; i < values.arraySize; i++)
            values.GetArrayElementAtIndex(i).stringValue = string.Empty;

        return index;
    }

    static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        bool needsQuotes = value.IndexOf(',') >= 0
                           || value.IndexOf('"') >= 0
                           || value.IndexOf('\n') >= 0
                           || value.IndexOf('\r') >= 0;

        if (!needsQuotes)
            return value;

        return '"' + value.Replace("\"", "\"\"") + '"';
    }

    /// <summary>Minimal RFC 4180 reader: handles quoted fields containing commas and newlines.</summary>
    static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();

        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;

                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;

                case '\r':
                    break;

                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    break;

                default:
                    field.Append(c);
                    break;
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        if (rows.Count > 0)
        {
            // Strip the UTF-8 BOM so the first header cell compares as "key".
            List<string> first = rows[0];
            if (first.Count > 0 && first[0].Length > 0 && first[0][0] == '\uFEFF')
                first[0] = first[0].Substring(1);
        }

        return rows;
    }
}
