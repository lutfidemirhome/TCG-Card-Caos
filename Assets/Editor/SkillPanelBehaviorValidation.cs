using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Runs real popup listeners against temporary progress, then restores all touched state.</summary>
public static class SkillPanelBehaviorValidation
{
    const string PrefabPath = "Assets/Resources/UI/Skills/SkillUI.prefab";
    const string ReportPath = "Temp/skill-panel-behavior-validation.txt";
    const string Body = "Panel/Body/";
    const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags InstanceMethods = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [MenuItem("TCG Card Chaos/UI/Validate Skill Panel Behavior")]
    public static void Validate()
    {
        var report = new StringBuilder("Skill panel isolated behavior validation\n");
        report.AppendLine("Edit mode only. Real prefab listeners; temporary progress; no save manager or world scans.");
        int checks = 0, errors = 0;
        GameObject root = null;
        SkillPanelView view = null;
        StateSnapshot state = null;
        var fonts = new PreviewFonts();
        float timeScale = Time.timeScale;
        bool audioPaused = AudioListener.pause, cursorVisible = Cursor.visible;
        CursorLockMode cursorLock = Cursor.lockState;
        bool touchedGlobals = false;
        void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(message);
            report.AppendLine("PASS " + message);
        }

        try
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode
                || GameSaveManager.Instance != null || SkillPanelView.Instance != null || MinorSkillUnlockView.Instance != null
                || GameSceneLoader.IsLoading || PlayerCardHand.Instance != null)
                throw new InvalidOperationException("Run outside Play Mode with no live skill view, player hand, save manager or scene load.");

            // Resolve every field and record its original contents before changing anything.
            state = new StateSnapshot();
            state.Capture(typeof(SkillProgress), "Levels", "Completed", "Cooldowns", "Active",
                "<Ready>k__BackingField", "<Revision>k__BackingField", "_testSkillsEnabled",
                "_testPoints", "TestLevels", "TestCooldowns", "TestActive", "_testAutoshelfContext");
            state.Capture(typeof(MinorSkillProgress), "_ownedKeys", "_unlockedSkills", "<Ready>k__BackingField", "Changed", "Unlocked");
            state.Capture(typeof(MinorSkillUnlockView), "<Instance>k__BackingField", "_closedFrame");
            state.Capture(typeof(GameSaveDirtyTracker), "<IsDirty>k__BackingField", "<Revision>k__BackingField");
            state.Capture(typeof(GameSaveManager), "_milestoneQueued");
            state.Capture(typeof(SkillPanelView), "<Instance>k__BackingField", "_closedFrame");
            state.Capture(typeof(CardInstancedRenderManager), "<IsGameplayReady>k__BackingField");
            state.Capture(typeof(WelcomePopupView), "<IsWaitingForStart>k__BackingField");
            state.Capture(typeof(GamePause), "<IsPaused>k__BackingField");
            state.Capture(typeof(Localization), "_table", "_currentLanguage", "_initialized", "LanguageChanged");
            state.Capture(typeof(UiMenuFont), "_font");
            state.Capture(typeof(EventSystem), "m_EventSystems");
            touchedGlobals = true;

            root = PrefabUtility.LoadPrefabContents(PrefabPath);
            var canvasObject = new GameObject("Skill behavior preview canvas", typeof(RectTransform));
            canvasObject.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(canvasObject, root.scene);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920, 1080);
            root.transform.SetParent(canvas.transform, false);

            var eventsObject = new GameObject("Skill behavior preview events");
            eventsObject.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(eventsObject, root.scene);
            EventSystem events = eventsObject.AddComponent<EventSystem>();
            // Non-ExecuteAlways EventSystem may not receive OnEnable in edit mode. Register only
            // this preview instance, so Awake's UiEventSystem.Ensure cannot create a scene object.
            var systems = (IList)Field(typeof(EventSystem), "m_EventSystems").GetValue(null);
            if (!systems.Contains(events)) systems.Add(events);
            EventSystem.current = events;
            Check(EventSystem.current == events, "Preview EventSystem is isolated from the open scene");

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) fonts.Isolate(text);
            foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(component is UIBehaviour)) component.enabled = false;
            root.SetActive(true);

            var levels = (int[])Field(typeof(SkillProgress), "Levels").GetValue(null);
            var completed = (HashSet<string>)Field(typeof(SkillProgress), "Completed").GetValue(null);
            Array.Clear(levels, 0, levels.Length);
            foreach (string name in new[] { "Cooldowns", "Active" })
            {
                var values = (float[])Field(typeof(SkillProgress), name).GetValue(null);
                Array.Clear(values, 0, values.Length);
            }
            completed.Clear();
            for (int i = 0; i < 3; i++) completed.Add("isolated-preview-row-" + i);
            Set(typeof(SkillProgress), "<Ready>k__BackingField", true);
            Set(typeof(SkillProgress), "_testSkillsEnabled", false);
            Set(typeof(CardInstancedRenderManager), "<IsGameplayReady>k__BackingField", true);
            Set(typeof(WelcomePopupView), "<IsWaitingForStart>k__BackingField", false);
            Set(typeof(GameSaveManager), "_milestoneQueued", false);
            Set(typeof(MinorSkillProgress), "Changed", null);
            Set(typeof(MinorSkillProgress), "Unlocked", null);
            Set(typeof(MinorSkillUnlockView), "_closedFrame", -1);
            MinorSkillProgress.Restore(null);
            GamePause.SetPaused(false);
            Check(SkillProgress.Points == 2, "Three completed rows provide two temporary upgrade points");

            view = root.GetComponent<SkillPanelView>();
            if (view == null) throw new InvalidOperationException("Prefab is missing SkillPanelView.");
            MethodInfo awake = typeof(SkillPanelView).GetMethod("Awake", InstanceMethods);
            if (awake == null) throw new MissingMethodException("SkillPanelView.Awake");
            awake.Invoke(view, null);
            // Edit-mode listener validation needs no visual coroutines. Initialize each dynamically
            // attached feedback component before disabling it, preserving the authored scale.
            MethodInfo feedbackAwake = typeof(SkillPanelButtonFeedback).GetMethod("Awake", InstanceMethods);
            foreach (var feedback in root.GetComponentsInChildren<SkillPanelButtonFeedback>(true))
            {
                feedbackAwake.Invoke(feedback, null);
                feedback.enabled = false;
            }
            CardSkillController controller = root.GetComponent<CardSkillController>();
            if (controller != null) controller.enabled = false;
            Check(SkillPanelView.Instance == view && !view.IsOpen, "Awake binds the authored prefab and starts closed");
            bool openCursorVisible = Cursor.visible;
            CursorLockMode openCursorLock = Cursor.lockState;
            view.Open();
            Check(view.IsOpen && GamePause.IsPaused && Time.timeScale == 0f && AudioListener.pause,
                "Open displays the popup and pauses gameplay, time and audio");
            Check(Cursor.lockState == CursorLockMode.None && Cursor.visible && SkillPanelView.ConsumesPauseInput,
                "Open releases the cursor and consumes pause input");

            Button At(string path) => root.transform.Find(path).GetComponent<Button>();
            TMP_Text Text(string path) => root.transform.Find(path).GetComponent<TMP_Text>();
            Image ImageAt(string path) => root.transform.Find(path).GetComponent<Image>();
            Button upgrade = At(Body + "Details/Upgrade");
            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                At(Body + "Nodes/Skill" + i).onClick.Invoke();
                Check(Text(Body + "Details/Name").text == Localization.Get("skills." + SkillCatalog.Keys[i] + ".name")
                    && ImageAt(Body + "Details/IconFrame/Icon").sprite == ImageAt(Body + "Nodes/Skill" + i + "/Icon").sprite,
                    "Selection listener updates the name and original icon for skill " + i);
                for (int j = 0; j < SkillCatalog.Count; j++)
                    Check(root.transform.Find(Body + "Nodes/Skill" + j + "/Selection").gameObject.activeSelf == (i == j),
                        "Skill " + i + " selection highlight " + j + " matches");
                Check(upgrade.interactable, "Skill " + i + " can be upgraded with available points");
            }

            int pointsBeforeMinor = SkillProgress.Points;
            ulong dirtyBeforeMinor = GameSaveDirtyTracker.Revision;
            string[] majorDetails = { "Level", "LevelFrame", "Stats", "Next", "StatsFrame", "StatsRule", "Upgrade" };
            for (int i = 0; i < MinorSkillProgress.Count; i++)
            {
                At(Body + "MinorSkills/Skill" + i).onClick.Invoke();
                Check(Text(Body + "Details/Name").text == Localization.Get("minor.locked.name")
                    && Text(Body + "Details/Description").text == Localization.Get("minor.locked.description")
                    && ImageAt(Body + "Details/IconFrame/Icon").sprite == ImageAt("Hotbar/Skill0/LockIcon").sprite,
                    "Locked minor " + i + " displays its lock and localized description");
                foreach (string path in majorDetails)
                    Check(!root.transform.Find(Body + "Details/" + path).gameObject.activeSelf,
                        "Locked minor hides major-only detail " + path);
                for (int j = 0; j < MinorSkillProgress.Count; j++)
                    Check(root.transform.Find(Body + "MinorSkills/Skill" + j + "/Selection").gameObject.activeSelf == (i == j),
                        "Minor " + i + " selection highlight " + j + " matches");
                for (int j = 0; j < SkillCatalog.Count; j++)
                    Check(!root.transform.Find(Body + "Nodes/Skill" + j + "/Selection").gameObject.activeSelf,
                        "Minor selection clears major highlight " + j);
                upgrade.onClick.Invoke(); // Explicitly bypass Button.interactable to exercise the listener's guard.
                Check(SkillProgress.Points == pointsBeforeMinor && GameSaveDirtyTracker.Revision == dirtyBeforeMinor,
                    "Minor " + i + " cannot spend a major upgrade point or dirty the save");
            }
            MinorSkillProgress.Restore(new MinorSkillSaveRecord { unlockedSkillsMask = 1 });
            At(Body + "MinorSkills/Skill0").onClick.Invoke();
            Check(Text(Body + "Details/Name").text == Localization.Get("minor.skill1.name")
                && Text(Body + "Details/Description").text == Localization.Get("minor.skill1.description")
                && Text(Body + "Details/Next").text == Localization.Get("minor.unlocked.status")
                && ImageAt(Body + "Details/IconFrame/Icon").sprite == view.GetMinorSkillIcon(0)
                && view.GetMinorSkillIcon(0) != null,
                "Unlocked minor displays its permanent ability name, description and dedicated icon");
            Check(!upgrade.gameObject.activeSelf && !upgrade.interactable && SkillProgress.Points == pointsBeforeMinor,
                "Unlocked minor still has no major upgrade control or point cost");
            Check(root.transform.Find("Hotbar").childCount == SkillCatalog.Count,
                "Minor abilities do not add slots to the 1–5 hotbar");
            At(Body + "Nodes/Skill1").onClick.Invoke();
            foreach (string path in majorDetails)
                Check(root.transform.Find(Body + "Details/" + path).gameObject.activeSelf,
                    "Switching back to Sort restores detail " + path);
            Check(Text(Body + "Details/Name").text == Localization.Get("skills.sort.name") && upgrade.interactable,
                "Returning to a major skill restores its description and upgrade availability");

            At(Body + "Nodes/Skill0").onClick.Invoke();
            int revision = SkillProgress.Revision;
            ulong dirtyRevision = GameSaveDirtyTracker.Revision;
            Check(upgrade.interactable, "Upgrade button is enabled before a real upgrade");
            upgrade.onClick.Invoke();
            Check(SkillProgress.Level(0) == 1 && SkillProgress.Points == 1 && SkillProgress.Revision == revision + 1,
                "Real upgrade listener spends exactly one point and increments the selected level");
            Check(GameSaveDirtyTracker.IsDirty && GameSaveDirtyTracker.Revision == unchecked(dirtyRevision + 1)
                && (bool)Field(typeof(GameSaveManager), "_milestoneQueued").GetValue(null),
                "Upgrade preserves dirty tracking and queues a milestone without creating a save manager");
            Check(Text(Body + "Details/Level").text == Localization.Format(LocalizationKeys.SkillsLevel, 1, SkillCatalog.MaxLevel(0))
                && Text(Body + "Points").text.Contains("<color=#2E7D32>1</color>"),
                "Upgrade refreshes the level and green point count");

            completed.Clear();
            view.Refresh();
            Check(SkillProgress.Points == 0 && !upgrade.interactable, "Zero points disable the upgrade button");
            upgrade.OnPointerClick(new PointerEventData(events) { button = PointerEventData.InputButton.Left });
            Check(SkillProgress.Level(0) == 1, "A pointer click on the disabled button cannot upgrade");

            for (int i = 0; i < 509; i++) completed.Add("isolated-preview-row-" + i);
            levels[0] = SkillCatalog.MaxLevel(0);
            view.Refresh();
            Check(SkillProgress.Points > 0 && !upgrade.interactable
                && Text(Body + "Details/Upgrade/Label").text == Localization.Get(LocalizationKeys.SkillsMax),
                "Maximum level disables upgrades even with remaining points");
            upgrade.OnPointerClick(new PointerEventData(events) { button = PointerEventData.InputButton.Left });
            Check(SkillProgress.Level(0) == SkillCatalog.MaxLevel(0), "A pointer click cannot exceed maximum level");

            string savedBeforeTest = JsonUtility.ToJson(SkillProgress.Capture());
            dirtyRevision = GameSaveDirtyTracker.Revision;
            SkillProgress.EnableSkillPointsForTesting();
            Check(SkillProgress.Points == 100, "Manual skill testing starts with 100 points");
            int testLevel = SkillProgress.Level(1);
            Check(SkillProgress.Upgrade(1) && SkillProgress.Level(1) == testLevel + 1 && SkillProgress.Points == 99,
                "Manual testing upgrades the chosen skill and spends one test point");
            Check(JsonUtility.ToJson(SkillProgress.Capture()) == savedBeforeTest
                && GameSaveDirtyTracker.Revision == dirtyRevision,
                "Test upgrades leave saved progress and dirty tracking unchanged");
            Check(!SkillProgress.Upgrade(0), "Test upgrades respect the maximum level");
            SkillProgress.EnableSkillPointsForTesting();
            Check(SkillProgress.Points == 100 && SkillProgress.Level(1) == testLevel,
                "Repeated test shortcut restores saved levels and replenishes 100 points");

            At("Panel/Close").onClick.Invoke();
            Check(!view.IsOpen && !GamePause.IsPaused && Time.timeScale == 1f && !AudioListener.pause,
                "Close button hides the popup and resumes gameplay, time and audio");
            Check(Cursor.lockState == openCursorLock && Cursor.visible == openCursorVisible,
                "Close restores the cursor state captured by Open");
            Check(SkillPanelView.ConsumesPauseInput, "Closing consumes Escape for the current frame");
            Check(GameSaveManager.Instance == null, "No save manager was created during validation");
        }
        catch (Exception exception)
        {
            errors++;
            report.AppendLine("ERROR " + (exception is TargetInvocationException && exception.InnerException != null
                ? exception.InnerException.ToString() : exception.ToString()));
        }
        finally
        {
            // Cleanup may invoke OnDisable/Close; restore globals only after disposing preview objects.
            try { if (view != null) view.Close(); }
            catch (Exception exception) { errors++; report.AppendLine("ERROR closing preview: " + exception); }
            try { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
            catch (Exception exception) { errors++; report.AppendLine("ERROR unloading preview: " + exception); }
            try { fonts.Dispose(); }
            catch (Exception exception) { errors++; report.AppendLine("ERROR disposing fonts: " + exception); }
            if (touchedGlobals)
            {
                errors += state.Restore(report);
                Time.timeScale = timeScale;
                AudioListener.pause = audioPaused;
                Cursor.lockState = cursorLock;
                Cursor.visible = cursorVisible;
                if (Time.timeScale != timeScale || AudioListener.pause != audioPaused
                    || Cursor.lockState != cursorLock || Cursor.visible != cursorVisible)
                { errors++; report.AppendLine("ERROR restoring original time/audio/cursor state."); }
                else report.AppendLine("PASS Original time, audio and cursor state restored");
            }
            report.AppendLine("Checks: " + checks + "; errors: " + errors);
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ReportPath, report.ToString(), new UTF8Encoding(false));
        }
        string result = "[Skills] Behavior validation: " + checks + " checks, " + errors
            + " errors. Report: " + Path.GetFullPath(ReportPath);
        if (errors == 0) Debug.Log(result); else Debug.LogError(result);
    }

    static FieldInfo Field(Type type, string name) => type.GetField(name, StaticFields)
        ?? throw new MissingFieldException(type.FullName, name);
    static void Set(Type type, string name, object value) => Field(type, name).SetValue(null, value);

    sealed class StateSnapshot
    {
        readonly List<Action> _restore = new List<Action>();
        public void Capture(Type type, params string[] names)
        {
            foreach (string name in names)
            {
                FieldInfo field = Field(type, name);
                object original = field.GetValue(null);
                if (original is Array array)
                {
                    Array copy = (Array)array.Clone();
                    _restore.Add(() => Array.Copy(copy, array, copy.Length));
                }
                else if (original is HashSet<string> set)
                {
                    var copy = new List<string>(set);
                    _restore.Add(() => { set.Clear(); foreach (string value in copy) set.Add(value); });
                }
                else if (original is IList list)
                {
                    var copy = new object[list.Count];
                    list.CopyTo(copy, 0);
                    _restore.Add(() => { list.Clear(); foreach (object value in copy) list.Add(value); });
                }
                else _restore.Add(() => field.SetValue(null, original));
            }
        }
        public int Restore(StringBuilder report)
        {
            int errors = 0;
            for (int i = _restore.Count - 1; i >= 0; i--)
                try { _restore[i](); }
                catch (Exception exception) { errors++; report.AppendLine("ERROR restoring static state: " + exception); }
            if (errors == 0) report.AppendLine("PASS All " + _restore.Count + " captured static fields/collections restored");
            return errors;
        }
    }

    // LayoutTask measures TMP text even without rendering. Keep dynamic glyph additions away from assets.
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
            var atlases = new Texture2D[source.atlasTextures.Length];
            for (int i = 0; i < atlases.Length; i++) atlases[i] = Copy(source.atlasTextures[i]);
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
            Material material = Copy(text.fontSharedMaterial);
            text.font = Font(text.font);
            if (material != null && text.font != null && text.font.atlasTextures.Length > 0)
                material.mainTexture = text.font.atlasTextures[0];
            text.fontSharedMaterial = material;
        }
        public void Dispose()
        {
            foreach (TMP_FontAsset font in _fonts.Values)
                if (font != null)
                    foreach (Texture2D atlas in font.atlasTextures)
                        if (atlas != null && !EditorUtility.IsPersistent(atlas) && !_owned.Contains(atlas)) _owned.Add(atlas);
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
        }
    }
}
