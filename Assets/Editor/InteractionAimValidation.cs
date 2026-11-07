using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Checks aim/preview regressions in a disposable scene, without starting gameplay or loading saves.</summary>
public static class InteractionAimValidation
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);

    [MenuItem("TCG Card Chaos/Validation/Interaction Aim (Isolated)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PlayerCardHand.Instance != null)
            throw new InvalidOperationException("Run outside Play Mode.");
        var report = new StringBuilder();
        int checks = 0;
        var scene = EditorSceneManager.NewPreviewScene();
        InteractionController controller = null;
        CardShelf shelf = null;
        try
        {
            void Check(bool condition, string label)
            {
                if (!condition) throw new InvalidOperationException(label);
                checks++;
            }
            GameObject Create(string name)
            {
                var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
                go.SetActive(false);
                SceneManager.MoveGameObjectToScene(go, scene);
                return go;
            }
            shelf = Create("Isolated shelf").AddComponent<CardShelf>();
            var slots = (List<CardShelfSlot>)Get(shelf, "_slots");
            float height = CardDimensions.Height * CardDimensions.WorldCardScale;
            float width = CardDimensions.Width * CardDimensions.WorldCardScale;
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                var go = Create("Seat " + row + " " + col);
                go.transform.SetParent(shelf.transform, false);
                go.transform.localPosition = new Vector3(col * width * 1.5f, row * height * 1.4f, 0f);
                var slot = go.AddComponent<CardShelfSlot>();
                Set(slot, "rowIndex", row);
                Set(slot, "columnIndex", col);
                slots.Add(slot);
            }
            Set(shelf, "_slotCacheValid", true);
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
            {
                shelf.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                foreach (var slot in slots)
                foreach (float vertical in new[] { -0.35f, 0f, 0.35f })
                {
                    Vector3 center = slot.transform.position + slot.transform.up * (height * 0.5f + 0.003f);
                    Vector3 target = center + slot.transform.up * height * vertical;
                    Vector3 origin = target + shelf.transform.rotation * new Vector3(0.2f, 0.1f, 2f);
                    var ray = new Ray(origin, (target - origin).normalized);
                    var selected = Call(shelf, "FindClosestSlotToAim", false, ray, true);
                    Check(ReferenceEquals(selected, slot), "Crosshair inside card selects its seat, yaw=" + yaw + " offset=" + vertical);
                    Check(shelf.TryGetSkillAimRow(ray, out int row) && row == slot.RowIndex, "Skill row agrees with crosshair");
                    Call(shelf, "ShowPlacementOutline", slot);
                    var outline = (GameObject)Get(shelf, "_placementOutline");
                    Check((outline.transform.position - center).sqrMagnitude < 0.000001f, "Outline matches target card center");
                }
            }
            report.AppendLine("PASS: 3 rows / 3 columns, card upper/lower/center aim, four cabinet rotations, outline and skill-row alignment.");
            Check(Call(shelf, "FindClosestSlotToAim", false, new Ray(new Vector3(100f, 100f, 2f), Vector3.back), true) == null,
                "Outside aim tolerance selects nothing");

            var cameraObject = Create("Isolated interaction camera");
            var camera = cameraObject.AddComponent<Camera>();
            controller = cameraObject.AddComponent<InteractionController>();
            Set(controller, "viewCamera", camera);
            Set(controller, "_inspectPreview", cameraObject.AddComponent<CardInspectPreview>());
            Set(controller, "_packInspectPreview", cameraObject.AddComponent<PackInspectPreview>());
            Set(controller, "_psaInspectPreview", cameraObject.AddComponent<PsaInspectPreview>());
            Call(controller, "BuildPromptUI");
            var card = Create("Isolated normal card").AddComponent<WorldCard>();
            var psa = Create("Isolated graded card").AddComponent<WorldCard>();
            Set(psa, "psaSlotNumber", 8);
            var pack = Create("Isolated pack").AddComponent<WorldBoosterPack>();
            foreach (var item in new Component[] { card, pack, psa, card })
            {
                bool isCard = item is WorldCard;
                Set(controller, "_raycastAimedCard", isCard ? item : null);
                Set(controller, "_raycastAimedPack", isCard ? null : item);
                Call(controller, isCard ? "HandleGroundCardTarget" : "HandleGroundPackTarget", item);
                Check(ReferenceEquals(Get(controller, "_currentTarget"), item), "Target available immediately: " + item.name);
                Check((bool)Call(controller, "ShouldUseCurrentTargetForInteract"), "Pickup available before preview delay");
                Check((bool)Get(item, "_interactionHighlighted"), "Immediate outline: " + item.name);
                Check((float)Get(controller, "_inspectPreviewTimer") == 0f, "Switch resets preview delay");
                object preview = Get(controller, isCard ? (item == psa ? "_psaInspectPreview" : "_inspectPreview") : "_packInspectPreview");
                var root = (RectTransform)Get(preview, "_previewRoot");
                Check(root == null || !root.gameObject.activeSelf, "Large preview hidden on target entry");
                Set(controller, "_inspectPreviewTimer", 0.05f - Time.deltaTime);
                Call(controller, "UpdateInspectPreview");
                root = (RectTransform)Get(preview, "_previewRoot");
                Check(root == null || !root.gameObject.activeSelf, "Preview remains hidden before 0.15 seconds");
                Check(ReferenceEquals(Get(controller, "_currentTarget"), item), "Waiting for preview preserves immediate pickup");
                Call(controller, isCard ? "HandleGroundCardTarget" : "HandleGroundPackTarget", item);
                Check(Mathf.Abs((float)Get(controller, "_inspectPreviewTimer") - 0.05f) < 0.0001f, "Stable aim preserves preview timer");
                Set(controller, "_inspectPreviewTimer", 0.15f);
                Call(controller, "UpdateInspectPreview");
                root = (RectTransform)Get(preview, "_previewRoot");
                Check(root != null && root.gameObject.activeSelf, "Large preview shown after delay");
                Check(ReferenceEquals(Get(controller, "_currentTarget"), item), "Preview preserves pickup target");
            }
            Call(controller, "ClearTarget");
            Check(Get(controller, "_currentTarget") == null && CardInteractionFocus.FocusedCard == null, "Leaving target clears interaction focus");
            Check(!(bool)Get(card, "_interactionHighlighted"), "Leaving target clears outline");
            Set(controller, "inspectPreviewDelay", 0f);
            Set(controller, "_raycastAimedCard", card);
            Call(controller, "HandleGroundCardTarget", card);
            Call(controller, "UpdateInspectPreview");
            Check(ReferenceEquals(Get(controller, "_currentTarget"), card), "Zero-delay preview does not clear pickup target");
            report.AppendLine("PASS: normal card / pack / graded card switches, immediate pickup eligibility and outline, delayed preview, clear focus, zero-delay preview.");
            report.AppendLine("PASS: " + checks + " checks. No Play Mode or player save writes.");
        }
        catch (Exception exception)
        {
            report.AppendLine("FAIL: " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            if (controller != null) Call(controller, "ClearTarget");
            if (shelf != null) Call(shelf, "DestroyPlacementOutline");
            EditorSceneManager.ClosePreviewScene(scene);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/interaction-aim-validation.txt", report.ToString());
            Debug.Log(report.ToString());
        }
    }
}
