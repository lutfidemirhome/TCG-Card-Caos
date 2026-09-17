using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Checks authored content only. Saved games keep their original snapshots.</summary>
public static class PackAssignmentSceneValidation
{
    public static List<string> FindProblems(PhysicsLevelLayout layout)
    {
        var errors = new List<string>();
        if (layout == null || !layout.gameObject.activeInHierarchy)
        {
            errors.Add("Paket atamalarının bulunduğu PhysicsLevelLayout sahnede aktif olmalı.");
            return errors;
        }

        CardCatalog.Reload();
        var catalogIds = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
        foreach (CardDefinition definition in CardCatalog.All)
        {
            if (definition == null)
                continue;
            if (string.IsNullOrWhiteSpace(definition.DefinitionId))
                errors.Add(definition.name + ": katalog kartının kalıcı tanımı eksik.");
            else if (catalogIds.TryGetValue(definition.DefinitionId, out CardDefinition previous))
                errors.Add(definition.DefinitionId + ": katalogda birden fazla kart asset'i aynı tanımı kullanıyor ("
                    + AssetDatabase.GetAssetPath(previous) + " / " + AssetDatabase.GetAssetPath(definition) + ").");
            else
                catalogIds.Add(definition.DefinitionId, definition);
        }

        var assigned = new Dictionary<string, string>(StringComparer.Ordinal);
        var persistentIds = new Dictionary<string, string>(StringComparer.Ordinal);
        WorldBoosterPack[] packs = layout.GetComponentsInChildren<WorldBoosterPack>(true);
        int english = 0, japanese = 0;
        foreach (WorldBoosterPack pack in packs)
        {
            if (pack.PackSet == PackCardSet.English) english++;
            else if (pack.PackSet == PackCardSet.Japanese) japanese++;
            string label = string.IsNullOrEmpty(pack.AssignmentLabel) ? pack.name : pack.AssignmentLabel;
            if (!pack.gameObject.activeInHierarchy)
                errors.Add(label + ": paket sahnede kapalı; içindeki kartlara oyunda ulaşılamaz.");
            CheckPersistentId(pack, label, persistentIds, errors, required: true);
            if (!pack.TryGetFixedContents(out var contents, out string error))
            {
                errors.Add(label + ": " + error);
                continue;
            }
            foreach (CardDefinition card in contents)
            {
                if (!IsCatalogDefinition(card))
                    errors.Add(label + ": " + card.DefinitionId + " kayıt sisteminin kataloğunda tekil bir kart olarak bulunamadı.");
                if (string.IsNullOrWhiteSpace(card.DefinitionId))
                    continue;
                if (assigned.TryGetValue(card.DefinitionId, out string previous))
                    errors.Add(label + ": " + card.DefinitionId + " ayrıca " + previous + " içinde.");
                else
                    assigned.Add(card.DefinitionId, label);
            }
        }
        if (english != PhysicsLevelLayout.MixPackCount || japanese != PhysicsLevelLayout.JapanPackCount)
            errors.Add($"Paket sayısı EN {english}/100, JP {japanese}/10 olmalı.");
        var sceneCards = new HashSet<string>(StringComparer.Ordinal);
        foreach (GameObject root in layout.gameObject.scene.GetRootGameObjects())
            foreach (WorldCard card in root.GetComponentsInChildren<WorldCard>())
            {
                if (!card.gameObject.activeInHierarchy)
                    continue;
                CheckPersistentId(card, card.name, persistentIds, errors, required: false);
                if (card.UsesPsaSlab)
                    continue;
                if (!IsCatalogDefinition(card.Definition))
                    errors.Add(card.name + ": sahnedeki kart kayıt sisteminin kataloğunda tekil bir kart olarak bulunamadı.");
                if (card.Definition == null || string.IsNullOrWhiteSpace(card.Definition.DefinitionId))
                    continue;
                if (!sceneCards.Add(card.Definition.DefinitionId))
                    errors.Add(card.Definition.DefinitionId + ": sahnede birden fazla örneği var.");
                if (assigned.TryGetValue(card.Definition.DefinitionId, out string pack))
                    errors.Add(card.Definition.DefinitionId + ": sahnede bir kartı var ve " + pack + " içinde de atanmış.");
            }
        foreach (CardDefinition definition in CardCatalog.All)
            if (definition != null && definition.FrontTexture != null
                && !string.IsNullOrWhiteSpace(definition.DefinitionId)
                && !sceneCards.Contains(definition.DefinitionId) && !assigned.ContainsKey(definition.DefinitionId))
                errors.Add(definition.DefinitionId + ": ne sahnede ne de bir pakette bulunuyor.");
        return errors;
    }

    static bool IsCatalogDefinition(CardDefinition card) => card != null
        && !string.IsNullOrWhiteSpace(card.DefinitionId)
        && AssetDatabase.GetAssetPath(card).StartsWith("Assets/Resources/Cards/Definitions/", StringComparison.Ordinal)
        && CardCatalog.TryGetById(card.DefinitionId, out CardDefinition savedDefinition) && savedDefinition == card;

    static void CheckPersistentId(
        Component owner, string label, Dictionary<string, string> owners, List<string> errors, bool required)
    {
        // Validation must not create components or assign identities. A copied
        // scene object must be corrected deliberately before authoring is saved.
        PersistentId persistent = owner.GetComponent<PersistentId>();
        string id = persistent != null ? persistent.Value : null;
        if (string.IsNullOrWhiteSpace(id))
        {
            if (required)
                errors.Add(label + ": paketin kalıcı sahne kimliği (PersistentId) eksik.");
            return;
        }
        if (owners.TryGetValue(id, out string previous))
            errors.Add(label + ": kalıcı sahne kimliği " + previous + " ile aynı; save/load için tekil olmalı.");
        else
            owners.Add(id, label);
    }
}

public sealed class PackAssignmentBuildValidation : IProcessSceneWithReport
{
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        // This authoring tool owns the full store's 110 packs and whole catalog.
        // Demo layouts intentionally contain only a subset of that inventory.
        if (report == null || GameBuildVariant.IsDemo) return;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (PhysicsLevelLayout layout in root.GetComponentsInChildren<PhysicsLevelLayout>(true))
            {
                List<string> errors = PackAssignmentSceneValidation.FindProblems(layout);
                if (errors.Count == 0) continue;
                throw new BuildFailedException("Paket atamalarında " + errors.Count
                    + " sorun var. TCG Card Chaos > Pack Kart Atamalari penceresinde düzelt ve uygula.\n"
                    + string.Join("\n", errors.GetRange(0, Mathf.Min(errors.Count, 12))));
            }
    }
}
