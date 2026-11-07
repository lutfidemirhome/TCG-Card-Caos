using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Real pickup/placement flights and progress round trips in a disposable Play scene.</summary>
[InitializeOnLoad]
public static class CompletedShelfPickupValidation
{
    const string SessionKey = "TCG.CompletedShelfPickupValidation";
    const string ScenePath = "Assets/Editor/__CompletedShelfPickupValidation.unity";
    const string ReportPath = "Temp/completed-shelf-pickup-validation.txt";
    static readonly StringBuilder Report = new StringBuilder();
    static readonly List<CardDefinition> Definitions = new List<CardDefinition>();
    static IEnumerator routine;
    static Func<bool> waitFor;
    static double resumeAt, deadline;
    static int lastFrame = -1, checks, achievementEvents;
    static string runtimeError;

    static CompletedShelfPickupValidation()
    {
        EditorApplication.playModeStateChanged += StateChanged;
    }

    [MenuItem("TCG Card Chaos/Validation/Completed Shelf Pickup (Isolated Play)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run outside Play mode.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save scene changes before running this validation.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
            throw new InvalidOperationException("Temporary validation scene already exists: " + ScenePath);
        SessionState.SetString(SessionKey + ".start", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        Scene previous = SceneManager.GetActiveScene();
        Scene isolated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SaveScene(isolated, ScenePath);
        EditorSceneManager.CloseScene(isolated, true);
        SceneManager.SetActiveScene(previous);
        SessionState.SetBool(SessionKey, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        Directory.CreateDirectory("Temp");
        File.WriteAllText(ReportPath, "Starting isolated Play validation.\n");
        EditorApplication.isPlaying = true;
    }

    static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Report.Clear(); Definitions.Clear(); checks = achievementEvents = 0; runtimeError = null;
            lastFrame = -1; resumeAt = 0; waitFor = null; deadline = EditorApplication.timeSinceStartup + 120;
            Application.logMessageReceived += LogMessage;
            routine = Scenario();
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(SessionKey + ".start", ""));
            SessionState.SetBool(SessionKey, false);
            AssetDatabase.DeleteAsset(ScenePath);
            File.AppendAllText(ReportPath, "Returned to Edit mode; original scenes restored.\n");
        }
    }

    static void LogMessage(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            runtimeError = message + "\n" + stack;
    }

    static void Tick()
    {
        if (routine == null || !EditorApplication.isPlaying) return;
        try
        {
            if (runtimeError != null) throw new InvalidOperationException(runtimeError);
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Validation exceeded 120 seconds.");
            if (EditorApplication.timeSinceStartup < resumeAt || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (waitFor != null && !waitFor()) return;
            waitFor = null;
            if (!routine.MoveNext()) { Finish(null); return; }
            waitFor = routine.Current as Func<bool>;
            resumeAt = EditorApplication.timeSinceStartup + (routine.Current is float delay ? delay : 0);
        }
        catch (Exception error) { Finish(error); }
    }

    static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= LogMessage;
        SteamSkillAchievements.ProgressChanged -= OnAchievementProgress;
        (routine as IDisposable)?.Dispose(); routine = null;
        foreach (CardDefinition definition in Definitions) if (definition) Object.Destroy(definition);
        Report.AppendLine(error == null ? "PASS " + checks + " checks; no player saves or Steam account modified." : "FAIL " + error);
        File.WriteAllText(ReportPath, Report.ToString());
        EditorApplication.isPlaying = false;
    }

    static void Check(bool pass, string message)
    {
        if (!pass) throw new InvalidOperationException(message);
        checks++; Report.AppendLine("PASS " + message);
        File.WriteAllText(ReportPath, Report.ToString());
    }

