using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Shared lookups for the localization editor tools.</summary>
public static class LocalizationEditorUtility
{
    public const string TableAssetPath = "Assets/Resources/Localization/LocalizationTable.asset";

    public static LocalizationTable FindTable()
    {
        var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(TableAssetPath);
        if (table != null)
            return table;

        string[] guids = AssetDatabase.FindAssets("t:LocalizationTable");
        if (guids.Length == 0)
        {
            Debug.LogError(
                "[Localization] No LocalizationTable found. Run "
                + "TCG Card Caos → UI → Create Or Update Localization Table.");
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<LocalizationTable>(
            AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    /// <summary>Every string constant declared in <see cref="LocalizationKeys"/>.</summary>
    public static Dictionary<string, string> GetDeclaredKeys()
    {
        var declared = new Dictionary<string, string>();

        FieldInfo[] fields = typeof(LocalizationKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static);

        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (!field.IsLiteral || field.IsInitOnly || field.FieldType != typeof(string))
                continue;

            if (field.GetRawConstantValue() is string value && !string.IsNullOrEmpty(value))
                declared[value] = field.Name;
        }

        return declared;
    }

    public static string CultureCode(int languageIndex) =>
        GameLanguages.GetCultureCode((GameLanguage)languageIndex);
}
