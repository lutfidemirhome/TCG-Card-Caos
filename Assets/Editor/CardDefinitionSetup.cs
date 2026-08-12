using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates placeholder <see cref="CardDefinition"/> assets before card art is imported.
/// </summary>
public static class CardDefinitionSetup
{
    const string DefinitionsFolder = "Assets/Resources/Cards/Definitions";

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
        int shelfSlotNumber)
    {
        var serialized = new SerializedObject(definition);
        serialized.FindProperty("definitionId").stringValue = definitionId;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("shelfCategoryId").stringValue = shelfCategoryId;
        serialized.FindProperty("shelfSlotNumber").intValue = shelfSlotNumber;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
