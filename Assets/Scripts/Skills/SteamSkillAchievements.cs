using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// SDK-independent achievement catalogue. Only real, completed shelf tasks feed this bridge;
/// buying a skill or enabling the temporary skill preview does not unlock achievements.
/// </summary>
public static class SteamSkillAchievements
{
    // Steam rewards the same 45 completion milestones as skill upgrade points.
    // Copy the table once so the two catalogues cannot gain extra in-between goals.
    static readonly int[] Goals = (int[])SkillCatalog.Milestones.Clone();
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

    static string[] BuildNames()
    {
        var names = new string[Goals.Length];
        for (int i = 0; i < names.Length; i++)
            names[i] = "SKILL_ROWS_" + Goals[i].ToString("D3", CultureInfo.InvariantCulture);
        return names;
    }
}
