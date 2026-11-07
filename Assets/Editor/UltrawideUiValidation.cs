using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
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

/// <summary>Checks authored menus against actual offscreen camera sizes; never runs save/apply/quit actions.</summary>
public static class UltrawideUiValidation
{
    const string ReportPath = "Temp/ultrawide-ui-validation.txt";
    static readonly Vector2Int[] Sizes = {
        new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160),
        new Vector2Int(2560, 1080), new Vector2Int(3440, 1440), new Vector2Int(5120, 1440),
        new Vector2Int(1280, 720), new Vector2Int(1920, 1200), new Vector2Int(1280, 1024)
    };
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    [MenuItem("TCG Card Chaos/Validation/Ultrawide UI (Isolated)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run outside Play Mode with MainScene open.");
        Scene main = SceneManager.GetSceneByPath("Assets/Scenes/MainScene.unity");
        if (!main.IsValid() || !main.isLoaded || main.isDirty)
            throw new InvalidOperationException("Open the saved MainScene before validation.");
        Scene menu = default, preview = default;
        var report = new StringBuilder("UI aspect-ratio validation; no player saves/settings are written.\n");
        int checks = 0;
        try
        {
            menu = EditorSceneManager.OpenPreviewScene("Assets/Scenes/MenuScene.unity");
            preview = EditorSceneManager.NewPreviewScene();
            ValidateCanvas(FindRoot(menu, "MainMenuCanvas"), preview, false, report, ref checks);
            ValidateCanvas(FindRoot(main, "InGamePauseCanvas"), preview, true, report, ref checks);
            foreach (Scene scene in new[] { menu, main })
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (CanvasScaler scaler in root.GetComponentsInChildren<CanvasScaler>(true))
                    {
                        Check(scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand,
                            "All authored screen canvases fit both axes: " + scaler.name, report, ref checks);
                    }
            GameObject runtime = RuntimeOverlayCanvasFactory.Create(null, "Runtime UI validation", 0).gameObject;
            SceneManager.MoveGameObjectToScene(runtime, preview);
            Check(runtime.GetComponent<CanvasScaler>().screenMatchMode == CanvasScaler.ScreenMatchMode.Expand,
                "Runtime HUD/preview canvases use the same fitting policy", report, ref checks);
            Object.DestroyImmediate(runtime);
            report.AppendLine("PASS " + checks + " checks. Original scenes, player saves and settings unchanged.");
        }
        catch (Exception error) { report.AppendLine("FAIL " + error); Debug.LogException(error); }
        finally
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            if (menu.IsValid()) EditorSceneManager.ClosePreviewScene(menu);
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ReportPath, report.ToString());
        }
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root;
        throw new InvalidOperationException("Missing " + name + " in " + scene.name);
    }

    static void Check(bool condition, string label, StringBuilder report, ref int checks)
    {
        if (!condition) throw new InvalidOperationException(label);
        checks++; report.AppendLine("PASS " + label);
    }

    static void ValidateCanvas(GameObject source, Scene scene, bool pause, StringBuilder report, ref int checks)
    {
        GameObject clone = Object.Instantiate(source);
        clone.name = source.name;
        clone.hideFlags = HideFlags.HideAndDontSave;
        SceneManager.MoveGameObjectToScene(clone, scene);
        GameObject cameraObject = new GameObject("UI validation camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var fonts = new PreviewFonts();
        try
        {
            clone.SetActive(true);
            foreach (MonoBehaviour component in clone.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(component is UIBehaviour)) component.enabled = false;
            SettingsPanelView settings = SettingsPanelView.Ensure(clone.transform);
            settings.Show();
            Toggle dogToggle = settings.transform.Find("Panel/Content/Row_ShowDog/Toggle").GetComponent<Toggle>();
            bool savedDog = GameSettings.Current.showDog;
            Check(dogToggle.isOn == savedDog, "Show Dog displays the saved preference", report, ref checks);
            dogToggle.isOn = !savedDog;
            typeof(SettingsPanelView).GetMethod("ReadDraftFromControls", Flags).Invoke(settings, null);
            var draft = (GameSettings.Snapshot)typeof(SettingsPanelView).GetField("_draft", Flags).GetValue(settings);
            Check(draft.showDog == !savedDog && GameSettings.Current.showDog == savedDog,
                "Show Dog updates only the draft before Save", report, ref checks);
            typeof(SettingsPanelView).GetMethod("CancelAndHide", Flags).Invoke(settings, null);
            settings.Show();
            Check(dogToggle.isOn == savedDog, "Cancel and reopen restores the saved Show Dog choice", report, ref checks);
            settings.Hide();
            if (pause)
            {
                var save = SaveGamePanelView.CreateFromLoadPanel(clone.GetComponentInChildren<LoadGamePanelView>(true), clone.transform);
                typeof(SaveGamePanelView).GetMethod("EnsureCancelLabel", Flags).Invoke(save, null);
            }
            else
            {
                // Use the same authored-button positioning as the live menu, including Continue.
                typeof(MainMenuView).GetMethod("ApplyMenuLayout", Flags).Invoke(clone.GetComponent<MainMenuView>(), null);
                clone.transform.Find("Button_Continue").gameObject.SetActive(true);
            }
            foreach (MonoBehaviour component in clone.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(component is UIBehaviour)) component.enabled = false;
            foreach (var load in clone.GetComponentsInChildren<LoadGamePanelView>(true))
                typeof(LoadGamePanelView).GetMethod("EnsureCancelLabel", Flags).Invoke(load, null);
            foreach (TMP_Text text in clone.GetComponentsInChildren<TMP_Text>(true)) fonts.Isolate(text);
            foreach (LocalizedText label in clone.GetComponentsInChildren<LocalizedText>(true)) label.Refresh();
            Canvas canvas = clone.GetComponent<Canvas>();
            CanvasScaler scaler = clone.GetComponent<CanvasScaler>();
            Check(scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand, source.name + " uses Expand", report, ref checks);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false; camera.cameraType = CameraType.Preview; camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.orthographic = true; camera.orthographicSize = 540;
            camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
            camera.transform.position = new Vector3(0, 0, -1000);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(12, 16, 32, 255);
            camera.cullingMask = 1 << 5; camera.allowHDR = false; camera.allowMSAA = false;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1000;
            string[] panels = pause
                ? new[] { "Panel_Pause", "Panel_Settings", "Panel_SaveGame", "Panel_LoadGame" }
                : new[] { "", "Panel_Settings", "Panel_LoadGame" };
            foreach (Vector2Int size in Sizes)
            {
                var target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32) { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    target.Create(); camera.targetTexture = target; camera.aspect = (float)size.x / size.y;
                    // Several equal-height resolutions run in one editor tick. Re-enable the scaler
                    // so its native rect invalidation also reaches stretched child backgrounds.
                    scaler.enabled = false;
                    scaler.enabled = true;
                    Canvas.ForceUpdateCanvases();
                    typeof(CanvasScaler).GetMethod("Handle", Flags).Invoke(scaler, null);
                    ((RectTransform)clone.transform).ForceUpdateRectTransforms();
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)clone.transform);
                    Canvas.ForceUpdateCanvases();
                    report.AppendLine(source.name + " " + size + " display=" + canvas.renderingDisplaySize + " scale=" + canvas.scaleFactor + " canvas=" + ((RectTransform)canvas.transform).rect.size);
                    Check((canvas.renderingDisplaySize - (Vector2)size).sqrMagnitude < 1,
                        "Native Canvas sees the requested render resolution", report, ref checks);
                    Check(((RectTransform)canvas.transform).rect.width >= 1919 && ((RectTransform)canvas.transform).rect.height >= 1079,
                        "Entire reference layout fits at " + size, report, ref checks);
                    foreach (string panel in panels)
                    {
                        foreach (Transform child in clone.transform)
                            if (child.name.StartsWith("Panel_") && child.name != "Panel_Roadmap") child.gameObject.SetActive(false);
                        Transform visible = string.IsNullOrEmpty(panel) ? clone.transform : clone.transform.Find(panel);
                        if (visible == null) throw new InvalidOperationException("Missing panel " + panel);
                        visible.gameObject.SetActive(true);
                        foreach (Transform child in visible)
                            if (child.name.Contains("Confirm")) child.gameObject.SetActive(false);
                        Canvas.ForceUpdateCanvases();
                        foreach (TMP_Text text in visible.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate(true, true);
                        Canvas.ForceUpdateCanvases();
                        int controls = 0;
                        foreach (Selectable control in visible.GetComponentsInChildren<Selectable>())
                        {
                            if (!control.isActiveAndEnabled) continue;
                            ScrollRect scroll = control.GetComponentInParent<ScrollRect>();
                            if (scroll && scroll.content && control.transform.IsChildOf(scroll.content)) continue;
                            Check(Inside(control.transform as RectTransform, camera, size),
                                source.name + "/" + panel + "/" + control.name + " fully onscreen " + size, report, ref checks);
                            controls++;
                        }
                        Check(controls > 0, "Visible controls checked for " + panel, report, ref checks);
                        if (panel == "Panel_Settings")
                        {
                            RectTransform body = visible.Find("Panel") as RectTransform;
                            foreach (Transform row in visible.Find("Panel/Content"))
                            {
                                if (!row.name.StartsWith("Row_")) continue;
                                var corners = new Vector3[4]; ((RectTransform)row).GetWorldCorners(corners);
                                foreach (Vector3 corner in corners)
                                    Check(body.rect.Contains(body.InverseTransformPoint(corner)),
                                        row.name + " stays inside settings panel " + size, report, ref checks);
                            }
                        }
                        foreach (Transform child in visible)
                        {
                            if (!child.name.Contains("Confirm")) continue;
                            child.gameObject.SetActive(true);
                            Canvas.ForceUpdateCanvases();
                            foreach (Selectable control in child.GetComponentsInChildren<Selectable>())
                                Check(Inside(control.transform as RectTransform, camera, size),
                                    child.name + "/" + control.name + " fully onscreen " + size, report, ref checks);
                            child.gameObject.SetActive(false);
                        }
                        if (size.x == 5120 && panel == "Panel_Settings")
                        {
                            // Prove this catches the original failure, then restore the production policy.
                            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                            scaler.matchWidthOrHeight = .5f;
                            typeof(CanvasScaler).GetMethod("Handle", Flags).Invoke(scaler, null);
                            Canvas.ForceUpdateCanvases();
                            Check(!Inside(visible.Find("Button_Save") as RectTransform, camera, size),
                                "Original scaling reproduces the clipped Save button at 5120x1440", report, ref checks);
                            RuntimeOverlayCanvasFactory.ConfigureScreenScaling(scaler);
                            typeof(CanvasScaler).GetMethod("Handle", Flags).Invoke(scaler, null);
                            Canvas.ForceUpdateCanvases();
                        }
                        // Render the reported problem and the unchanged standard layout for visual review.
                        if ((size.x == 5120 || size.x == 1920 && size.y == 1080) && panel != "Panel_LoadGame")
                            Render(canvas, camera, target, "Temp/ui-" + (pause ? "pause-" : "menu-") + (panel == "" ? "home" : panel) + "-" + size.x + "x" + size.y + ".png");
                    }
                }
                finally { camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); }
            }
        }
        finally { Object.DestroyImmediate(clone); Object.DestroyImmediate(cameraObject); fonts.Dispose(); }
    }

    static bool Inside(RectTransform rect, Camera camera, Vector2Int size)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        foreach (Vector3 corner in corners)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
            if (point.x < -1 || point.y < -1 || point.x > size.x + 1 || point.y > size.y + 1) return false;
        }
        return true;
    }

    static void Render(Canvas canvas, Camera camera, RenderTexture target, string path)
    {
        // Flush RectTransform changes after switching the camera target in the same editor tick.
        ((RectTransform)canvas.transform).ForceUpdateRectTransforms();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
        Canvas.ForceUpdateCanvases();
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
        else camera.Render();
        RenderTexture previous = RenderTexture.active;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
            // Reuse the established editor renderer if a prefab preview has no native canvas submission.
            if ((bool)typeof(SkillPanelValidation).GetMethod("IsUniform", Flags).Invoke(null, new object[] { pixels }))
            {
                typeof(SkillPanelValidation).GetMethod("DrawCanvasMeshes", Flags).Invoke(null, new object[] { canvas.transform, camera, target, new StringBuilder() });
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
            }
            Directory.CreateDirectory("Temp"); File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; Object.DestroyImmediate(pixels); }
    }

    // Rendering may populate TMP's dynamic atlases; only use disposable copies here.
    sealed class PreviewFonts : IDisposable
    {
        readonly Dictionary<TMP_FontAsset, TMP_FontAsset> fonts = new Dictionary<TMP_FontAsset, TMP_FontAsset>();
        readonly List<Object> owned = new List<Object>();
        T Copy<T>(T source) where T : Object
        {
            if (!source) return null;
            T copy = Object.Instantiate(source); copy.hideFlags = HideFlags.HideAndDontSave; owned.Add(copy); return copy;
        }
        TMP_FontAsset Font(TMP_FontAsset source)
        {
            if (!source) return null;
            if (fonts.TryGetValue(source, out var existing)) return existing;
            TMP_FontAsset font = Copy(source); fonts[source] = font;
            var atlases = new Texture2D[source.atlasTextures.Length];
            for (int i = 0; i < atlases.Length; i++) atlases[i] = Copy(source.atlasTextures[i]);
            font.atlasTextures = atlases; font.material = Copy(source.material);
            if (font.material && atlases.Length > 0) font.material.mainTexture = atlases[0];
            font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (source.fallbackFontAssetTable != null)
                foreach (var fallback in source.fallbackFontAssetTable) font.fallbackFontAssetTable.Add(Font(fallback));
            for (int i = 0; i < font.fontWeightTable.Length; i++)
            {
                var pair = font.fontWeightTable[i]; pair.regularTypeface = Font(pair.regularTypeface); pair.italicTypeface = Font(pair.italicTypeface); font.fontWeightTable[i] = pair;
            }
            return font;
        }
        public void Isolate(TMP_Text text)
        {
            Material material = Copy(text.fontSharedMaterial); text.font = Font(text.font);
            if (!text.font || !material) return;
            material.mainTexture = text.font.atlasTextures[0]; text.fontSharedMaterial = material;
        }
        public void Dispose()
        {
            foreach (var font in fonts.Values)
                if (font) foreach (var atlas in font.atlasTextures)
                    if (atlas && !EditorUtility.IsPersistent(atlas) && !owned.Contains(atlas)) owned.Add(atlas);
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.DestroyImmediate(owned[i]);
        }
    }
}
