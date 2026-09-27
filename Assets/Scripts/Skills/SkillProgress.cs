using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SkillSaveRecord
{
    public int version;
    public int[] levels;
    public string[] completedRows;
    public string[] completedSeries;
    public float[] cooldowns;
    public float[] activeTimes;
    public SkillAutoshelfContext autoshelving;
}

/// <summary>Per-save progress. Completed rows are locked; their contents can earn credit only once.</summary>
public static class SkillProgress
{
    const int SaveVersion = 2;
    const float MaxPickupEffectSeconds = 60f;
    static readonly HashSet<string> Completed = new HashSet<string>(StringComparer.Ordinal);
    static readonly HashSet<string> CompletedSeries = new HashSet<string>(StringComparer.Ordinal);
    static readonly List<int> Rows = new List<int>(16);
    static readonly List<string> PsaKeys = new List<string>(16);
    static readonly int[] Levels = new int[SkillCatalog.Count];
    static readonly float[] Cooldowns = new float[SkillCatalog.Count];
    static readonly float[] Active = new float[SkillCatalog.Count];
    public static bool Ready { get; private set; }
    public static SkillAutoshelfContext AutoshelfContext { get; private set; }

    public static void SetAutoshelfContext(SkillAutoshelfContext context)
    {
        AutoshelfContext = context;
    }
    public static int Revision { get; private set; }
    public static int CompletedRows => Completed.Count;
    public static int Level(int skill) => Levels[skill];
    public static float Cooldown(int skill) => Cooldowns[skill];
    public static float ActiveTime(int skill) => Active[skill];
    public static int Points
    {
        get
        {
            int earned = 0, spent = 0;
            foreach (int threshold in SkillCatalog.Milestones) if (Completed.Count >= threshold) earned++;
            foreach (int level in Levels) spent += level;
            return Mathf.Max(0, earned - spent);
        }
    }
    public static int NextMilestone
    {
        get { foreach (int threshold in SkillCatalog.Milestones) if (Completed.Count < threshold) return threshold; return 0; }
    }
    public static int NextGoal => Completed.Count < 2 ? 2 : Completed.Count < 18 ? Completed.Count + 1 : NextMilestone;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Ready = false; Completed.Clear(); CompletedSeries.Clear(); Rows.Clear(); PsaKeys.Clear();
        AutoshelfContext = null;
        Array.Clear(Levels, 0, Levels.Length); Array.Clear(Cooldowns, 0, Cooldowns.Length); Array.Clear(Active, 0, Active.Length);
        Revision++;
    }

    public static void Restore(SkillSaveRecord saved, bool isNewGame = false)
    {
        Reset();
        foreach (WorldCard card in UnityEngine.Object.FindObjectsByType<WorldCard>(FindObjectsSortMode.None))
            card.SetSkillCompletionLocked(false);
        if (!isNewGame && saved != null)
        {
            AddSavedKeys(Completed, saved.completedRows);
            AddSavedKeys(CompletedSeries, saved.completedSeries);
            AutoshelfContext = saved.autoshelving;
            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                if (saved.levels != null && i < saved.levels.Length) Levels[i] = Mathf.Clamp(saved.levels[i], 0, SkillCatalog.MaxLevel(i));
                // Store the full pending cooldown during an effect; upgrades never shorten it.
                if (saved.cooldowns != null && i < saved.cooldowns.Length) Cooldowns[i] = SafeTime(saved.cooldowns[i], Levels[i] > 0 ? SkillCatalog.MaxCooldown(i) : 0f);
                if (saved.activeTimes != null && i < saved.activeTimes.Length && Levels[i] > 0)
                {
                    float maximum = i == (int)CardSkill.Assemble ? MaxPickupEffectSeconds
                        : i >= 2 ? SkillCatalog.Effect(i, Levels[i]) : 0f;
                    Active[i] = SafeTime(saved.activeTimes[i], maximum);
                }
                if (saved.version < SaveVersion && Active[i] > 0f)
                    Cooldowns[i] = Mathf.Max(Cooldowns[i], SkillCatalog.Cooldown(i, Levels[i]));
            }
        }
        bool migrated = false;
        if (!isNewGame)
        {
            // Keep legacy earned credits. Current completed contents become locked and are
            // recorded by series, so a second physical row can never reward those cards again.
            CardShelf[] shelves = UnityEngine.Object.FindObjectsByType<CardShelf>(FindObjectsSortMode.None);
            // Seed every already-credited legacy row before checking new rows. Otherwise a
            // duplicated series visited first could earn again before its old row is seen.
            foreach (CardShelf shelf in shelves) migrated |= SeedCreditedSeries(shelf);
            foreach (CardShelf shelf in shelves) migrated |= Collect(shelf);
            foreach (PsaCabinet cabinet in UnityEngine.Object.FindObjectsByType<PsaCabinet>(FindObjectsSortMode.None))
                migrated |= Collect(cabinet);
        }
        Ready = true;
        Revision++;
        if (migrated || (!isNewGame && saved != null && saved.version < SaveVersion))
            GameSaveDirtyTracker.MarkDirty();
        if (!isNewGame) SteamSkillAchievements.NotifyProgress(CompletedRows);
    }

    static void AddSavedKeys(HashSet<string> target, string[] keys)
    {
        if (keys == null) return;
        foreach (string key in keys) if (!string.IsNullOrEmpty(key)) target.Add(key);
    }

    static float SafeTime(float value, float maximum) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp(value, 0f, maximum);

    static bool SeedCreditedSeries(CardShelf shelf)
    {
        if (!shelf) return false;
        shelf.CopyCompletedSkillRows(Rows);
        bool changed = false;
        string path = PersistentId.BuildPathFallback(shelf.transform);
        foreach (int row in Rows)
            if (Completed.Contains(path + ":row:" + row) && shelf.TryGetCompletedSkillSeries(row, out string series))
                changed |= CompletedSeries.Add("normal:" + series);
        return changed;
    }

    static bool Collect(CardShelf shelf)
    {
        if (!shelf) return false;
        shelf.CopyCompletedSkillRows(Rows);
        bool changed = false;
        string path = PersistentId.BuildPathFallback(shelf.transform);
        foreach (int row in Rows)
        {
            if (!shelf.TryGetCompletedSkillSeries(row, out string series)) continue;
            shelf.SetSkillRowCompletionLock(row, true);
            string rowKey = path + ":row:" + row;
            bool freshSeries = CompletedSeries.Add("normal:" + series);
            // Old saves may already own this physical row: preserve that credit, while
            // learning its series without granting an additional point.
            if (freshSeries) changed |= Completed.Add(rowKey);
            changed |= freshSeries;
        }
        return changed;
    }

    static bool Collect(PsaCabinet cabinet)
    {
        if (!cabinet || !cabinet.CopyCompletedSkillCardKeys(PsaKeys)) return false;
        cabinet.SetSkillCompletionLock(true);
        string rowKey = PersistentId.BuildPathFallback(cabinet.transform) + ":psa";
        bool unusedContents = true, changed = false;
        foreach (string key in PsaKeys) if (CompletedSeries.Contains(key)) unusedContents = false;
        foreach (string key in PsaKeys) changed |= CompletedSeries.Add(key);
        if (unusedContents) changed |= Completed.Add(rowKey);
        return changed;
    }

    public static void NotifyShelfChanged(CardShelf shelf)
    {
        if (!Ready) return;
        int oldCount = Completed.Count, oldPoints = Points;
        if (Collect(shelf)) ProgressChanged(oldCount, oldPoints);
    }

    public static void NotifyPsaCabinetChanged(PsaCabinet cabinet)
    {
        if (!Ready) return;
        int oldCount = Completed.Count, oldPoints = Points;
        if (Collect(cabinet)) ProgressChanged(oldCount, oldPoints);
    }

    static void ProgressChanged(int oldCount, int oldPoints)
    {
        Revision++;
        GameSaveDirtyTracker.MarkDirty();
        SteamSkillAchievements.NotifyProgress(CompletedRows);
        if ((Completed.Count >= 2 && Completed.Count <= 18 && Completed.Count != oldCount) || Points > oldPoints)
            GameSaveManager.RequestMilestoneAutosave();
    }

    public static bool Upgrade(int skill)
    {
        if (!Ready || skill < 0 || skill >= SkillCatalog.Count || Points < 1 || Levels[skill] >= SkillCatalog.MaxLevel(skill)) return false;
        Levels[skill]++;
        Revision++;
        GameSaveDirtyTracker.MarkDirty();
        GameSaveManager.RequestMilestoneAutosave();
        return true;
    }
    public static bool CanUse(int skill) => Ready && skill >= 0 && skill < SkillCatalog.Count
        && Level(skill) > 0 && Cooldown(skill) <= 0f && ActiveTime(skill) <= 0f;
    public static void Used(int skill, float effectDurationOverride = -1f)
    {
        Cooldowns[skill] = SkillCatalog.Cooldown(skill, Level(skill));
        Active[skill] = skill >= 2 ? SkillCatalog.Effect(skill, Level(skill)) : 0f;
        if (skill == (int)CardSkill.Assemble && effectDurationOverride >= 0f)
            Active[skill] = SafeTime(effectDurationOverride, MaxPickupEffectSeconds);
        Revision++;
        GameSaveDirtyTracker.MarkDirty();
    }
    public static void Tick(float delta)
    {
        if (!Ready || delta <= 0f) return;
        float[] cooldowns = Cooldowns, active = Active;
        bool changedSecond = false;
        for (int i = 0; i < SkillCatalog.Count; i++)
        {
            int cooldownBefore = Mathf.CeilToInt(cooldowns[i]), activeBefore = Mathf.CeilToInt(active[i]);
            // Only the part of this frame after the effect ends advances the cooldown.
            float cooldownDelta = Mathf.Max(0f, delta - active[i]);
            active[i] = Mathf.Max(0f, active[i] - delta);
            cooldowns[i] = Mathf.Max(0f, cooldowns[i] - cooldownDelta);
            changedSecond |= cooldownBefore != Mathf.CeilToInt(cooldowns[i]) || activeBefore != Mathf.CeilToInt(active[i]);
        }
        if (changedSecond) GameSaveDirtyTracker.MarkDirty();
    }
    public static SkillSaveRecord Capture() => Ready ? new SkillSaveRecord {
        version = SaveVersion, levels = (int[])Levels.Clone(), completedRows = ToSortedArray(Completed),
        completedSeries = ToSortedArray(CompletedSeries),
        cooldowns = (float[])Cooldowns.Clone(), activeTimes = (float[])Active.Clone(),
        autoshelving = Active[(int)CardSkill.Autoshelving] > 0f ? AutoshelfContext : null
    } : null;
    static string[] ToSortedArray(HashSet<string> values) { var result = new string[values.Count]; values.CopyTo(result); Array.Sort(result, StringComparer.Ordinal); return result; }
}
