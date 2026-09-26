using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// SDK-independent achievement catalogue. Only real, completed shelf tasks feed this bridge;
/// buying a skill or enabling the temporary skill preview does not unlock achievements.
/// </summary>
public static class SteamSkillAchievements
{
    static readonly int[] Goals = BuildGoals();
    static readonly string[] Names = BuildNames();
    public static int Count => Goals.Length;
    public static int HighestCompletedRows { get; private set; }
    public static event Action<int> ProgressChanged;

    public static int RequiredRows(int index) => Goals[index];
    public static string ApiName(int index) => Names[index];
    public static bool IsAvailableForBuild => !GameBuildVariant.IsDemo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()
    {
        HighestCompletedRows = 0;
        ProgressChanged = null;
    }

    /// <summary>Call after restoring or increasing the real completed-task count.</summary>
    public static void NotifyProgress(int completedRows)
    {
        if (!IsAvailableForBuild) return;
        completedRows = Mathf.Clamp(completedRows, 0, Goals[Goals.Length - 1]);
        if (completedRows <= HighestCompletedRows) return;
        HighestCompletedRows = completedRows;
        ProgressChanged?.Invoke(completedRows);
    }

    static int[] BuildGoals()
    {
        // Match SkillProgress.NextGoal, including 13, 15 and 17 (tasks without an upgrade point).
        var goals = new List<int>(48);
        for (int row = 2; row <= 18; row++) goals.Add(row);
        foreach (int row in SkillCatalog.Milestones)
            if (row > 18 && !goals.Contains(row)) goals.Add(row);
        goals.Sort();
        return goals.ToArray();
    }

    static string[] BuildNames()
    {
        var names = new string[Goals.Length];
        for (int i = 0; i < names.Length; i++)
            names[i] = "SKILL_ROWS_" + Goals[i].ToString("D3", CultureInfo.InvariantCulture);
        return names;
    }
}
