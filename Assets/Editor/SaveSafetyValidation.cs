using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Editor-only regressions: disposable save files and preview-scene objects; never loads a player save.</summary>
public static class SaveSafetyValidation
{
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    const string ReportPath = "Temp/save-safety-validation.txt";
    static FieldInfo Field(Type type, string name) => type.GetField(name, Flags) ?? throw new MissingFieldException(type.Name, name);
    static object Call(Type type, object target, string name, params object[] args) =>
        type.GetMethod(name, Flags).Invoke(target, args);
    static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);

    [MenuItem("TCG Card Chaos/Save/Validate Save Safety (Isolated)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || GameSaveManager.Instance != null)
            throw new InvalidOperationException("Run outside Play Mode, without a live save manager.");
        var report = new StringBuilder("Save safety validation — no player save files or scene assets modified.\n");
        int checks = 0;
        Scene scene = EditorSceneManager.NewPreviewScene();
        string sandbox = Path.GetFullPath("Temp/SaveSafetyValidation/" + Guid.NewGuid().ToString("N"));
        object originalRoot = Field(typeof(SaveFileIO), "_rootFolder").GetValue(null);
        object originalPlayer = Field(typeof(CardCollisionUtility), "_cachedPlayer").GetValue(null);
        var registry = (IDictionary)Field(typeof(PersistentIdRegistry), "Packs").GetValue(null);
        var restoredIds = (HashSet<string>)Field(typeof(GameSaveRestore), "RestoredIds").GetValue(null);
        var pendingPacks = (IList)Field(typeof(GameSaveRestore), "PendingPackPhysics").GetValue(null);
        var pendingCards = (IList)Field(typeof(GameSaveRestore), "PendingCardPhysics").GetValue(null);
        int pendingPackCount = pendingPacks.Count, pendingCardCount = pendingCards.Count;
        var addedIds = new List<string>();
        var trackedPacks = new List<WorldBoosterPack>();
        var created = new List<GameObject>();
        GameSaveSettings settings = null;
        try
        {
            void Check(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException(message);
                checks++;
                report.AppendLine("PASS " + message);
            }
            GameObject Create(string name, params Type[] components)
            {
                var go = new GameObject(name, components) { hideFlags = HideFlags.HideAndDontSave };
                go.SetActive(false);
                SceneManager.MoveGameObjectToScene(go, scene);
                created.Add(go);
                return go;
            }
            var inactive = Create("Isolated save objects");
            var scatter = Create("Isolated scatter").transform;
            var previewPlayer = Create("Isolated player").AddComponent<FirstPersonController>();
            Field(typeof(CardCollisionUtility), "_cachedPlayer").SetValue(null, previewPlayer);
            Field(typeof(SaveFileIO), "_rootFolder").SetValue(null, sandbox);
            settings = ScriptableObject.CreateInstance<GameSaveSettings>();
            var manager = Create("Isolated save coordinator").AddComponent<GameSaveManager>();
            Set(manager, "_settings", settings);
            bool Resolve(GameSaveManager.SaveRequestKind kind, string id, out string resolved, out SaveSlotType type, out int index)
            {
                object[] args = { kind, id, null, SaveSlotType.Auto, -1 };
                bool ok = (bool)Call(typeof(GameSaveManager), manager, "TryResolveSlot", args);
                resolved = (string)args[2]; type = (SaveSlotType)args[3]; index = (int)args[4];
                return ok;
            }
            void Write(string id, SaveSlotType type, int index, int progress)
            {
                var data = new GameSaveData { slotId = id, slotType = type, slotIndex = index,
                    buildVariant = GameBuildVariant.Current, cardsPlaced = progress, timestampUnix = 1000 + progress };
                Check(SaveFileIO.TryWriteSaveAndMeta(data, data.ToMetadata(false), out string error), "Isolated write " + id + ": " + error);
            }
            Check(Resolve(GameSaveManager.SaveRequestKind.Manual, null, out string first, out var firstType, out int firstIndex)
                && first == "manual_0" && firstType == SaveSlotType.Manual, "First manual save uses manual_0");
            Write(first, firstType, firstIndex, 123);
            string originalManual = File.ReadAllText(SaveFileIO.GetSavePath(first));
            foreach (string invalid in new[] { "autosave_0", "autosave_1", "autosave_2", "../manual_0", "manual_-1", "manual_00", "manual_30", "other_1" })
                Check(!Resolve(GameSaveManager.SaveRequestKind.Manual, invalid, out _, out _, out _), "Manual write rejects " + invalid);
            // Both a continuing session and a fresh game's rotating/exit saves share this path.
            foreach (var kind in new[] { GameSaveManager.SaveRequestKind.Autosave, GameSaveManager.SaveRequestKind.Milestone,
                GameSaveManager.SaveRequestKind.Exit, GameSaveManager.SaveRequestKind.Autosave,
                GameSaveManager.SaveRequestKind.Exit, GameSaveManager.SaveRequestKind.Milestone })
            {
                Check(Resolve(kind, null, out string id, out var type, out int index)
                    && type == SaveSlotType.Auto && id.StartsWith("autosave_"), "Automatic/exit save stays in autosave namespace");
                Write(id, type, index, index + 1);
                Call(typeof(GameSaveManager), null, "AdvanceAutosaveIndex", index);
                Check(File.ReadAllText(SaveFileIO.GetSavePath(first)) == originalManual, "Manual file survives autosave rotation unchanged");
            }
            Check(Resolve(GameSaveManager.SaveRequestKind.Manual, null, out string second, out _, out _)
                && second == "manual_1", "New manual save does not reuse occupied manual_0");
            Check(Resolve(GameSaveManager.SaveRequestKind.Manual, first, out _, out var overwriteType, out _)
                && overwriteType == SaveSlotType.Manual, "Explicit manual overwrite retains manual slot type");
            Write(first, SaveSlotType.Manual, 0, 456);
            Check(File.ReadAllText(SaveFileIO.GetBackupPath(first)) == originalManual, "Explicit overwrite preserves previous payload in backup");
            Check(SaveFileIO.TryLoadSave(first, out var read, out _) && read.cardsPlaced == 456, "Manual file round-trip retains progress");
            File.WriteAllText(SaveFileIO.GetSavePath(first), "invalid JSON");
            Check(SaveFileIO.TryLoadSave(first, out read, out _) && read.cardsPlaced == 123, "Damaged latest JSON falls back to manual backup");
            for (int i = 1; i < settings.MaxManualSlots; i++)
                File.WriteAllText(SaveFileIO.GetBackupPath(SaveFileIO.ManualSlotId(i)), "reserved backup");
            Check(!Resolve(GameSaveManager.SaveRequestKind.Manual, null, out _, out _, out _), "Full manual slots never overwrite an existing save implicitly");
            Check(Resolve(GameSaveManager.SaveRequestKind.Manual, first, out _, out _, out _), "Full list still permits an explicitly selected manual slot");

            var panelRoot = Create("Isolated save panel", typeof(RectTransform));
            var panel = panelRoot.AddComponent<SaveGamePanelView>();
            var confirmRoot = Create("Isolated confirmation", typeof(RectTransform));
            confirmRoot.transform.SetParent(panelRoot.transform, false);
            var confirm = confirmRoot.AddComponent<LoadGameConfirmView>();
            Set(panel, "confirmView", confirm);
            var row = Create("Isolated save row", typeof(RectTransform)).AddComponent<LoadGameSlotView>();
            Set(row, "_boundSlotId", "manual_0"); Set(row, "_isEmpty", false);
            Call(typeof(SaveGamePanelView), panel, "OnSlotClicked", row);
            Check(confirm.IsOpen && (string)Field(typeof(SaveGamePanelView), "_pendingSlotId").GetValue(panel) == "manual_0",
                "Clicking a populated manual row opens overwrite confirmation first");
            Call(typeof(SaveGamePanelView), panel, "OnConfirmNo");
            Check(!confirm.IsOpen && Field(typeof(SaveGamePanelView), "_pendingSlotId").GetValue(panel) == null,
                "Cancel clears pending overwrite");
            Set(row, "_boundSlotId", "autosave_0");
            Call(typeof(SaveGamePanelView), panel, "OnSlotClicked", row);
            Check(!confirm.IsOpen, "Stale autosave row cannot be selected in Save panel");

            var listRoot = Create("Isolated load list", typeof(RectTransform));
            var content = Create("Content", typeof(RectTransform), typeof(VerticalLayoutGroup)).transform;
            content.SetParent(listRoot.transform, false);
            listRoot.AddComponent<ScrollRect>().content = (RectTransform)content;
            var rows = new LoadGameSlotView[5];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = Create("Row_" + i, typeof(RectTransform)).AddComponent<LoadGameSlotView>();
                rows[i].transform.SetParent(content, false);
            }
            SaveSlotListLayout.ShowLoadGroupHeaders(listRoot.transform, rows, 2, 3);
            Check(content.GetChild(0).name == "ManualSavesHeader" && content.GetChild(3).name == "AutosavesHeader",
                "Load list separates manual and automatic rows with headings");
            SaveSlotListLayout.ShowLoadGroupHeaders(listRoot.transform, rows, 1, 3);
            Check(content.GetChild(0).name == "ManualSavesHeader" && content.GetChild(2).name == "AutosavesHeader",
                "Group order remains correct after list refresh");
            SaveSlotListLayout.HideLoadGroupHeaders(listRoot.transform);
            Check(!content.Find("ManualSavesHeader").gameObject.activeSelf && !content.Find("AutosavesHeader").gameObject.activeSelf,
                "Save-panel clone hides Load-only group headings");
            var table = Resources.Load<LocalizationTable>(LocalizationTable.ResourcePath);
            foreach (string key in new[] { LocalizationKeys.LoadGameManualSaves, LocalizationKeys.LoadGameAutosaves })
            {
                var entry = table.Entries.First(e => e.key == key);
                Check(entry.values.Length == 12 && entry.values.All(v => !string.IsNullOrWhiteSpace(v)), key + " has all 12 translations");
            }

            CardArtLibrary.EnsureLoaded();
            var authored = Object.FindObjectsByType<WorldBoosterPack>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(p => !EditorSceneManager.IsPreviewScene(p.gameObject.scene) && p.GetComponent<PhysicsLevelItem>() != null).ToArray();
            Check(authored.Length > 0, "Authored packs available in the open game scene");
            int inverted = 0;
            foreach (WorldBoosterPack source in authored)
            {
                var clone = Object.Instantiate(source.gameObject, inactive.transform, true);
                clone.hideFlags = HideFlags.HideAndDontSave;
                clone.SetActive(false);
                var pack = clone.GetComponent<WorldBoosterPack>();
                pack.enabled = false;
                if (pack.transform.up.y < 0f) inverted++;
                Set(pack, "_collider", clone.GetComponent<BoxCollider>());
                pack.RegisterForAuthoredGround();
                trackedPacks.Add(pack);
                PersistentId.GetOrCreate(clone).AssignNew();
                string id = clone.GetComponent<PersistentId>().Value;
                addedIds.Add(id); registry.Add(id, pack);
                PackSaveRecord Capture() => (PackSaveRecord)Call(typeof(GameSaveWorldCollector), null, "CollectPack", pack, new HashSet<WorldBoosterPack>());
                var record = Capture();
                var before = MeshPose(pack.PackVisualRoot);
                Vector3 rootBefore = pack.transform.position;
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    restoredIds.Remove(id);
                    record = JsonUtility.FromJson<PackSaveRecord>(JsonUtility.ToJson(record));
                    Check(record.worldVisualPose != null && record.worldVisualPose.IsValid, source.name + " JSON visual pose valid");
                    Call(typeof(GameSaveRestore), null, "RestorePack", record, scatter, null);
                    pack.RegisterForAuthoredGround();
                    Check(SamePose(before, MeshPose(pack.PackVisualRoot)) && Vector3.Distance(rootBefore, pack.transform.position) < 0.0001f,
                        source.name + " visible pose preserved after load " + (cycle + 1));
                    Check(record.contents.SequenceEqual(Capture().contents), source.name + " saved contents unchanged");
                    record = Capture();
                }
                // Old version-1 records lack child transforms; existing scene layout is the fallback.
                record.worldVisualPose = null;
                restoredIds.Remove(id);
                Call(typeof(GameSaveRestore), null, "RestorePack", record, scatter, null);
                Check(SamePose(before, MeshPose(pack.PackVisualRoot)), source.name + " legacy authored save preserves visible pose");
                registry.Remove(id); restoredIds.Remove(id);
                CardGroundStack.UntrackPack(pack);
                Object.DestroyImmediate(clone);
            }
            Check(inverted > 0, "Upside-down authored packs included: " + inverted + "/" + authored.Length);

            foreach (Quaternion rotation in new[] { Quaternion.identity, Quaternion.Euler(180, 32, 0), Quaternion.Euler(62, 123, 15) })
            foreach (bool physical in new[] { false, true })
            {
                var pack = PackFactory.CreateWorldPack(new Vector3(1, 0.4f, 2), rotation);
                SceneManager.MoveGameObjectToScene(pack.gameObject, scene);
                created.Add(pack.gameObject); pack.gameObject.SetActive(false);
                if (physical) pack.PrepareEditorPhysicsPlacement();
                var pose = pack.CaptureWorldVisualPose();
                var before = MeshPose(pack.PackVisualRoot);
                for (int i = 0; i < 3; i++)
                {
                    pack.Initialize(null, preserveExistingVisualLayout: true);
                    pack.RestoreSavedWorldPose(false, 0, physical, JsonUtility.FromJson<PackWorldVisualPose>(JsonUtility.ToJson(pose)));
                    Check(SamePose(before, MeshPose(pack.PackVisualRoot)), "Generated pack pose preserved: " + rotation.eulerAngles + ", physical=" + physical);
                }
                if (physical)
                {
                    pack.ResumeSavedPhysics(new ThrownPhysicsSaveState { isSimulating = true, isSleeping = true });
                    Check(SamePose(before, MeshPose(pack.PackVisualRoot)), "Physics resume preserves saved visual pose");
                    Check(pack.GetComponent<Rigidbody>().IsSleeping(), "Saved resting pack remains asleep on restore");
                }
            }

            // Normal and TCG cards already restore roots and rebuild their canonical child basis.
            foreach (bool psa in new[] { false, true })
            foreach (bool back in new[] { false, true })
            foreach (bool physical in new[] { false, true })
            {
                var definition = Resources.LoadAll<CardDefinition>("Cards/Definitions").First(d => d != null);
                var card = psa ? CardFactory.CreateWorldPsaCard(new Vector3(2, .3f, 2), Quaternion.Euler(physical ? 180 : 0, 23, 0), 8)
                    : CardFactory.CreateWorldCard(new Vector3(2, .3f, 2), Quaternion.Euler(physical ? 180 : 0, 23, 0), definition, 0);
                SceneManager.MoveGameObjectToScene(card.gameObject, scene);
                created.Add(card.gameObject); card.gameObject.SetActive(false);
                card.SetGroundShowsBack(back);
                if (physical) Call(typeof(WorldCard), card, "EnsureRigidbody");
                Call(typeof(WorldCard), card, "RefreshRenderMode");
                var before = MeshPose(card.transform);
                var record = new CardSaveRecord { location = CardRuntimeLocation.World, faceDown = back };
                record.SetPosition(card.transform.position); record.SetRotation(card.transform.rotation);
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    record = JsonUtility.FromJson<CardSaveRecord>(JsonUtility.ToJson(record));
                    Call(typeof(GameSaveRestore), null, "PlaceWorldCard", card, record, scatter);
                    Call(typeof(WorldCard), card, "RefreshRenderMode");
                    Check(Vector3.Distance(card.transform.position, record.Position) < .0001f && SamePose(before, MeshPose(card.transform)),
                        (psa ? "TCG" : "Normal") + " card retains pose, faceDown=" + back + ", physical=" + physical + ", load=" + cycle);
                }
            }
            report.AppendLine("SUCCESS " + checks + " checks. Unity remains outside Play Mode.");
        }
        catch (Exception exception)
        {
            report.AppendLine("FAIL " + exception);
            Debug.LogError("[Save Safety] " + exception);
        }
        finally
        {
            foreach (var pack in trackedPacks) if (pack != null) CardGroundStack.UntrackPack(pack);
            foreach (string id in addedIds) { registry.Remove(id); restoredIds.Remove(id); }
            while (pendingPacks.Count > pendingPackCount) pendingPacks.RemoveAt(pendingPacks.Count - 1);
            while (pendingCards.Count > pendingCardCount) pendingCards.RemoveAt(pendingCards.Count - 1);
            foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            EditorSceneManager.ClosePreviewScene(scene);
            if (settings != null) Object.DestroyImmediate(settings);
            Field(typeof(SaveFileIO), "_rootFolder").SetValue(null, originalRoot);
            Field(typeof(CardCollisionUtility), "_cachedPlayer").SetValue(null, originalPlayer);
            if (Directory.Exists(sandbox)) Directory.Delete(sandbox, true);
            Directory.CreateDirectory("Temp"); File.WriteAllText(ReportPath, report.ToString());
        }
        Debug.Log("[Save Safety] " + checks + " checks; report: " + ReportPath);
    }

    static Matrix4x4[] MeshPose(Transform root) => root.GetComponentsInChildren<MeshFilter>(true)
        .Where(m => m.sharedMesh != null).Select(m => m.transform.localToWorldMatrix).ToArray();
    static bool SamePose(Matrix4x4[] before, Matrix4x4[] after)
    {
        if (before.Length != after.Length) return false;
        for (int i = 0; i < before.Length; i++)
        for (int j = 0; j < 16; j++)
            if (Mathf.Abs(before[i][j] - after[i][j]) > .0001f) return false;
        return true;
    }
}