    static void OnAchievementProgress(int rows) => achievementEvents++;
    static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    static CardShelf Shelf(string name, float x, int rows = 1)
    {
        var root = new GameObject(name); root.transform.position = new Vector3(x, 1, 4);
        var shelf = root.AddComponent<CardShelf>();
        for (int row = 0; row < rows; row++)
        {
            var level = new GameObject(row == 0 ? "ShelfSlots_Level" : "ShelfSlots_Level (" + row + ")");
            level.transform.SetParent(root.transform, false);
            level.transform.localPosition = new Vector3(0, row * .5f, 0);
            for (int i = 0; i < shelf.SlotsPerRow; i++)
            {
                var slot = new GameObject(CardShelfSlotNaming.BuildName(row, i));
                slot.transform.SetParent(level.transform, false);
                slot.transform.localPosition = new Vector3(i * .22f, 0, 0);
                slot.AddComponent<CardShelfSlot>();
            }
        }
        shelf.RefreshSlotCache();
        return shelf;
    }

    static WorldCard[] Fill(CardShelf shelf, string series, int row = 0)
    {
        var cards = new List<WorldCard>();
        foreach (CardShelfSlot slot in shelf.GetComponentsInChildren<CardShelfSlot>())
        {
            if (slot.RowIndex != row) continue;
            int number = shelf.ResolveSlotNumber(slot);
            var definition = ScriptableObject.CreateInstance<CardDefinition>(); Definitions.Add(definition);
            Set(definition, "definitionId", "normal_common_validation_" + series + "_" + number);
            Set(definition, "shelfSlotNumber", number);
            var card = CardFactory.CreateWorldCard(Vector3.zero, Quaternion.identity, definition, 0);
            Check(slot.RestoreOccupiedCard(card, .003f, true, false), "Create normal shelf fixture");
            cards.Add(card);
        }
        return cards.ToArray();
    }

    static PsaCabinet Cabinet(string name, int grade, float x)
    {
        var root = new GameObject(name); root.transform.position = new Vector3(x, 1, 8);
        var cabinet = root.AddComponent<PsaCabinet>();
        // Reset is called when adding a component in some Editor versions.
        while (root.transform.childCount > 0) Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
        for (int i = 0; i < 10; i++)
        {
            var holder = new GameObject("Holder " + i); holder.transform.SetParent(root.transform, false);
            holder.transform.localPosition = new Vector3((i % 5) * .3f, 0, (i / 5) * .4f);
            holder.AddComponent<PsaCabinetSlot>().SetSlotNumber(grade);
        }
        return cabinet;
    }

    static WorldCard[] Fill(PsaCabinet cabinet, int grade)
    {
        var cards = new List<WorldCard>();
        for (int i = 0; i < cabinet.Slots.Length; i++)
        {
            // Distinct identities, including both languages in the same graded cabinet.
            var card = CardFactory.CreateWorldPsaCard(Vector3.zero, Quaternion.identity, grade,
                i / 2 + 1, cardSet: i % 2 == 0 ? PsaCardSet.English : PsaCardSet.Japanese);
            Check(cabinet.Slots[i].RestoreOccupiedCard(card, false), "Create mixed-language TCG fixture");
            cards.Add(card);
        }
        return cards.ToArray();
    }

    static void SameProgress(SkillSaveRecord before, int events, string context)
    {
        SkillSaveRecord now = SkillProgress.Capture();
        Check(string.Join("|", before.completedRows) == string.Join("|", now.completedRows), context + ": earned rows preserved");
        Check(string.Join("|", before.levels) == string.Join("|", now.levels), context + ": purchased skill levels preserved");
        Check(achievementEvents == events, context + ": no repeated achievement progress");
    }

