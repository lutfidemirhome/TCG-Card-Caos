using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Checks real skill candidates and cabinet rules in a disposable preview scene.</summary>
public static class SkillCardGroupingValidation
{
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [MenuItem("TCG Card Chaos/Skills/Validate Mixed Language TCG Groups")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run outside Play Mode.");
        var report = new StringBuilder();
        int checks = 0;
        Scene scene = EditorSceneManager.NewPreviewScene();
        var definitions = new List<CardDefinition>();
        var tracked = new List<WorldCard>();
        ulong revision = CardGroundStack.MembershipRevision;
        try
        {
            void Check(bool pass, string message)
            {
                if (!pass) throw new InvalidOperationException(message);
                checks++;
                report.AppendLine("PASS " + message);
            }
            GameObject Create(string name)
            {
                var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
                go.SetActive(false);
                SceneManager.MoveGameObjectToScene(go, scene);
                return go;
            }
            WorldCard Card(int grade, PsaCardSet language, int variant = 1, string series = null)
            {
                var go = Create("Isolated skill card");
                var card = go.AddComponent<WorldCard>();
                card.enabled = false;
                Set(card, "psaSlotNumber", grade);
                Set(card, "psaCardSet", language);
                Set(card, "psaVariantIndex", variant);
                if (series != null)
                {
                    var definition = ScriptableObject.CreateInstance<CardDefinition>();
                    definitions.Add(definition);
                    Set(definition, "definitionId", series);
                    Set(card, "definition", definition);
                }
                go.SetActive(true);
                return card;
            }
            PsaCabinetSlot Slot(Transform parent, int grade)
            {
                var go = Create("Isolated grade slot");
                go.transform.SetParent(parent, false);
                var slot = go.AddComponent<PsaCabinetSlot>();
                slot.enabled = false;
                Set(slot, "showSlotLabel", false);
                Set(slot, "slotNumber", grade);
                go.SetActive(true);
                return slot;
            }
            var controllerObject = Create("Isolated skills");
            var controller = controllerObject.AddComponent<CardSkillController>();
            controller.enabled = false;
            Type groupType = typeof(CardSkillController).GetNestedType("CardGroup", Flags);
            object Group(WorldCard card)
            {
                object[] args = { card, null };
                Check((bool)groupType.GetMethod("TryFrom", Flags).Invoke(null, args), "Capture selected card group");
                return args[1];
            }
            bool Matches(object group, WorldCard card) => (bool)groupType.GetMethod("Matches", Flags).Invoke(group, new object[] { card });
            var normalA = Card(0, PsaCardSet.English, series: "normal_common_a_1");
            var normalA2 = Card(0, PsaCardSet.English, series: "normal_common_a_2");
            var normalB = Card(0, PsaCardSet.English, series: "normal_common_b_1");
            foreach (int grade in new[] { 7, 8, 9, 10 })
            foreach (PsaCardSet language in new[] { PsaCardSet.English, PsaCardSet.Japanese })
            {
                object group = Group(Card(grade, language));
                Check(Matches(group, Card(grade, PsaCardSet.English, 2)), "Grade " + grade + " accepts English from " + language);
                Check(Matches(group, Card(grade, PsaCardSet.Japanese, 3)), "Grade " + grade + " accepts Japanese from " + language);
                Check(!Matches(group, Card(grade == 10 ? 7 : grade + 1, language)), "Other grades remain excluded");
                Check(!Matches(group, normalA), "Normal cards remain excluded from TCG groups");
            }
            object normalGroup = Group(normalA);
            Check(Matches(normalGroup, normalA2) && !Matches(normalGroup, normalB), "Normal series isolation remains unchanged");
            WorldCard english = Card(8, PsaCardSet.English), japanese = Card(8, PsaCardSet.Japanese);
            Check(!Matches(normalGroup, english), "TCG remains excluded from normal series");

            var cabinetObject = Create("Isolated cabinet");
            var cabinet = cabinetObject.AddComponent<PsaCabinet>();
            cabinet.enabled = false;
            // Reset authors default holders when this component is added in the Editor.
            // Replace them with this test's two grade-8 holders in the preview scene only.
            while (cabinet.transform.childCount > 0)
                Object.DestroyImmediate(cabinet.transform.GetChild(0).gameObject);
            cabinetObject.SetActive(true);
            var occupied = Slot(cabinet.transform, 8);
            var empty = Slot(cabinet.transform, 8);
            Set(occupied, "occupiedCard", english);
            Check(cabinet.TryFindSkillSlot(japanese, out var target) && target == empty,
                "Japanese card uses remaining slot beside English card of same grade and variant");
            Check(!cabinet.TryFindSkillSlot(Card(8, PsaCardSet.English), out _), "Duplicate identity is still rejected");
            Check(!cabinet.TryFindSkillSlot(Card(9, PsaCardSet.Japanese), out _), "Other grades cannot use grade 8 cabinet");
            Set(occupied, "occupiedCard", null);

            var wrongGrade = Card(9, PsaCardSet.English);
            var held = Card(8, PsaCardSet.Japanese, 2);
            var flying = Card(8, PsaCardSet.Japanese, 3);
            var reveal = Card(8, PsaCardSet.English, 2);
            var locked = Card(8, PsaCardSet.English, 3);
            var shelved = Card(8, PsaCardSet.Japanese, 4);
            var inactive = Card(8, PsaCardSet.English, 4);
            Set(held, "_handState", Enum.Parse(typeof(WorldCard).GetNestedType("HandState", Flags), "Held"));
            Set(flying, "_handState", Enum.Parse(typeof(WorldCard).GetNestedType("HandState", Flags), "FlyingToShelf"));
            Set(reveal, "_handState", Enum.Parse(typeof(WorldCard).GetNestedType("HandState", Flags), "PackReveal"));
            Set(locked, "_skillCompletionLocked", true);
            shelved.transform.SetParent(empty.transform, false);
            inactive.gameObject.SetActive(false);
            tracked.AddRange(new[] { english, japanese, wrongGrade, normalA, held, flying, reveal, locked, shelved, inactive });
            foreach (WorldCard card in tracked) CardGroundStack.Track(card);
            MethodInfo find = typeof(CardSkillController).GetMethod("FindMatching", Flags);
            var candidates = (List<WorldCard>)Get(controller, "_matching");
            foreach (WorldCard selected in new[] { japanese, english })
            {
                Check((bool)find.Invoke(controller, new object[] { selected }), "Collection/highlight candidate search succeeds");
                Check(candidates.Contains(english) && candidates.Contains(japanese), "Collection/highlight candidate search includes both languages");
                foreach (WorldCard excluded in new[] { wrongGrade, normalA, held, flying, reveal, locked, shelved, inactive })
                    Check(!candidates.Contains(excluded), "Collection/highlight excludes ineligible card: " + Array.IndexOf(tracked.ToArray(), excluded));
            }
            var legacy = JsonUtility.FromJson<SkillAutoshelfContext>("{\"psa\":true,\"psaSet\":1,\"psaNumber\":8}");
            object restoredGroup = Activator.CreateInstance(groupType);
            groupType.GetField("psa", Flags).SetValue(restoredGroup, legacy.psa);
            groupType.GetField("set", Flags).SetValue(restoredGroup, legacy.psaSet);
            groupType.GetField("number", Flags).SetValue(restoredGroup, legacy.psaNumber);
            Check(Matches(restoredGroup, english) && Matches(restoredGroup, japanese), "Old Japanese autoshelf context now matches both languages");
            report.AppendLine("PASS " + checks + " checks; no player save or scene edits.");
            Debug.Log("[Skills] Mixed-language TCG validation: " + checks + " checks passed.");
        }
        catch (Exception error) { report.AppendLine("FAIL " + error); throw; }
        finally
        {
            foreach (WorldCard card in tracked) CardGroundStack.Untrack(card);
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var definition in definitions) Object.DestroyImmediate(definition);
            typeof(CardGroundStack).GetField("<MembershipRevision>k__BackingField", Flags).SetValue(null, revision);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/skill-mixed-language-validation.txt", report.ToString());
        }
    }

    static object Get(object target, string field) => target.GetType().GetField(field, Flags).GetValue(target);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
}
