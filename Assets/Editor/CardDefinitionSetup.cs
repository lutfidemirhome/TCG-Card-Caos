using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates placeholder <see cref="CardDefinition"/> assets before card art is imported.
/// </summary>
public static class CardDefinitionSetup
{
    const string DefinitionsFolder = "Assets/Resources/Cards/Definitions";
    const string ArtRootFolder = "Assets/Art/Cards/Normal_Common_Cards";
    static readonly Regex FileNamePattern = new Regex(
        "^normal_common_(?<character>[a-z]+)_(?<slot>\\d+)\\.png$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [MenuItem("TCG Card Caos/Import Normal Common Cards From Art")]
    public static void ImportNormalCommonCardsFromArt()
    {
        if (!AssetDatabase.IsValidFolder(ArtRootFolder))
        {
            Debug.LogError("TCG Card Caos: Missing art folder " + ArtRootFolder);
            return;
        }

        Directory.CreateDirectory(DefinitionsFolder);
        string[] pngGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRootFolder });
        int created = 0;
        int updated = 0;

        for (int i = 0; i < pngGuids.Length; i++)
        {
            string pngPath = AssetDatabase.GUIDToAssetPath(pngGuids[i]);
            if (string.IsNullOrEmpty(pngPath) || !pngPath.EndsWith(".png"))
                continue;

            string fileName = Path.GetFileName(pngPath);
            Match match = FileNamePattern.Match(fileName);
            if (!match.Success)
                continue;

            string character = match.Groups["character"].Value.ToLowerInvariant();
            int slot = int.Parse(match.Groups["slot"].Value);
            string definitionId = "normal_common_" + character + "_" + slot.ToString("00");
            string assetPath = DefinitionsFolder + "/" + definitionId + ".asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);

            CardDefinition definition = AssetDatabase.LoadAssetAtPath<CardDefinition>(assetPath);
            bool isNew = definition == null;
            if (isNew)
            {
                definition = ScriptableObject.CreateInstance<CardDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
                created++;
            }
            else
            {
                updated++;
            }

            ApplyDefinitionFields(
                definition,
                definitionId,
                BuildDisplayName(character, slot),
                CardShelfCategories.NormalCommon,
                slot,
                texture);
            EditorUtility.SetDirty(definition);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        CardCatalog.Reload();
        Debug.Log(
            "TCG Card Caos: Imported Normal Common cards into "
            + DefinitionsFolder
            + " (created "
            + created
            + ", updated "
            + updated
            + ").");
    }

    [MenuItem("TCG Card Caos/Create Normal Common Card Definitions (Slots 1-10)")]
    public static void CreateNormalCommonDefinitions()
    {
        Directory.CreateDirectory(DefinitionsFolder);

        int created = 0;
        for (int slot = 1; slot <= CardShelfCategories.MaxSlotNumber; slot++)
        {
            string id = CardShelfCategories.NormalCommon + "_" + slot.ToString("00");
            string assetPath = DefinitionsFolder + "/" + id + ".asset";
            if (AssetDatabase.LoadAssetAtPath<CardDefinition>(assetPath) != null)
                continue;

            var definition = ScriptableObject.CreateInstance<CardDefinition>();
            ApplyDefinitionFields(definition, id, "Normal Common " + slot, CardShelfCategories.NormalCommon, slot);
            AssetDatabase.CreateAsset(definition, assetPath);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "TCG Card Caos: Card definitions ready in "
            + DefinitionsFolder
            + " (created "
            + created
            + ", total slots 1-10). Assign front textures when art is ready.");
    }

    [MenuItem("TCG Card Caos/Create Card Definition")]
    public static void CreateSingleDefinition()
    {
        Directory.CreateDirectory(DefinitionsFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath(DefinitionsFolder + "/CardDefinition.asset");

        var definition = ScriptableObject.CreateInstance<CardDefinition>();
        ApplyDefinitionFields(
            definition,
            "normal_common_01",
            "Normal Common 1",
            CardShelfCategories.NormalCommon,
            1);

        AssetDatabase.CreateAsset(definition, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = definition;
        EditorGUIUtility.PingObject(definition);
    }

    static void ApplyDefinitionFields(
        CardDefinition definition,
        string definitionId,
        string displayName,
        string shelfCategoryId,
        int shelfSlotNumber,
        Texture2D frontTexture = null)
    {
        var serialized = new SerializedObject(definition);
        serialized.FindProperty("definitionId").stringValue = definitionId;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("shelfCategoryId").stringValue = shelfCategoryId;
        serialized.FindProperty("shelfSlotNumber").intValue = shelfSlotNumber;
        serialized.FindProperty("frontTexture").objectReferenceValue = frontTexture;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static string BuildDisplayName(string character, int slot)
    {
        if (string.IsNullOrEmpty(character))
            return "Card " + slot;

        return char.ToUpperInvariant(character[0]) + character.Substring(1) + " " + slot;
    }
}
