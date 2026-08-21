using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cross-checks the three places a key can live: the constants in <see cref="LocalizationKeys"/>,
/// the rows in the localization table, and the keys wired into scenes and prefabs. Catches typos and
/// keys that were referenced in UI but never translated.
/// Menu: TCG Card Caos → Localization → Validate Translations
/// </summary>
public static class LocalizationValidator
{
    [MenuItem("TCG Card Caos/Localization/Validate Translations")]
    public static void Validate()
    {
        LocalizationTable table = LocalizationEditorUtility.FindTable();
        if (table == null)
            return;

        var tableKeys = new HashSet<string>();
        var duplicateKeys = new List<string>();

        IReadOnlyList<LocalizationTable.Entry> entries = table.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            LocalizationTable.Entry entry = entries[i];
            if (entry == null || string.IsNullOrEmpty(entry.key))
                continue;

            if (!tableKeys.Add(entry.key))
                duplicateKeys.Add(entry.key);
        }

        Dictionary<string, string> declared = LocalizationEditorUtility.GetDeclaredKeys();
        Dictionary<string, List<string>> usedKeys = FindKeysUsedInAssets();

        var report = new StringBuilder();
        report.Append("[Localization] Validation report\n");
        report.Append("  Table keys: ").Append(tableKeys.Count)
              .Append(" | LocalizationKeys constants: ").Append(declared.Count)
              .Append(" | Keys wired in assets: ").Append(usedKeys.Count).Append('\n');

        bool hasProblem = false;

        hasProblem |= AppendSection(report, "Duplicate rows in table", duplicateKeys);

        var declaredButMissing = new List<string>();
        foreach (KeyValuePair<string, string> pair in declared)
        {
            if (!tableKeys.Contains(pair.Key))
                declaredButMissing.Add(pair.Key + "  (LocalizationKeys." + pair.Value + ")");
        }
        hasProblem |= AppendSection(report, "Declared in code but missing from table", declaredButMissing);

        var orphanRows = new List<string>();
        foreach (string key in tableKeys)
        {
            if (!declared.ContainsKey(key))
                orphanRows.Add(key);
        }
        hasProblem |= AppendSection(report, "In table but no LocalizationKeys constant", orphanRows);

        var brokenReferences = new List<string>();
        foreach (KeyValuePair<string, List<string>> pair in usedKeys)
        {
            if (!tableKeys.Contains(pair.Key))
                brokenReferences.Add(pair.Key + "  <- " + string.Join(", ", pair.Value));
        }
        hasProblem |= AppendSection(report, "Used in a scene/prefab but missing from table", brokenReferences);

        AppendMissingTranslations(report, entries);

        if (hasProblem)
            Debug.LogWarning(report.ToString());
        else
            Debug.Log(report.ToString());
    }

    static void AppendMissingTranslations(
        StringBuilder report, IReadOnlyList<LocalizationTable.Entry> entries)
    {
        report.Append("\n  Missing translations per language:\n");

        for (int language = 0; language < GameLanguages.Count; language++)
        {
            var missing = new List<string>();

            for (int i = 0; i < entries.Count; i++)
            {
                LocalizationTable.Entry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.key))
                    continue;

                bool filled = entry.values != null
                              && language < entry.values.Length
                              && !string.IsNullOrWhiteSpace(entry.values[language]);

                if (!filled)
                    missing.Add(entry.key);
            }

            string label = (GameLanguage)language
                           + " (" + LocalizationEditorUtility.CultureCode(language) + ")";

            report.Append("    ").Append(label.PadRight(30));

            if (missing.Count == 0)
                report.Append("complete\n");
            else
                report.Append(missing.Count).Append(" missing: ")
                      .Append(string.Join(", ", missing)).Append('\n');
        }
    }

    static bool AppendSection(StringBuilder report, string title, List<string> items)
    {
        if (items.Count == 0)
            return false;

        report.Append("\n  ").Append(title).Append(" (").Append(items.Count).Append("):\n");
        for (int i = 0; i < items.Count; i++)
            report.Append("    ").Append(items[i]).Append('\n');

        return true;
    }

    /// <summary>
    /// Maps every key referenced by a <see cref="LocalizedText"/> component to the assets using it.
    /// Scenes and prefabs are read as text so no scene has to be opened to run the check.
    /// </summary>
    static Dictionary<string, List<string>> FindKeysUsedInAssets()
    {
        var usedKeys = new Dictionary<string, List<string>>();

        string scriptGuid = FindLocalizedTextGuid();
        if (string.IsNullOrEmpty(scriptGuid))
            return usedKeys;

        string scriptMarker = "guid: " + scriptGuid;
        string[] guids = AssetDatabase.FindAssets("t:Scene t:Prefab");

        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!assetPath.StartsWith("Assets/") || !File.Exists(assetPath))
                continue;

            string text = File.ReadAllText(assetPath);
            if (!text.Contains(scriptMarker))
                continue;

            CollectKeysFromYaml(text, scriptMarker, Path.GetFileName(assetPath), usedKeys);
        }

        return usedKeys;
    }

    static void CollectKeysFromYaml(
        string text, string scriptMarker, string assetName, Dictionary<string, List<string>> usedKeys)
    {
        string[] lines = text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].IndexOf(scriptMarker, System.StringComparison.Ordinal) < 0)
                continue;

            // The serialized "key" field follows the m_Script line within the same component block.
            for (int offset = 1; offset <= 6 && i + offset < lines.Length; offset++)
            {
                string line = lines[i + offset];
                if (line.StartsWith("--- "))
                    break;

                string trimmed = line.Trim();
                if (!trimmed.StartsWith("key:"))
                    continue;

                string key = trimmed.Substring(4).Trim().Trim('\'', '"');
                if (string.IsNullOrEmpty(key))
                    break;

                if (!usedKeys.TryGetValue(key, out List<string> sources))
                {
                    sources = new List<string>();
                    usedKeys[key] = sources;
                }

                if (!sources.Contains(assetName))
                    sources.Add(assetName);

                break;
            }
        }
    }

    static string FindLocalizedTextGuid()
    {
        string[] guids = AssetDatabase.FindAssets("LocalizedText t:MonoScript");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (Path.GetFileNameWithoutExtension(path) == "LocalizedText")
                return guids[i];
        }

        Debug.LogWarning("[Localization] Could not locate the LocalizedText script to scan assets.");
        return null;
    }
}
