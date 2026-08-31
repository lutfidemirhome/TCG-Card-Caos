using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Removes Mix-floor cards and pack rolls that pointed at deleted ghost definitions
/// (wrong-category series with no art). Does not respawn Mix All.
/// </summary>
public static class GhostCatalogCardCleanup
{
    static readonly string[] GhostNamePrefixes =
    {
        "Card_rock_common_stonecoil_",
        "Card_dragon_uncommon_voltaryn_",
        "Card_flying_rare_frostail_",
        "Card_ground_rare_drillmole_",
        "Card_ice_rare_shardback_",
        "Card_lightning_uncommon_voltwing_",
    };

    [MenuItem("TCG Card Chaos/Remove Ghost Catalog Cards From Scene")]
    public static void RunFromMenu()
    {
        int removed = CleanupOpenScene();
        EditorUtility.DisplayDialog(
            "Ghost catalog",
            removed + " hayalet kart / pack içeriği temizlendi.\nSahneyi kaydet.",
            "OK");
    }

    static int CleanupOpenScene()
    {
        int removed = 0;
        WorldCard[] cards = Object.FindObjectsByType<WorldCard>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < cards.Length; i++)
        {
            WorldCard card = cards[i];
            if (card == null || !ShouldRemoveCard(card))
                continue;
            if (card.GetComponentInParent<CardShelfSlot>() != null
                || card.GetComponentInParent<PsaCabinetSlot>() != null)
                continue;

            Undo.DestroyObjectImmediate(card.gameObject);
            removed++;
        }

        WorldBoosterPack[] packs = Object.FindObjectsByType<WorldBoosterPack>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        CardDefinition replacement = FirstValidDefinition();
        for (int i = 0; i < packs.Length; i++)
        {
            WorldBoosterPack pack = packs[i];
            if (pack == null)
                continue;

            IReadOnlyList<CardDefinition> contents = pack.PeekPreRolledContents();
            if (contents == null || contents.Count == 0)
                continue;

            bool dirty = false;
            var fixedContents = new List<CardDefinition>(contents.Count);
            for (int c = 0; c < contents.Count; c++)
            {
                CardDefinition definition = contents[c];
                if (definition != null && definition.FrontTexture != null)
                {
                    fixedContents.Add(definition);
                    continue;
                }

                if (replacement != null)
                    fixedContents.Add(replacement);
                dirty = true;
            }

            if (!dirty)
                continue;

            ReplacePackContents(pack, fixedContents);
            removed++;
        }

        if (removed > 0)
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        return removed;
    }

    static bool ShouldRemoveCard(WorldCard card)
    {
        string id = card.Definition != null ? card.Definition.DefinitionId : null;
        if (!string.IsNullOrEmpty(id)
            && (id.StartsWith("rock_common_stonecoil_")
                || id.StartsWith("dragon_uncommon_voltaryn_")
                || id.StartsWith("flying_rare_frostail_")
                || id.StartsWith("ground_rare_drillmole_")
                || id.StartsWith("ice_rare_shardback_")
                || id.StartsWith("lightning_uncommon_voltwing_")))
            return true;

        string name = card.gameObject.name;
        for (int i = 0; i < GhostNamePrefixes.Length; i++)
        {
            if (name.StartsWith(GhostNamePrefixes[i]))
                return true;
        }

        return false;
    }

    static void ReplacePackContents(WorldBoosterPack pack, List<CardDefinition> contents)
    {
        var so = new SerializedObject(pack);
        SerializedProperty list = so.FindProperty("preRolledContents");
        if (list == null || !list.isArray)
            return;

        Undo.RecordObject(pack, "Fix Ghost Pack Contents");
        list.arraySize = contents.Count;
        for (int i = 0; i < contents.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = contents[i];

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pack);
    }

    static CardDefinition FirstValidDefinition()
    {
        CardCatalog.Reload();
        IReadOnlyList<CardDefinition> all = CardCatalog.All;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null && all[i].FrontTexture != null)
                return all[i];
        }

        return null;
    }
}
