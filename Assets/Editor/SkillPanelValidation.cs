using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Validates an isolated prefab preview. Never opens a game or changes player progress.</summary>
public static class SkillPanelValidation
{
    const string PrefabPath = "Assets/Resources/UI/Skills/SkillUI.prefab";
    const string UnlockPrefabPath = "Assets/Resources/UI/Skills/MinorSkillUnlock.prefab";
    const string ReportPath = "Temp/skill-panel-validation.txt";
    const string Body = "Panel/Body/";
    static readonly string[] IconGuids = {
        "70ed2ed69e3c4ad582b261e096373126", "7b40757330ea4d9491b52f925f00ba94",
        "b6590136fba6456d9260a8bacecde1c0", "2097be328cef46d9939d1b376d516c8c",
        "82566535585a46109a6752a4171f5232"
    };

    [MenuItem("TCG Card Chaos/UI/Validate Skill Panel")]
    public static void Validate()
    {
        var report = new StringBuilder("Skill panel validation\n");
        report.AppendLine("Isolated 1920x1080 preview; no scene, save, PlayerPrefs or localization state changes.");
        int errors = 0, scenarios = 0, labelsChecked = 0;
        GameObject root = null;
        var fonts = new PreviewFonts();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run this validation outside Play Mode.");
            LocalizationTable table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
                "Assets/Resources/Localization/LocalizationTable.asset");
            if (table == null) throw new InvalidOperationException("Missing localization table.");
            ValidateTranslations(table, report, ref errors);
            root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) throw new InvalidOperationException("Missing skill prefab.");
            ValidateStructure(root.transform, report, ref errors);
            if (errors > 0) throw new InvalidOperationException("Fix structural/translation errors before layout validation.");

            PreparePreview(root, fonts);
            TMP_Text[] labels = root.transform.Find("Panel").GetComponentsInChildren<TMP_Text>(true);
            Canvas.ForceUpdateCanvases();
            for (int language = 0; language < GameLanguages.Count; language++)
            {
                GameLanguage locale = (GameLanguage)language;
                for (int selected = 0; selected < SkillCatalog.Count; selected++)
                {
                    // Exercise locked, unlocked/next and max strings without invoking SkillProgress.
                    int[] levels = { 0, 1, SkillCatalog.MaxLevel(selected) };
                    foreach (int level in levels)
                    {
                        Populate(root.transform, table, locale, selected, level);
                        Canvas.ForceUpdateCanvases();
                        string context = locale + "/" + SkillCatalog.Keys[selected] + "/level=" + level;
                        int before = errors;
                        ValidateLabels(root.transform, labels, context, report, ref errors, ref labelsChecked);
                        report.AppendLine((errors == before ? "PASS " : "FAIL ") + context);
                        scenarios++;
                    }
                }
                for (int minor = 0; minor < MinorSkillProgress.Count; minor++)
                {
                    foreach (bool unlocked in new[] { false, true })
                    {
                        PopulateMinor(root.transform, table, locale, minor, unlocked);
                        Canvas.ForceUpdateCanvases();
                        string context = locale + "/minor" + (minor + 1) + "/" + (unlocked ? "unlocked" : "locked");
                        int before = errors;
                        ValidateLabels(root.transform, labels, context, report, ref errors, ref labelsChecked);
                        report.AppendLine((errors == before ? "PASS " : "FAIL ") + context);
                        scenarios++;
                    }
                }
            }
        }
        catch (Exception exception)
        {
            Error(report, ref errors, exception.ToString());
        }
        finally
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
            fonts.Dispose();
            report.AppendLine("Scenarios: " + scenarios + "; label layouts: " + labelsChecked + "; errors: " + errors);
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ReportPath, report.ToString(), new UTF8Encoding(false));
        }
        string result = "[Skills] " + scenarios + " locale/skill/state scenarios, " + labelsChecked
            + " label layouts, " + errors + " errors. Report: " + Path.GetFullPath(ReportPath);
        if (errors == 0) Debug.Log(result); else Debug.LogError(result);
    }

    [MenuItem("TCG Card Chaos/UI/Validate Minor Skill Unlock Popup")]
    public static void ValidateUnlock()
    {
        var report = new StringBuilder("Minor skill unlock popup validation\n");
        report.AppendLine("Isolated 1920x1080 preview; four messages across 12 locales; no live unlock/save events.");
        int errors = 0, scenarios = 0, labelsChecked = 0;
        GameObject root = null;
        var fonts = new PreviewFonts();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run this validation outside Play Mode.");
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/Resources/Localization/LocalizationTable.asset");
            if (table == null) throw new InvalidOperationException("Missing localization table.");
            ValidateTranslations(table, report, ref errors);
            root = PrefabUtility.LoadPrefabContents(UnlockPrefabPath);
            ValidateUnlockStructure(root.transform, report, ref errors);
            if (errors > 0) throw new InvalidOperationException("Fix popup structure/translations before validating layout.");
            PreparePreview(root, fonts);
            TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
            for (int language = 0; language < GameLanguages.Count; language++)
                for (int skill = 0; skill < MinorSkillProgress.Count; skill++)
                {
                    PopulateUnlock(root.transform, table, (GameLanguage)language, skill);
                    Canvas.ForceUpdateCanvases();
                    string context = (GameLanguage)language + "/unlock" + (skill + 1);
                    int before = errors;
                    ValidateLabels(root.transform, labels, context, report, ref errors, ref labelsChecked);
                    report.AppendLine((errors == before ? "PASS " : "FAIL ") + context);
                    scenarios++;
                }
        }
        catch (Exception exception) { Error(report, ref errors, exception.ToString()); }
        finally
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
            fonts.Dispose();
            report.AppendLine("Scenarios: " + scenarios + "; label layouts: " + labelsChecked + "; errors: " + errors);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/minor-unlock-ui-validation.txt", report.ToString(), new UTF8Encoding(false));
        }
        string result = "[Minor Skills] Unlock popup: " + scenarios + " locale/message scenarios, " + labelsChecked
            + " label layouts, " + errors + " errors. Report: " + Path.GetFullPath("Temp/minor-unlock-ui-validation.txt");
        if (errors == 0) Debug.Log(result); else Debug.LogError(result);
    }

    static void ValidateUnlockStructure(Transform root, StringBuilder report, ref int errors)
    {
        Required<MinorSkillUnlockView>(root, "", report, ref errors);
        Required<Image>(root, "Panel", report, ref errors);
        Required<Image>(root, "Panel/Body", report, ref errors);
        Required<Image>(root, "Panel/Body/Icon", report, ref errors);
        Required<Button>(root, "Panel/Body/Close", report, ref errors);
        foreach (string path in new[] { "Title", "Message", "Close/Label" })
            Required<TMP_Text>(root, "Panel/Body/" + path, report, ref errors);
        var body = Required<RectTransform>(root, "Panel/Body", report, ref errors);
        if (body != null && body.sizeDelta != new Vector2(800f, 520f))
            Error(report, ref errors, "Unlock popup must retain its 800x520 reference size, half the skill-panel width.");
    }

    static void PopulateUnlock(Transform root, LocalizationTable table, GameLanguage language, int skill)
    {
        root.Find("Panel/Body/Title").GetComponent<TMP_Text>().text = table.Get("minor.unlock.title", language);
        root.Find("Panel/Body/Message").GetComponent<TMP_Text>().text = string.Format(CultureInfo.InvariantCulture,
            table.Get("minor.unlock.message", language), table.Get("minor.skill" + (skill + 1) + ".name", language))
            + "\n" + table.Get("minor.skill" + (skill + 1) + ".description", language);
        root.Find("Panel/Body/Icon").GetComponent<Image>().sprite = root.GetComponent<MinorSkillUnlockView>().GetMinorSkillIcon(skill);
        root.Find("Panel/Body/Close/Label").GetComponent<TMP_Text>().text = table.Get("minor.unlock.close", language);
    }

    static void ValidateLabels(Transform root, TMP_Text[] labels, string context, StringBuilder report,
        ref int errors, ref int labelsChecked)
    {
        foreach (TMP_Text label in labels)
        {
            if (!label.gameObject.activeInHierarchy) continue;
            label.ForceMeshUpdate(true, true);
            labelsChecked++;
            Rect box = label.rectTransform.rect;
            Vector4 margin = label.margin;
            float width = Mathf.Max(0f, box.width - margin.x - margin.z);
            float height = Mathf.Max(0f, box.height - margin.y - margin.w);
            Bounds bounds = label.textBounds;
            bool exceeds = bounds.size.x > width + 1f || bounds.size.y > height + 1f;
            if (label.isTextOverflowing || label.isTextTruncated || exceeds)
                Error(report, ref errors, context + " " + RelativePath(root, label.transform)
                    + " overflow=" + label.isTextOverflowing + " truncated=" + label.isTextTruncated
                    + " firstOverflow=" + label.firstOverflowCharacterIndex
                    + " font=" + label.fontSize.ToString("0.##", CultureInfo.InvariantCulture)
                    + " bounds=" + bounds.size.ToString("F1") + " available=" + width + "x" + height
                    + " text=" + label.text.Replace("\n", " | "));
        }
    }

    [MenuItem("TCG Card Chaos/UI/Render Skill Panel Preview")]
    public static void RenderPreview() => RenderPreview(false);

    [MenuItem("TCG Card Chaos/UI/Render Locked Minor Skill Preview")]
    public static void RenderMinorPreview() => RenderPreview(true);

    [MenuItem("TCG Card Chaos/UI/Render Minor Skill Unlock Preview")]
    public static void RenderUnlockPreview() => RenderPreview(false, true);

    [MenuItem("TCG Card Chaos/UI/Render Unlocked Minor Skills")]
    public static void RenderUnlockedMinorPreview() => RenderPreview(true, false, true);

    static void RenderPreview(bool minor, bool unlock = false, bool minorUnlocked = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Render the isolated preview outside Play Mode.");
        GameObject root = null;
        var fonts = new PreviewFonts();
        RenderTexture target = null;
        Texture2D readback = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            LocalizationTable table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
                "Assets/Resources/Localization/LocalizationTable.asset");
            if (table == null) throw new InvalidOperationException("Missing localization table.");
            root = PrefabUtility.LoadPrefabContents(unlock ? UnlockPrefabPath : PrefabPath);
            var report = new StringBuilder();
            int errors = 0;
            if (unlock) ValidateUnlockStructure(root.transform, report, ref errors);
            else ValidateStructure(root.transform, report, ref errors);
            if (errors != 0) throw new InvalidOperationException(report.ToString());
            Canvas canvas = PreparePreview(root, fonts);
            var cameraObject = new GameObject("Skill preview camera", typeof(Camera));
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(cameraObject, root.scene);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.cameraType = CameraType.Preview;
            camera.scene = root.scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(root.scene);
            camera.orthographic = true;
            camera.orthographicSize = 540f;
            camera.aspect = 1920f / 1080f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 2000f;
            camera.transform.position = new Vector3(0f, 0f, -1000f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(16, 20, 38, 255);
            camera.cullingMask = 1 << 5;
            camera.useOcclusionCulling = false;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            canvas.worldCamera = camera;
            target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32)
            { hideFlags = HideFlags.HideAndDontSave, antiAliasing = 1 };
            target.Create();
            camera.targetTexture = target;
            readback = new Texture2D(1920, 1080, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave };
            Directory.CreateDirectory("Temp");
            var renderReport = new StringBuilder("Skill preview render diagnostics\n");
            renderReport.AppendLine("Canvas=" + ((RectTransform)canvas.transform).rect
                + " root=" + ((RectTransform)root.transform).rect + " camera=" + camera.transform.position
                + " sceneMask=" + camera.overrideSceneCullingMask);
            GameLanguage[] languages = { GameLanguage.English, GameLanguage.Turkish };
            foreach (GameLanguage language in languages)
            {
                if (unlock) PopulateUnlock(root.transform, table, language, 0);
                else if (minor) PopulateMinor(root.transform, table, language, 0, minorUnlocked);
                else Populate(root.transform, table, language, (int)CardSkill.Insight, 1);
                Canvas.ForceUpdateCanvases();
                foreach (TMP_Text text in root.transform.Find("Panel").GetComponentsInChildren<TMP_Text>())
                    text.ForceMeshUpdate(true, true);
                Canvas.ForceUpdateCanvases();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
                else camera.Render();
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                readback.Apply();
                if (IsUniform(readback))
                {
                    // Native Canvas submission can be absent in prefab preview scenes. Use the same
                    // Unity-generated Image/TMP meshes and materials, with this camera's projection.
                    int meshes = DrawCanvasMeshes(root.transform.Find("Panel"), camera, target, renderReport);
                    RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                    readback.Apply();
                    renderReport.AppendLine(language + ": explicit Canvas mesh draw count=" + meshes);
                }
                File.WriteAllText("Temp/skill-panel-preview-render.txt", renderReport.ToString());
                if (IsUniform(readback))
                    throw new InvalidOperationException("Preview remained blank; see Temp/skill-panel-preview-render.txt.");
                string path = "Temp/skill-panel-preview-" + (unlock ? "unlock-" : minor ? (minorUnlocked ? "minor-open-" : "minor-") : "")
                    + (language == GameLanguage.English ? "en" : "tr") + ".png";
                File.WriteAllBytes(path, readback.EncodeToPNG());
                Debug.Log("[Skills] Rendered isolated Unity UI preview: " + Path.GetFullPath(path));
            }
        }
        finally
        {
            RenderTexture.active = previous;
            if (readback != null) Object.DestroyImmediate(readback);
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            fonts.Dispose();
        }
    }

    static bool IsUniform(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        Color32 first = pixels[0];
        for (int i = 0; i < pixels.Length; i += 31)
            if (Math.Abs(pixels[i].r - first.r) > 3 || Math.Abs(pixels[i].g - first.g) > 3
                || Math.Abs(pixels[i].b - first.b) > 3) return false;
        return true;
    }

    static int DrawCanvasMeshes(Transform panel, Camera camera, RenderTexture target, StringBuilder report)
    {
        var owned = new List<Object>();
        var commands = new CommandBuffer { name = "Skill prefab UI preview" };
        int count = 0;
        try
        {
            commands.SetRenderTarget(target);
            commands.ClearRenderTarget(true, true, camera.backgroundColor);
            commands.SetViewport(new Rect(0, 0, target.width, target.height));
            commands.SetViewProjectionMatrices(camera.worldToCameraMatrix,
                GL.GetGPUProjectionMatrix(camera.projectionMatrix, false));
            // Hierarchy traversal preserves the Canvas back-to-front sibling order.
            foreach (Graphic graphic in panel.GetComponentsInChildren<Graphic>())
            {
                if (!graphic.enabled) continue;
                CanvasRenderer renderer = graphic.canvasRenderer;
                renderer.cull = false;
                graphic.SetAllDirty();
                graphic.Rebuild(CanvasUpdate.PreRender);
                if (graphic is TMP_Text text) text.ForceMeshUpdate(true, true);
                Mesh source = renderer.GetMesh();
                report.AppendLine(RelativePath(panel, graphic.transform) + " vertices="
                    + (source == null ? 0 : source.vertexCount) + " rect=" + graphic.rectTransform.rect
                    + " position=" + graphic.transform.position + " alpha=" + renderer.GetInheritedAlpha());
                if (source == null || source.vertexCount == 0) continue;
                Mesh mesh = Object.Instantiate(source);
                mesh.hideFlags = HideFlags.HideAndDontSave;
                owned.Add(mesh);
                Color tint = renderer.GetColor();
                Color32[] colors = mesh.colors32;
                for (int i = 0; i < colors.Length; i++) colors[i] = (Color)colors[i] * tint;
                mesh.colors32 = colors;
                var material = new Material(graphic.materialForRendering) { hideFlags = HideFlags.HideAndDontSave };
                owned.Add(material);
                // TMP's base Graphic.mainTexture is white; its SDF atlas lives on its material.
                // Image sprites instead supply their texture through CanvasRenderer.SetTexture.
                if (graphic is Image) material.mainTexture = graphic.mainTexture;
                report.AppendLine("  shader=" + material.shader.name + " texture="
                    + (material.mainTexture == null ? "null" : material.mainTexture.name));
                material.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
                commands.DrawMesh(mesh, graphic.rectTransform.localToWorldMatrix, material, 0, 0);
                count++;
            }
            Graphics.ExecuteCommandBuffer(commands);
            return count;
        }
        finally
        {
            commands.Release();
            foreach (Object item in owned) Object.DestroyImmediate(item);
        }
    }

    static Canvas PreparePreview(GameObject root, PreviewFonts fonts)
    {
        // World-space Canvas gives deterministic reference dimensions without relying on the Game view.
        var canvasObject = new GameObject("Skill validation canvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.hideFlags = HideFlags.HideAndDontSave;
        canvasObject.layer = 5;
        SceneManager.MoveGameObjectToScene(canvasObject, root.scene);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920f, 1080f);
        root.transform.SetParent(canvas.transform, false);
        // Non-ExecuteAlways gameplay components never run in this edit-mode preview.
        foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(component is UIBehaviour)) component.enabled = false;
        root.SetActive(true);
        root.transform.Find("Task")?.gameObject.SetActive(false);
        root.transform.Find("Hotbar")?.gameObject.SetActive(false);
        root.transform.Find("Panel").gameObject.SetActive(true);
        foreach (TMP_Text text in root.transform.Find("Panel").GetComponentsInChildren<TMP_Text>(true)) fonts.Isolate(text);
        return canvas;
    }

    static void ValidateStructure(Transform root, StringBuilder report, ref int errors)
    {
        Required<SkillPanelView>(root, "", report, ref errors);
        Required<RectTransform>(root, "Task", report, ref errors);
        Required<RectTransform>(root, "Hotbar", report, ref errors);
        Required<TMP_Text>(root, "Task/Title", report, ref errors);
        Required<TMP_Text>(root, "Task/Description", report, ref errors);
        Required<Button>(root, "Task/Open", report, ref errors);
        Required<TMP_Text>(root, "Task/Open/Label", report, ref errors);
        RectTransform body = Required<RectTransform>(root, "Panel/Body", report, ref errors);
        if (body != null && body.sizeDelta != new Vector2(1600f, 900f))
            Error(report, ref errors, "Panel/Body must retain its 1600x900 reference size.");
        string[] paths = { "Title", "Points", "Instructions", "MinorSkills/Title", "Details/Name", "Details/Level",
            "Details/Description", "Details/Stats", "Details/Next", "Details/Upgrade/Label" };
        foreach (string path in paths) Required<TMP_Text>(root, Body + path, report, ref errors);
        for (int i = 0; i < MinorSkillProgress.Count; i++)
        {
            string path = Body + "MinorSkills/Skill" + i;
            Required<Button>(root, path, report, ref errors);
            Required<Image>(root, path + "/Icon", report, ref errors);
            Required<Image>(root, path + "/Selection", report, ref errors);
            Required<TMP_Text>(root, path + "/Name", report, ref errors);
        }
        if (root.Find(Body + "Progress") != null)
            Error(report, ref errors, "The duplicated panel goal section must be removed.");
        Required<Button>(root, Body + "Details/Upgrade", report, ref errors);
        Required<Image>(root, Body + "Details/IconFrame/Icon", report, ref errors);
        Required<Button>(root, "Panel/Close", report, ref errors);
        Required<TMP_Text>(root, "Panel/Close/Label", report, ref errors);
        if (root.Find(Body + "Close") != null)
            Error(report, ref errors, "Old Close remains inside the popup body.");
        for (int i = 0; i < SkillCatalog.Count; i++)
        {
            string node = Body + "Nodes/Skill" + i;
            Required<Button>(root, node, report, ref errors);
            Required<Image>(root, node, report, ref errors);
            Required<TMP_Text>(root, node + "/Name", report, ref errors);
            Required<TMP_Text>(root, node + "/Level", report, ref errors);
            Required<RectTransform>(root, node + "/Check", report, ref errors);
            Required<Image>(root, node + "/Selection", report, ref errors);
            Image icon = Required<Image>(root, node + "/Icon", report, ref errors);
            Image hotbar = Required<Image>(root, "Hotbar/Skill" + i + "/Icon", report, ref errors);
            Required<Image>(root, "Hotbar/Skill" + i, report, ref errors);
            Required<TMP_Text>(root, "Hotbar/Skill" + i + "/Label", report, ref errors);
            Required<Image>(root, "Hotbar/Skill" + i + "/LockIcon", report, ref errors);
            Required<Image>(root, "Hotbar/Skill" + i + "/KeyHint", report, ref errors);
            Required<Image>(root, "Hotbar/Skill" + i + "/BarBackground", report, ref errors);
            Required<Image>(root, "Hotbar/Skill" + i + "/BarBackground/Fill", report, ref errors);
            if (icon != null && SpriteGuid(icon) != IconGuids[i])
                Error(report, ref errors, node + ": original skill icon reference changed or missing.");
            if (hotbar != null && SpriteGuid(hotbar) != IconGuids[i])
                Error(report, ref errors, "Hotbar/Skill" + i + ": original icon reference changed or missing.");
        }
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.font == null || text.fontSharedMaterial == null)
                Error(report, ref errors, RelativePath(root, text.transform) + ": missing font or material.");
        }
        TMP_Text points = root.Find(Body + "Points")?.GetComponent<TMP_Text>();
        if (points != null && !points.richText)
            Error(report, ref errors, "Points needs rich text for the green numeric value.");
        report.AppendLine("Structure and original skill icon references inspected.");
    }

    static T Required<T>(Transform root, string path, StringBuilder report, ref int errors) where T : Component
    {
        Transform target = string.IsNullOrEmpty(path) ? root : root.Find(path);
        T component = target != null ? target.GetComponent<T>() : null;
        if (component == null) Error(report, ref errors, "Missing " + typeof(T).Name + " at " + path);
        return component;
    }

    static string SpriteGuid(Image image) => image.sprite == null ? "" :
        AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(image.sprite));

    static void ValidateTranslations(LocalizationTable table, StringBuilder report, ref int errors)
    {
        var rows = new Dictionary<string, LocalizationTable.Entry>();
        foreach (LocalizationTable.Entry row in table.Entries)
        {
            if (row == null || string.IsNullOrEmpty(row.key)) continue;
            if (!row.key.StartsWith("skills.", StringComparison.Ordinal) && !row.key.StartsWith("minor.", StringComparison.Ordinal)
                && row.key != LocalizationKeys.PauseBack) continue;
            if (rows.ContainsKey(row.key)) { Error(report, ref errors, "Duplicate translation: " + row.key); continue; }
            rows.Add(row.key, row);
            if (row.values == null || row.values.Length != GameLanguages.Count)
            { Error(report, ref errors, row.key + " must contain all 12 locales."); continue; }
            var expected = Placeholders(row.values[0]);
            for (int language = 0; language < GameLanguages.Count; language++)
            {
                string value = row.values[language];
                if (string.IsNullOrWhiteSpace(value))
                    Error(report, ref errors, row.key + "/" + (GameLanguage)language + " is empty.");
                else if (!expected.SetEquals(Placeholders(value)))
                    Error(report, ref errors, row.key + "/" + (GameLanguage)language + " has different placeholders.");
                else
                {
                    try { string.Format(CultureInfo.InvariantCulture, table.Get(row.key, (GameLanguage)language), 2, 10); }
                    catch (FormatException) { Error(report, ref errors, row.key + "/" + (GameLanguage)language + " has invalid formatting."); }
                }
            }
        }
        string[] required = { LocalizationKeys.SkillsTitle, LocalizationKeys.SkillsPoints, LocalizationKeys.SkillsInstructions,
            LocalizationKeys.SkillsLevel, LocalizationKeys.SkillsUpgrade, LocalizationKeys.SkillsMax, LocalizationKeys.SkillsLocked,
            LocalizationKeys.SkillsNext, LocalizationKeys.SkillsStats, LocalizationKeys.SkillsWait, LocalizationKeys.SkillsAmount,
            LocalizationKeys.SkillsTaskTitle, LocalizationKeys.SkillsTaskRows, LocalizationKeys.PauseBack };
        foreach (string key in required) if (!rows.ContainsKey(key)) Error(report, ref errors, "Missing translation: " + key);
        foreach (string key in SkillCatalog.Keys)
        {
            if (!rows.ContainsKey("skills." + key + ".name")) Error(report, ref errors, "Missing skill name: " + key);
            if (!rows.ContainsKey("skills." + key + ".description")) Error(report, ref errors, "Missing skill description: " + key);
        }
        string[] minorKeys = { "minor.title", "minor.locked.name", "minor.locked.description", "minor.unlocked.status",
            "minor.unlock.title", "minor.unlock.message", "minor.unlock.close" };
        foreach (string key in minorKeys)
            if (!rows.ContainsKey(key)) Error(report, ref errors, "Missing minor translation: " + key);
        for (int i = 1; i <= MinorSkillProgress.Count; i++)
            foreach (string suffix in new[] { ".name", ".description" })
                if (!rows.ContainsKey("minor.skill" + i + suffix))
                    Error(report, ref errors, "Missing minor translation: minor.skill" + i + suffix);
        report.AppendLine("Translation rows inspected: " + rows.Count + "; locales: " + GameLanguages.Count);
    }

    static HashSet<string> Placeholders(string value)
    {
        var result = new HashSet<string>();
        foreach (Match match in Regex.Matches(value ?? "", @"(?<!\{)\{(\d+)(?:[^{}]*)\}(?!\})")) result.Add(match.Groups[1].Value);
        return result;
    }

    static void Populate(Transform root, LocalizationTable table, GameLanguage language, int selected, int level)
    {
        string Get(string key) => table.Get(key, language);
        string Format(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, Get(key), args);
        void Set(string path, string value) => root.Find(path).GetComponent<TMP_Text>().text = value;
        foreach (string path in new[] { "Level", "LevelFrame", "Stats", "Next", "StatsFrame", "StatsRule", "Upgrade" })
            root.Find(Body + "Details/" + path).gameObject.SetActive(true);
        Set(Body + "Title", Get(LocalizationKeys.SkillsTitle));
        Set(Body + "Instructions", Get(LocalizationKeys.SkillsInstructions));
        Set(Body + "Points", Format(LocalizationKeys.SkillsPoints, "<color=#2E7D32>2</color>"));
        Set(Body + "MinorSkills/Title", Get("minor.title"));
        for (int i = 0; i < MinorSkillProgress.Count; i++)
        {
            string minor = Body + "MinorSkills/Skill" + i;
            Set(minor + "/Name", Get("minor.locked.name"));
            root.Find(minor + "/Selection").gameObject.SetActive(false);
            root.Find(minor + "/Icon").GetComponent<Image>().sprite = root.Find("Hotbar/Skill0/LockIcon").GetComponent<Image>().sprite;
        }
        Set("Panel/Close/Label", Get(LocalizationKeys.PauseBack));
        for (int i = 0; i < SkillCatalog.Count; i++)
        {
            string node = Body + "Nodes/Skill" + i;
            int nodeLevel = Mathf.Min(level, SkillCatalog.MaxLevel(i));
            Set(node + "/Name", Get("skills." + SkillCatalog.Keys[i] + ".name"));
            Set(node + "/Level", Format(LocalizationKeys.SkillsLevel, nodeLevel, SkillCatalog.MaxLevel(i)));
            root.Find(node + "/Selection").gameObject.SetActive(i == selected);
            root.Find(node + "/Check").gameObject.SetActive(nodeLevel > 0);
            root.Find(node + "/Level").GetComponent<TMP_Text>().color = nodeLevel > 0
                ? new Color32(46, 111, 56, 255) : new Color32(94, 89, 77, 255);
        }
        int maximum = SkillCatalog.MaxLevel(selected);
        Image selectedIcon = root.Find(Body + "Nodes/Skill" + selected + "/Icon").GetComponent<Image>();
        Image detailIcon = root.Find(Body + "Details/IconFrame/Icon").GetComponent<Image>();
        detailIcon.sprite = selectedIcon.sprite;
        detailIcon.color = selectedIcon.color;
        root.Find(Body + "Details/Upgrade").GetComponent<Button>().interactable = level < maximum;
        Set(Body + "Details/Name", Get("skills." + SkillCatalog.Keys[selected] + ".name"));
        Set(Body + "Details/Description", Get("skills." + SkillCatalog.Keys[selected] + ".description"));
        Set(Body + "Details/Level", Format(LocalizationKeys.SkillsLevel, level, maximum));
        Set(Body + "Details/Next", level >= maximum ? Get(LocalizationKeys.SkillsMax)
            : level == 0 ? Get(LocalizationKeys.SkillsLocked) + " / " + Get(LocalizationKeys.SkillsNext)
            : Get(LocalizationKeys.SkillsNext) + " (" + level + " \u2192 " + (level + 1) + ")");
        Set(Body + "Details/Upgrade/Label", Get(level >= maximum ? LocalizationKeys.SkillsMax : LocalizationKeys.SkillsUpgrade));
        string Stat(int current, int next) => level >= maximum ? current.ToString(CultureInfo.InvariantCulture)
            : (level == 0 ? "\u2014" : current.ToString(CultureInfo.InvariantCulture))
                + " \u2192 <color=#2E7D32>" + next + "</color>";
        string cooldown = Stat(SkillCatalog.Cooldown(selected, level), SkillCatalog.Cooldown(selected, level + 1));
        string effect = Stat(SkillCatalog.Effect(selected, level), SkillCatalog.Effect(selected, level + 1));
        string stats = selected >= 2 ? Format(LocalizationKeys.SkillsStats, cooldown, effect).Replace(" / ", "\n")
            : Format(LocalizationKeys.SkillsWait, cooldown);
        if (selected == 0) stats += "\n" + Format(LocalizationKeys.SkillsAmount, effect);
        Set(Body + "Details/Stats", stats);
    }

    static void PopulateMinor(Transform root, LocalizationTable table, GameLanguage language, int selected, bool unlocked)
    {
        Populate(root, table, language, (int)CardSkill.Sort, 0);
        void Set(string path, string key) => root.Find(path).GetComponent<TMP_Text>().text = table.Get(key, language);
        foreach (string path in new[] { "Level", "LevelFrame", "Stats", "Next", "StatsFrame", "StatsRule", "Upgrade" })
            root.Find(Body + "Details/" + path).gameObject.SetActive(false);
        for (int i = 0; i < SkillCatalog.Count; i++)
            root.Find(Body + "Nodes/Skill" + i + "/Selection").gameObject.SetActive(false);
        Sprite locked = root.Find("Hotbar/Skill0/LockIcon").GetComponent<Image>().sprite;
        SkillPanelView view = root.GetComponent<SkillPanelView>();
        Sprite icon = unlocked ? view.GetMinorSkillIcon(selected) : locked;
        for (int i = 0; i < MinorSkillProgress.Count; i++)
        {
            string path = Body + "MinorSkills/Skill" + i;
            Set(path + "/Name", unlocked ? "minor.skill" + (i + 1) + ".name" : "minor.locked.name");
            root.Find(path + "/Selection").gameObject.SetActive(i == selected);
            root.Find(path + "/Icon").GetComponent<Image>().sprite = unlocked ? view.GetMinorSkillIcon(i) : locked;
        }
        string key = "minor.skill" + (selected + 1);
        Set(Body + "Details/Name", unlocked ? key + ".name" : "minor.locked.name");
        Set(Body + "Details/Description", unlocked ? key + ".description" : "minor.locked.description");
        root.Find(Body + "Details/IconFrame/Icon").GetComponent<Image>().sprite = icon;
        root.Find(Body + "Details/Next").gameObject.SetActive(unlocked);
        Set(Body + "Details/Next", "minor.unlocked.status");
    }

    static string RelativePath(Transform root, Transform target) => AnimationUtility.CalculateTransformPath(target, root);
    static void Error(StringBuilder report, ref int errors, string message) { errors++; report.AppendLine("ERROR " + message); }

    // Dynamic TMP atlases can add glyphs during layout. Clone fonts, fallback graphs, materials and
    // atlases so validation cannot dirty or write to authored font assets in the open project.
    sealed class PreviewFonts : IDisposable
    {
        readonly Dictionary<TMP_FontAsset, TMP_FontAsset> _fonts = new Dictionary<TMP_FontAsset, TMP_FontAsset>();
        readonly List<Object> _owned = new List<Object>();
        T Copy<T>(T source) where T : Object
        {
            if (source == null) return null;
            T copy = Object.Instantiate(source);
            copy.hideFlags = HideFlags.HideAndDontSave;
            _owned.Add(copy);
            return copy;
        }
        TMP_FontAsset Font(TMP_FontAsset source)
        {
            if (source == null) return null;
            if (_fonts.TryGetValue(source, out TMP_FontAsset existing)) return existing;
            TMP_FontAsset font = Copy(source);
            _fonts.Add(source, font);
            Texture2D[] sourceAtlases = source.atlasTextures;
            var atlases = new Texture2D[sourceAtlases.Length];
            for (int i = 0; i < atlases.Length; i++) atlases[i] = Copy(sourceAtlases[i]);
            font.atlasTextures = atlases;
            font.material = Copy(source.material);
            if (font.material != null && atlases.Length > 0) font.material.mainTexture = atlases[0];
            font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (source.fallbackFontAssetTable != null)
                foreach (TMP_FontAsset fallback in source.fallbackFontAssetTable) font.fallbackFontAssetTable.Add(Font(fallback));
            for (int i = 0; i < font.fontWeightTable.Length; i++)
            {
                TMP_FontWeightPair pair = font.fontWeightTable[i];
                pair.regularTypeface = Font(pair.regularTypeface);
                pair.italicTypeface = Font(pair.italicTypeface);
                font.fontWeightTable[i] = pair;
            }
            return font;
        }
        public void Isolate(TMP_Text text)
        {
            Material sourceMaterial = text.fontSharedMaterial;
            text.font = Font(text.font);
            Material material = Copy(sourceMaterial);
            material.mainTexture = text.font.atlasTextures[0];
            text.fontSharedMaterial = material;
        }
        public void Dispose()
        {
            // TMP may have allocated extra atlases while laying out CJK translations.
            foreach (TMP_FontAsset font in _fonts.Values)
                if (font != null)
                    foreach (Texture2D atlas in font.atlasTextures)
                        if (atlas != null && !EditorUtility.IsPersistent(atlas) && !_owned.Contains(atlas)) _owned.Add(atlas);
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
        }
    }
}