    static IEnumerator Scenario()
    {
        yield return .2f;
        Check(!GameScenes.IsActiveGameScene() && GameSaveManager.Instance == null, "Isolated scene has no real save manager");
        LoadingScreenUI.Instance?.Hide(); GamePause.SetPaused(false);
        var cameraObject = new GameObject("Validation player"); cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0, 1.7f, 0);
        cameraObject.AddComponent<Camera>(); cameraObject.AddComponent<AudioListener>();
        PlayerCardHand hand = cameraObject.AddComponent<PlayerCardHand>();
        SkillProgress.Restore(null, isNewGame: true);
        SteamSkillAchievements.ProgressChanged += OnAchievementProgress;
        var shelfA = Shelf("Normal A", -4); var shelfB = Shelf("Normal B", 0); var shelfC = Shelf("Normal C", 4);
        WorldCard[] normalA = Fill(shelfA, "alpha"), normalB = Fill(shelfB, "beta");
        SkillProgress.NotifyShelfChanged(shelfA); SkillProgress.NotifyShelfChanged(shelfB);
        Check(SkillProgress.CompletedRows == 2 && SkillProgress.Points == 1, "Two new rows earn the first skill point");
        Check(SkillProgress.Upgrade(0), "Earned skill point can be spent");
        SkillSaveRecord baseline = SkillProgress.Capture(); int events = achievementEvents;
        foreach (WorldCard card in normalA) Check(!card.IsShelfRowCompleteLocked, "Completed normal card has no permanent pickup lock");
        normalA[0].PlayShelfRowCompleteFeedback();
        Check(normalA[0].IsShelfRowCompleteLocked && !hand.TryPickup(normalA[0], false), "Short completion effect protects its moving card");
        yield return .9f;
        Check(!normalA[0].IsShelfRowCompleteLocked && hand.TryPickup(normalA[0], false), "Normal card can be picked up after completion effect");
        yield return .8f;
        Check(normalA[0].IsHeld && !shelfA.IsComplete(), "Taking completed card updates shelf occupancy");
        SameProgress(baseline, events, "Remove completed normal card");
        SkillProgress.Restore(JsonUtility.FromJson<SkillSaveRecord>(JsonUtility.ToJson(SkillProgress.Capture())));
        SameProgress(baseline, events, "Reload progress while normal row is incomplete");
        Check(shelfA.TryPlaceSkillCard(hand, normalA[0]), "Return completed normal card through actual placement flight");
        yield return new Func<bool>(() => !normalA[0].IsFlyingToShelf);
        Check(CabinetCompletionEffect.IsActiveFor(shelfA.transform), "Normal cabinet completion plays gold/pop effect");
        yield return .8f;
        Check(!CabinetCompletionEffect.IsActiveFor(shelfA.transform), "Normal cabinet completion effect ends normally");
        Check(shelfA.IsComplete(), "Returned normal card completes row again");
        SameProgress(baseline, events, "Re-complete original normal row");
        foreach (WorldCard card in normalA) Check(hand.TryPickup(card, false), "Pick up completed normal series");
        yield return .8f;
        foreach (WorldCard card in normalA) Check(shelfC.TryPlaceSkillCard(hand, card), "Move completed normal series to another cabinet");
        yield return 1.7f;
        Check(shelfC.IsComplete() && !shelfA.IsComplete(), "Normal series moved to different cabinet");
        SameProgress(baseline, events, "Complete different normal cabinet with same cards");
        foreach (WorldCard card in normalA) Check(hand.TryPickup(card, false), "Re-pick moved normal series");
        yield return .8f;
        foreach (WorldCard card in normalA) Check(shelfA.TryPlaceSkillCard(hand, card), "Return normal series to original row");
        yield return new Func<bool>(() => Array.TrueForAll(normalA, card => !card.IsFlyingToShelf));
        Check(CabinetCompletionEffect.IsActiveFor(shelfA.transform), "Normal cabinet repeated completion plays gold/pop effect");
        yield return .8f;
        Check(!CabinetCompletionEffect.IsActiveFor(shelfA.transform), "Normal cabinet repeated completion effect ends normally");
        SameProgress(baseline, events, "Return entire normal series");

