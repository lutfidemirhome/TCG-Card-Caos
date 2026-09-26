using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SkillSaveRecord
{
    public int[] levels;
    public string[] completedRows;
    public float[] cooldowns;
    public float[] activeTimes;
}

/// <summary>Per-save progress. Each physical horizontal row earns credit once.</summary>
public static class SkillProgress
{
    static readonly HashSet<string> Completed = new HashSet<string>(StringComparer.Ordinal);
    static readonly List<int> Rows = new List<int>(16);
    static readonly int[] Levels = new int[SkillCatalog.Count];
    static readonly float[] Cooldowns = new float[SkillCatalog.Count];
    static readonly float[] Active = new float[SkillCatalog.Count];
    static readonly float[] TestCooldowns = new float[SkillCatalog.Count];
    static readonly float[] TestActive = new float[SkillCatalog.Count];
    public static bool IsTestingAllSkills { get; private set; }
    static float[] CurrentCooldowns => IsTestingAllSkills ? TestCooldowns : Cooldowns;
    static float[] CurrentActive => IsTestingAllSkills ? TestActive : Active;
    public static bool Ready { get; private set; }
    public static int Revision { get; private set; }
    public static int CompletedRows => Completed.Count;
    public static int Level(int skill) => IsTestingAllSkills ? SkillCatalog.MaxLevel(skill) : Levels[skill];
    public static float Cooldown(int skill) => CurrentCooldowns[skill];
    public static float ActiveTime(int skill) => CurrentActive[skill];
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
        Ready = false; Completed.Clear(); Rows.Clear();
        IsTestingAllSkills = false;
        Array.Clear(Levels, 0, Levels.Length); Array.Clear(Cooldowns, 0, Cooldowns.Length); Array.Clear(Active, 0, Active.Length);
        Array.Clear(TestCooldowns, 0, TestCooldowns.Length); Array.Clear(TestActive, 0, TestActive.Length);
        Revision++;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Temporary preview only; Capture always uses the real levels and timers.</summary>
    public static void ToggleAllSkillsForTesting()
    {
        if (!Ready) return;
        IsTestingAllSkills = !IsTestingAllSkills;
        Array.Clear(TestCooldowns, 0, TestCooldowns.Length);
        Array.Clear(TestActive, 0, TestActive.Length);
        Revision++;
    }
#endif

    public static void Restore(SkillSaveRecord saved)
    {
        Reset();
        if (saved != null)
        {
            if (saved.completedRows != null)
                foreach (string row in saved.completedRows) if (!string.IsNullOrEmpty(row)) Completed.Add(row);
            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                if (saved.levels != null && i < saved.levels.Length) Levels[i] = Mathf.Clamp(saved.levels[i], 0, SkillCatalog.MaxLevel(i));
                // An upgrade does not shorten an already running cooldown; loading must preserve it too.
                if (saved.cooldowns != null && i < saved.cooldowns.Length) Cooldowns[i] = SafeTime(saved.cooldowns[i], Levels[i] > 0 ? SkillCatalog.MaxCooldown(i) : 0f);
                if (saved.activeTimes != null && i < saved.activeTimes.Length && i >= 2) Active[i] = SafeTime(saved.activeTimes[i], SkillCatalog.Effect(i, Levels[i]));
            }
        }
        // Also migrates older saves: already completed rows are credited, without moving any card.
        foreach (CardShelf shelf in UnityEngine.Object.FindObjectsByType<CardShelf>(FindObjectsSortMode.None))
            Collect(shelf);
        Ready = true;
        Revision++;
    }

    static float SafeTime(float value, float maximum) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp(value, 0f, maximum);

    static bool Collect(CardShelf shelf)
    {
        if (!shelf) return false;
        shelf.CopyCompletedSkillRows(Rows);
        bool changed = false;
        string path = PersistentId.BuildPathFallback(shelf.transform);
        foreach (int row in Rows) changed |= Completed.Add(path + ":row:" + row);
        return changed;
    }

    public static void NotifyShelfChanged(CardShelf shelf)
    {
        if (!Ready) return;
        int oldCount = Completed.Count, oldPoints = Points;
        if (!Collect(shelf)) return;
        Revision++;
        GameSaveDirtyTracker.MarkDirty();
        // Preserve periodic autosave; the first rewards and later skill milestones also save.
        if ((Completed.Count >= 2 && Completed.Count <= 18 && Completed.Count != oldCount) || Points > oldPoints)
            GameSaveManager.RequestMilestoneAutosave();
    }

    public static bool Upgrade(int skill)
    {
        if (!Ready || IsTestingAllSkills || skill < 0 || skill >= SkillCatalog.Count || Points < 1 || Levels[skill] >= SkillCatalog.MaxLevel(skill)) return false;
        Levels[skill]++;
        Revision++;
        GameSaveDirtyTracker.MarkDirty();
        GameSaveManager.RequestMilestoneAutosave();
        return true;
    }
    public static bool CanUse(int skill) => Ready && skill >= 0 && skill < SkillCatalog.Count
        && Level(skill) > 0 && Cooldown(skill) <= 0f && ActiveTime(skill) <= 0f;
    public static void Used(int skill)
    {
        CurrentCooldowns[skill] = SkillCatalog.Cooldown(skill, Level(skill));
        CurrentActive[skill] = skill >= 2 ? SkillCatalog.Effect(skill, Level(skill)) : 0f;
        Revision++;
        if (!IsTestingAllSkills) GameSaveDirtyTracker.MarkDirty();
    }
    public static void Tick(float delta)
    {
        if (!Ready || delta <= 0f) return;
        float[] cooldowns = CurrentCooldowns, active = CurrentActive;
        bool changedSecond = false;
        for (int i = 0; i < SkillCatalog.Count; i++)
        {
            int cooldownBefore = Mathf.CeilToInt(cooldowns[i]), activeBefore = Mathf.CeilToInt(active[i]);
            cooldowns[i] = Mathf.Max(0f, cooldowns[i] - delta);
            active[i] = Mathf.Max(0f, active[i] - delta);
            changedSecond |= cooldownBefore != Mathf.CeilToInt(cooldowns[i]) || activeBefore != Mathf.CeilToInt(active[i]);
        }
        // Keep exit/periodic saves current without invalidating card HUD/scene caches.
        if (changedSecond && !IsTestingAllSkills) GameSaveDirtyTracker.MarkDirty();
    }
    public static SkillSaveRecord Capture() => Ready ? new SkillSaveRecord {
        levels = (int[])Levels.Clone(), completedRows = ToSortedArray(),
        cooldowns = (float[])Cooldowns.Clone(), activeTimes = (float[])Active.Clone()
    } : null;
    static string[] ToSortedArray() { var rows = new string[Completed.Count]; Completed.CopyTo(rows); Array.Sort(rows, StringComparer.Ordinal); return rows; }
}
