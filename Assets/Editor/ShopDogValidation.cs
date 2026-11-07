using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

/// <summary>Runs the real dog bootstrap and player collision checks in a disposable scene.</summary>
[InitializeOnLoad]
public static class ShopDogValidation
{
    const string SessionKey = "TCG.ShopDogValidation";
    const string ScenePath = "Assets/Editor/__ShopDogValidation.unity";
    const string ReportPath = "Temp/shop-dog-validation.txt";
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly string[] IntPrefs = { "version", "language", "width", "height", "refresh", "fullscreen", "quality", "frameRateLimit", "invertY", "invertX", "showDog" };
    static readonly string[] FloatPrefs = { "fov", "sensitivity", "master", "music", "sfx" };
    static readonly StringBuilder Report = new StringBuilder();
    static IEnumerator routine;
    static Func<bool> waitFor;
    static double deadline;
    static int lastFrame, checks;
    static string runtimeError;

    static ShopDogValidation() => EditorApplication.playModeStateChanged += StateChanged;

    [MenuItem("TCG Card Chaos/Validation/Shop Dog (Isolated Play)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play mode.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)) throw new InvalidOperationException("Temporary scene already exists.");
        ShopDogArea source = Object.FindFirstObjectByType<ShopDogArea>();
        if (!source) throw new InvalidOperationException("Open MainScene first.");
        SessionState.SetString(SessionKey + ".start", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        Scene previous = SceneManager.GetActiveScene();
        Scene isolated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(isolated);
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Validation Floor"; floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(20, 1, 20);
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Validation Wall"; wall.transform.position = new Vector3(7, 1.5f, 0); wall.transform.localScale = new Vector3(.4f, 3, 12);
        ShopDogArea area = Object.Instantiate(source);
        area.name = "Validation Dog Area"; area.gameObject.SetActive(false);
        Set(area, "navigationBounds", new Bounds(new Vector3(0, 1, 0), new Vector3(20, 4, 20)));
        Set(area, "spawnPoint", new Vector3(0, .06f, 0));
        Set(area, "roamingPoints", new[] { new Vector3(-6, .06f, 0), new Vector3(5, .06f, 0), new Vector3(0, .06f, 6) });
        Set(area, "walkableRootNames", new[] { "Validation Floor" });
        Set(area, "additionalWalkableSurfaces", new Collider[0]);
        GameObject player = new GameObject("Validation Player", typeof(CharacterController));
        var cc = player.GetComponent<CharacterController>(); cc.height = 1.8f; cc.radius = .3f; cc.center = Vector3.up * .9f;
        player.transform.position = new Vector3(0, .06f, 3);
        GameObject camera = new GameObject("Validation Camera", typeof(Camera));
        camera.transform.position = new Vector3(0, 6, -9); camera.transform.LookAt(Vector3.zero);
        EditorSceneManager.SaveScene(isolated, ScenePath);
        EditorSceneManager.CloseScene(isolated, true); SceneManager.SetActiveScene(previous);
        BackupPrefs();
        SessionState.SetBool(SessionKey, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        Directory.CreateDirectory("Temp"); File.WriteAllText(ReportPath, "Starting isolated dog validation.\n");
        EditorApplication.isPlaying = true;
    }

    static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Report.Clear(); checks = 0; runtimeError = null; lastFrame = -1; waitFor = null;
            deadline = EditorApplication.timeSinceStartup + 90;
            Application.logMessageReceived += LogMessage;
            routine = Scenario(); EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; RestorePrefs();
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(SessionKey + ".start", ""));
            SessionState.SetBool(SessionKey, false); AssetDatabase.DeleteAsset(ScenePath);
            File.AppendAllText(ReportPath, "Returned to Edit mode; original scenes and preferences restored.\n");
        }
    }
    static void LogMessage(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = message + "\n" + stack;
    }
    static void Tick()
    {
        if (routine == null || !EditorApplication.isPlaying) return;
        try
        {
            if (runtimeError != null) throw new InvalidOperationException(runtimeError);
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Dog validation exceeded 90 seconds.");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (waitFor != null && !waitFor()) return;
            waitFor = null;
            if (!routine.MoveNext()) { Finish(null); return; }
            waitFor = routine.Current as Func<bool>;
        }
        catch (Exception error) { Finish(error); }
    }
    static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= LogMessage;
        (routine as IDisposable)?.Dispose(); routine = null; RestorePrefs();
        Report.AppendLine(error == null ? "PASS " + checks + " checks; no saves or Steam account modified." : "FAIL " + error);
        File.WriteAllText(ReportPath, Report.ToString()); EditorApplication.isPlaying = false;
    }
    static void Check(bool pass, string message)
    {
        if (!pass) throw new InvalidOperationException(message);
        checks++; Report.AppendLine("PASS " + message); File.WriteAllText(ReportPath, Report.ToString());
    }
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    static object Call(object target, string name, params object[] arguments) => target.GetType().GetMethod(name, Flags).Invoke(target, arguments);
    static void ReloadSettings() => typeof(GameSettings).GetField("_loaded", Flags).SetValue(null, false);
    static void PersistDog(bool visible)
    {
        var snapshot = GameSettings.Current; snapshot.showDog = visible;
        // Exercise the actual persistence path without applying display/audio changes to the Editor.
        typeof(GameSettings).GetMethod("WritePrefs", Flags).Invoke(null, new object[] { snapshot });
        ReloadSettings();
        (typeof(GameSettings).GetField("Changed", Flags).GetValue(null) as Action)?.Invoke();
    }

    static IEnumerator Scenario()
    {
        LoadingScreenUI.Instance?.Hide();
        typeof(CardInstancedRenderManager).GetProperty("IsGameplayReady", Flags).SetValue(null, true);
        GamePause.SetPaused(false);
        ShopDogArea area = Object.FindFirstObjectByType<ShopDogArea>(FindObjectsInactive.Include);
        CharacterController player = GameObject.Find("Validation Player").GetComponent<CharacterController>();
        PlayerPrefs.DeleteKey("tcg.settings.version"); PlayerPrefs.DeleteKey("tcg.settings.showDog"); ReloadSettings();
        Check(GameSettings.Current.showDog, "Dog defaults ON for a new player");
        PlayerPrefs.SetInt("tcg.settings.version", 1); ReloadSettings();
        Check(GameSettings.Current.showDog, "Dog defaults ON for old settings without this key");
        PersistDog(false);
        Check(!GameSettings.Current.showDog, "OFF survives settings reload");
        area.gameObject.SetActive(true); yield return null;
        Check(Get(area, "_dog") == null && !area.IsReady, "Starting with dog OFF skips model and navigation creation");
        PersistDog(true);
        yield return new Func<bool>(() => area.IsReady);
        GameObject dog = (GameObject)Get(area, "_dog");
        var controller = dog.GetComponent<ShopDogController>(); var agent = dog.GetComponent<NavMeshAgent>();
        Check(dog.activeInHierarchy && agent.isOnNavMesh, "Re-enabling creates the real model and working navigation");
        Check(dog.GetComponentsInChildren<Collider>(true).Length == 0 && dog.GetComponentsInChildren<Rigidbody>(true).Length == 0,
            "Dog has no collision body, including its imported visual hierarchy");
        Check(dog.GetComponent<ShopDogGroundAlignment>() != null, "Ground alignment remains installed");
        Check(dog.GetComponentInChildren<Animation>()["Walk"] != null && dog.GetComponentInChildren<Animation>()["run"] != null,
            "Walking and running animations remain available");
        controller.enabled = false;
        Vector3 origin = dog.transform.position;
        foreach (float direction in new[] { 1f, -1f })
        {
            player.enabled = false; player.transform.position = origin + Vector3.left * direction * 1.5f; player.enabled = true;
            Physics.SyncTransforms();
            for (int i = 0; i < 60; i++) player.Move(new Vector3(direction * .05f, -.01f, 0));
            Check((player.transform.position.x - origin.x) * direction > 1.35f, "Player crosses dog in direction " + direction + " after controller re-enable");
            Check(player.isGrounded && player.transform.position.y > -.1f, "Floor still supports player after crossing dog");
        }
        player.enabled = false; player.transform.position = new Vector3(0, .06f, 0); player.enabled = true;
        Physics.SyncTransforms();
        for (int i = 0; i < 200; i++) player.Move(new Vector3(.05f, -.01f, 0));
        Check(player.transform.position.x < 6.6f && player.transform.position.x > 6f, "World walls still block the player");
        player.enabled = false; player.transform.position = new Vector3(0, .06f, 0); player.enabled = true;
        Physics.SyncTransforms();
        Vector3 playerBefore = player.transform.position;
        controller.enabled = true;
        Check(agent.Warp(new Vector3(-2, origin.y, 0)), "Dog can be placed on its navigation surface");
        Check((bool)Call(controller, "TryStartMoving"), "Real controller starts a route");
        Check(agent.SetDestination(new Vector3(2, origin.y, 0)), "Direct dog route crosses stationary player");
        float nearest = float.MaxValue; double crossingDeadline = EditorApplication.timeSinceStartup + 15;
        while (dog.transform.position.x < 1.3f && EditorApplication.timeSinceStartup < crossingDeadline)
        {
            Vector3 difference = dog.transform.position - player.transform.position; difference.y = 0;
            nearest = Mathf.Min(nearest, difference.magnitude); yield return null;
        }
        Check(dog.transform.position.x > 1.3f && nearest < .45f, "Moving dog passes through player without yielding or detouring");
        Check((player.transform.position - playerBefore).sqrMagnitude < .0001f, "Moving dog does not push the player");
        PersistDog(false); yield return null;
        Check(!dog.activeInHierarchy && !GameSettings.Current.showDog, "Setting OFF stops rendering, animation and navigation");
        PersistDog(true); yield return null;
        Check(dog.activeInHierarchy && agent.isOnNavMesh && (GameObject)Get(area, "_dog") == dog, "Setting ON reuses existing dog and navigation");
        // Force repeated decisions at a safe destination: a planned trip now has a 70% start chance.
        controller.enabled = false; agent.Warp(origin); controller.enabled = true;
        int moving = 0, lying = 0, idle = 0;
        for (int i = 0; i < 1000; i++)
        {
            Set(controller, "_canRest", true); Set(controller, "_restedHere", true);
            Call(controller, "ChooseBehaviour");
            string state = Get(controller, "_behaviour").ToString();
            if (state == "Moving") moving++; else if (state == "LyingDown") lying++; else if (state == "Idle") idle++;
        }
        Check(moving > 620 && moving < 780 && lying > 80 && idle > 80,
            "1000 safe-floor decisions: " + moving + " trips, " + lying + " lie-downs, " + idle + " idle rests (target 70% trips)");
        Check((float)Get(controller, "walkSpeed") == .7f && (float)Get(controller, "runSpeed") == 3.6f, "Walk and run speeds unchanged");
        var table = Resources.Load<LocalizationTable>(LocalizationTable.ResourcePath);
        for (int i = 0; i < GameLanguages.Count; i++)
            Check(!string.IsNullOrWhiteSpace(table.Get(LocalizationKeys.SettingsShowDog, (GameLanguage)i))
                && table.Get(LocalizationKeys.SettingsShowDog, (GameLanguage)i) != LocalizationKeys.SettingsShowDog,
                "Show Dog translation exists: " + (GameLanguage)i);
        IEnumerator hudChecks = ValidateHud(area, dog);
        while (hudChecks.MoveNext()) yield return hudChecks.Current;
        area.gameObject.SetActive(false); yield return null;
        Check(!area.IsReady && !dog, "Area shutdown cleans up dog and navigation");
    }


    static IEnumerator ValidateHud(ShopDogArea area, GameObject dog)
    {
        // Targeted HUD persistence also works before the player has ever saved Settings.
        PlayerPrefs.DeleteKey("tcg.settings.version"); PlayerPrefs.DeleteKey("tcg.settings.showDog"); ReloadSettings();
        float fov = GameSettings.Current.fov;
        int quality = QualitySettings.GetQualityLevel();
        GameSettings.SetDogVisible(false); ReloadSettings();
        Check(!GameSettings.Current.showDog && !PlayerPrefs.HasKey("tcg.settings.version"),
            "HUD preference persists for a first-time player without creating unrelated settings");
        Check(GameSettings.Current.fov == fov && QualitySettings.GetQualityLevel() == quality,
            "HUD toggle preserves camera and graphics settings");
        GameSettings.SetDogVisible(true);
        Canvas canvas = RuntimeOverlayCanvasFactory.Create(null, "Validation HUD", 150);
        Transform skillRoot = new GameObject("SkillUI", typeof(RectTransform)).transform; skillRoot.SetParent(canvas.transform, false);
        Transform hotbar = new GameObject("Hotbar", typeof(RectTransform)).transform; hotbar.SetParent(skillRoot, false);
        Image source = Resources.Load<GameObject>("UI/Skills/SkillUI").transform.Find("Hotbar/Skill0").GetComponent<Image>();
        Image template = new GameObject("Skill0", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        template.transform.SetParent(hotbar, false); template.sprite = source.sprite; template.type = source.type;
        template.enabled = false;
        DogHudToggle hud = DogHudToggle.Ensure(canvas.transform);
        // Exercise the same input state machine without synthesizing OS keyboard events.
        hud.enabled = false;
        Action refreshHud = () => { if (hud) Call(hud, "Refresh"); };
        GameSettings.Changed += refreshHud;
        UiEventSystem.Ensure();
        RectTransform rect = (RectTransform)hud.transform;
        Canvas.ForceUpdateCanvases();
        var paw = hud.GetComponentInChildren<PawIconGraphic>();
        var pawMesh = paw.GetComponent<CanvasRenderer>().GetMesh();
        Check(pawMesh.vertexCount > 100, "Visible paw geometry reaches the CanvasRenderer");
        Check(rect.anchorMin == new Vector2(1, 0) && rect.anchorMax == rect.anchorMin
            && rect.anchoredPosition.y >= 134 && rect.sizeDelta == new Vector2(48, 48),
            "Paw stays above the hand counter in the requested bottom-right space");
        Check(hud.GetComponent<Image>().sprite == source.sprite && hud.GetComponent<Selectable>() == null,
            "Indicator keeps the in-game background and has no mouse button");
        foreach (Graphic graphic in hud.GetComponentsInChildren<Graphic>(true))
            Check(!graphic.raycastTarget, "Indicator never captures mouse clicks: " + graphic.name);
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        Check(!ExecuteEvents.Execute(hud.gameObject, pointer, ExecuteEvents.pointerClickHandler),
            "Mouse clicks have no dog toggle handler");
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        Call(hud, "ProcessShortcut", true, false, true, true);
        for (int i = 0; i < 10; i++) Call(hud, "ProcessShortcut", true, false, true, true);
        Check(GameSettings.Current.showDog, "Holding Alt does not repeatedly toggle the dog");
        Call(hud, "ProcessShortcut", false, false, true, true);
        Check(!GameSettings.Current.showDog && !dog.activeInHierarchy && hud.transform.Find("OffSlash").gameObject.activeSelf,
            "One Alt tap hides the dog and crosses out the paw");
        for (int i = 0; i < 10; i++) Call(hud, "ProcessShortcut", false, false, true, true);
        Check(!GameSettings.Current.showDog, "A completed tap changes the preference only once");
        Call(hud, "ProcessShortcut", true, false, true, true);
        Call(hud, "ProcessShortcut", false, false, true, true);
        Check(GameSettings.Current.showDog && dog.activeInHierarchy && !hud.transform.Find("OffSlash").gameObject.activeSelf,
            "Next Alt tap restores the same dog and white paw");
        Check(Cursor.lockState == CursorLockMode.Locked && !Cursor.visible,
            "Shortcut never releases the mouse or changes camera cursor state");
        PersistDog(false);
        Check(hud.transform.Find("OffSlash").gameObject.activeSelf, "Settings changes synchronize the HUD icon");
        GameSettings.SetDogVisible(true);
        Call(hud, "ProcessShortcut", true, false, true, true);
        Call(hud, "ProcessShortcut", true, true, true, true);
        Call(hud, "ProcessShortcut", false, false, true, true);
        Check(GameSettings.Current.showDog, "Alt+Tab and other key combinations do not toggle the dog");
        Call(hud, "ProcessShortcut", true, false, true, true);
        Call(hud, "OnApplicationFocus", false);
        Call(hud, "ProcessShortcut", false, false, true, true);
        Check(GameSettings.Current.showDog, "Losing window focus cancels the pending tap");
        GamePause.SetPaused(true);
        Call(hud, "ProcessShortcut", true, false, true, false);
        GamePause.SetPaused(false);
        Call(hud, "ProcessShortcut", false, false, true, true);
        Check(GameSettings.Current.showDog, "Alt pressed in a menu does not toggle after returning to gameplay");
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        Check(!(bool)typeof(DogHudToggle).GetProperty("GameplayAvailable", Flags).GetValue(null),
            "Shortcut is blocked while a menu owns the pointer");
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        GameLanguage originalLanguage = Localization.CurrentLanguage;
        var hint = hud.transform.Find("ShortcutHint").GetComponent<TMPro.TMP_Text>();
        string expectedKey = Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer
            ? "Option" : "Alt";
        try
        {
            foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
            {
                Localization.SetLanguagePreview(language);
                GameSettings.SetDogVisible(true);
                Check(hint.text == expectedKey, "Visible dog shows only the platform key: " + language);
                GameSettings.SetDogVisible(false);
                Check(hint.text == expectedKey, "Hidden dog keeps the same short key label: " + language);
                hint.ForceMeshUpdate();
                Check(!hint.isTextOverflowing, "Short key label fits its HUD area: " + language
                    + " (rect " + hint.rectTransform.rect.size + ", preferred " + hint.preferredWidth + " x " + hint.preferredHeight
                    + ", font " + hint.fontSize + ", text " + hint.text + ")");
            }
        }
        finally
        {
            Localization.SetLanguagePreview(originalLanguage);
            GameSettings.SetDogVisible(true);
        }
        Check(hint.rectTransform.anchoredPosition.x == 0 && hint.rectTransform.anchoredPosition.y < -24,
            "Short key label is centered below the paw");
        // Show an enlarged duplicate beside the actual HUD indicator for visual inspection only.
        var preview = Object.Instantiate(hud.gameObject, canvas.transform);
        var previewRect = (RectTransform)preview.transform;
        previewRect.anchorMin = previewRect.anchorMax = previewRect.pivot = new Vector2(.5f, .5f);
        previewRect.anchoredPosition = new Vector2(250, 0); previewRect.localScale = Vector3.one * 3;
        string screenshot = "Temp/dog-hud-preview.png";
        if (File.Exists(screenshot)) File.Delete(screenshot);
        yield return null;
        ScreenCapture.CaptureScreenshot(screenshot);
        yield return new Func<bool>(() => File.Exists(screenshot));
        GameSettings.Changed -= refreshHud;
        Object.Destroy(canvas.gameObject);
    }

    static void BackupPrefs()
    {
        foreach (string suffix in IntPrefs) Backup(suffix, false);
        foreach (string suffix in FloatPrefs) Backup(suffix, true);
    }
    static void Backup(string suffix, bool floating)
    {
        string key = "tcg.settings." + suffix, backup = SessionKey + ".pref." + suffix;
        SessionState.SetBool(backup + ".exists", PlayerPrefs.HasKey(key));
        if (floating) SessionState.SetFloat(backup, PlayerPrefs.GetFloat(key));
        else SessionState.SetInt(backup, PlayerPrefs.GetInt(key));
    }
    static void RestorePrefs()
    {
        foreach (string suffix in IntPrefs) Restore(suffix, false);
        foreach (string suffix in FloatPrefs) Restore(suffix, true);
        PlayerPrefs.Save(); ReloadSettings();
    }
    static void Restore(string suffix, bool floating)
    {
        string key = "tcg.settings." + suffix, backup = SessionKey + ".pref." + suffix;
        if (!SessionState.GetBool(backup + ".exists", false)) PlayerPrefs.DeleteKey(key);
        else if (floating) PlayerPrefs.SetFloat(key, SessionState.GetFloat(backup, 0));
        else PlayerPrefs.SetInt(key, SessionState.GetInt(backup, 0));
    }
}