        var cabinetA = Cabinet("TCG A", 8, -4); var cabinetB = Cabinet("TCG B", 8, 0);
        WorldCard[] psa = Fill(cabinetA, 8);
        SkillProgress.NotifyPsaCabinetChanged(cabinetA);
        Check(SkillProgress.CompletedRows == 3 && SkillProgress.Points == 1, "Fresh TCG cabinet earns normal progress");
        baseline = SkillProgress.Capture(); events = achievementEvents;
        foreach (WorldCard card in psa) Check(!card.IsShelfRowCompleteLocked, "Completed TCG card has no permanent pickup lock");
        Check(hand.TryPickup(psa[0], false), "Pick up completed TCG card");
        yield return .8f;
        Check(psa[0].IsHeld && !cabinetA.IsComplete(), "Taking TCG card updates cabinet occupancy");
        SkillProgress.Restore(JsonUtility.FromJson<SkillSaveRecord>(JsonUtility.ToJson(SkillProgress.Capture())));
        SameProgress(baseline, events, "Reload progress while TCG cabinet is incomplete");
        Check(cabinetA.TryPlaceSkillCard(hand, psa[0]), "Return completed TCG card through actual placement flight");
        yield return new Func<bool>(() => !psa[0].IsFlyingToShelf);
        Check(CabinetCompletionEffect.IsActiveFor(cabinetA.transform), "TCG cabinet completion plays gold/pop effect");
        yield return .8f;
        Check(!CabinetCompletionEffect.IsActiveFor(cabinetA.transform), "TCG cabinet completion effect ends normally");
        Check(cabinetA.IsComplete(), "Returned TCG card completes cabinet again");
        SameProgress(baseline, events, "Re-complete original TCG cabinet");
        foreach (WorldCard card in psa) Check(hand.TryPickup(card, false), "Pick up completed TCG cabinet contents");
        yield return .8f;
        foreach (WorldCard card in psa) Check(cabinetB.TryPlaceSkillCard(hand, card), "Move TCG contents to another cabinet");
        yield return 1.7f;
        Check(cabinetB.IsComplete() && !cabinetA.IsComplete(), "Mixed-language TCG contents moved to different cabinet");
        SameProgress(baseline, events, "Complete different TCG cabinet with same cards");
        foreach (WorldCard card in psa) Check(hand.TryPickup(card, false), "Re-pick moved TCG contents");
        yield return .8f;
        foreach (WorldCard card in psa) Check(cabinetA.TryPlaceSkillCard(hand, card), "Return TCG contents to original cabinet");
        yield return new Func<bool>(() => Array.TrueForAll(psa, card => !card.IsFlyingToShelf));
        Check(CabinetCompletionEffect.IsActiveFor(cabinetA.transform), "TCG cabinet repeated completion plays gold/pop effect");
        yield return .8f;
        Check(!CabinetCompletionEffect.IsActiveFor(cabinetA.transform), "TCG cabinet repeated completion effect ends normally");
        SameProgress(baseline, events, "Return entire TCG cabinet contents");

        var cabinetFresh = Cabinet("TCG fresh", 9, 4); Fill(cabinetFresh, 9);
        SkillProgress.NotifyPsaCabinetChanged(cabinetFresh);
        Fill(shelfC, "gamma"); SkillProgress.NotifyShelfChanged(shelfC);
        Check(SkillProgress.CompletedRows == 5 && SkillProgress.Points == 3, "New series and new TCG contents still earn points");
        baseline = SkillProgress.Capture(); events = achievementEvents;
        var legacy = JsonUtility.FromJson<SkillSaveRecord>(JsonUtility.ToJson(baseline));
        legacy.version = 1; legacy.completedSeries = null;
        SkillProgress.Restore(legacy);
        SameProgress(baseline, events, "Legacy save migrates without removing or duplicating credit");
        Check(SkillProgress.Capture().completedSeries.Length == baseline.completedSeries.Length, "Legacy completed contents get anti-repeat identities");
        Check(hand.TryPickup(normalA[0], false) && hand.TryPickup(psa[0], false), "Normal and TCG cards remain movable after legacy restore");
        yield return .8f;
        Check(shelfA.TryPlaceSkillCard(hand, normalA[0]) && cabinetA.TryPlaceSkillCard(hand, psa[0]), "Return both card types after legacy restore");
        yield return 1.7f;
        SameProgress(baseline, events, "Re-completion after legacy restore");
        Check(hand.Count == 0 && SkillProgress.Points == 3, "Hand empty and unspent skill points retained");
        // Two repeated completions using just one removed card in each full cabinet.
        for (int repeat = 0; repeat < 2; repeat++)
        {
            Check(hand.TryPickup(normalA[0], false) && hand.TryPickup(psa[0], false), "Remove one card from each completed cabinet");
            yield return .8f;
            Check(!shelfA.IsComplete() && !cabinetA.IsComplete(), "Both cabinets become incomplete after removal");
            Check(shelfA.TryPlaceSkillCard(hand, normalA[0]) && cabinetA.TryPlaceSkillCard(hand, psa[0]), "Replace one card in each completed cabinet");
            yield return new Func<bool>(() => !normalA[0].IsFlyingToShelf && !psa[0].IsFlyingToShelf);
            Check(shelfA.IsComplete() && cabinetA.IsComplete(), "Both live cabinet counts return to complete");
            Check(CabinetCompletionEffect.IsActiveFor(shelfA.transform) && CabinetCompletionEffect.IsActiveFor(cabinetA.transform), "Both cabinets replay gold/pop on each re-completion");
            SameProgress(baseline, events, "Repeated single-card cabinet completion");
            yield return .8f;
        }
        var multiRowShelf = Shelf("Two-row cabinet", 8, rows: 2);
        WorldCard[] rowCards = Fill(multiRowShelf, "row_repeat");
        SkillProgress.NotifyShelfChanged(multiRowShelf);
        baseline = SkillProgress.Capture(); events = achievementEvents;
        for (int repeat = 0; repeat < 2; repeat++)
        {
            Check(hand.TryPickup(rowCards[0], false), "Remove card from completed row in partial cabinet");
            yield return .8f;
            Check(multiRowShelf.TryPlaceSkillCard(hand, rowCards[0]), "Replace card in completed row");
            yield return new Func<bool>(() => !rowCards[0].IsFlyingToShelf);
            Check(!multiRowShelf.IsComplete() && multiRowShelf.HasCompletedSeriesRow(), "Row completes while whole cabinet stays incomplete");
            foreach (WorldCard card in rowCards) Check(card.IsShelfRowCompleteLocked, "Entire row replays its gold/pop effect");
            Check(!CabinetCompletionEffect.IsActiveFor(multiRowShelf.transform), "Partial cabinet celebrates only the completed row");
            SameProgress(baseline, events, "Repeated row completion");
            yield return .8f;
            foreach (WorldCard card in rowCards) Check(!card.IsShelfRowCompleteLocked, "Repeated row effect releases pickup lock");
        }
        WorldCard[] secondRow = Fill(multiRowShelf, "second_row_repeat", row: 1);
        SkillProgress.NotifyShelfChanged(multiRowShelf);
        baseline = SkillProgress.Capture(); events = achievementEvents;
        foreach (WorldCard card in new[] { secondRow[0], rowCards[0] })
        {
            Check(hand.TryPickup(card, false), "Remove card from full multi-row normal cabinet");
            yield return .8f;
            Check(!multiRowShelf.IsComplete(), "Full multi-row cabinet becomes incomplete");
            Check(multiRowShelf.TryPlaceSkillCard(hand, card), "Return card to full multi-row cabinet");
            yield return new Func<bool>(() => !card.IsFlyingToShelf);
            Check(multiRowShelf.IsComplete() && CabinetCompletionEffect.IsActiveFor(multiRowShelf.transform), "Entire multi-row normal cabinet replays gold/pop effect");
            SameProgress(baseline, events, "Full multi-row cabinet re-completion");
            yield return .8f;
        }
        Check(GameSaveManager.Instance == null, "Validation never created real save manager");
    }
}
