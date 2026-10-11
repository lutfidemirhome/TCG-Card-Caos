using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// SDK-independent achievement catalogue. Only real, completed shelf tasks feed this bridge;
/// buying a skill does not unlock achievements.
/// </summary>
public static class SteamSkillAchievements
{
    // Steam rewards the same 45 completion milestones as skill upgrade points.
    // Copy the table once so the two catalogues cannot gain extra in-between goals.
    static readonly int[] Goals = (int[])SkillCatalog.Milestones.Clone();
    static readonly string[] Names = BuildNames();
    public const string CompletionApiName = "SHOP_COMPLETE";
    public static int MilestoneCount => Goals.Length;
    public static int Count => Goals.Length + 1;
    public static int HighestCompletedRows { get; private set; }
    public static bool GameCompleted { get; private set; }
    public static event Action<int> ProgressChanged;
    public static event Action CompletionEarned;

    public static int RequiredRows(int index) => index == Goals.Length ? int.MaxValue : Goals[index];
    public static string ApiName(int index) => index == Goals.Length ? CompletionApiName : Names[index];
    public static bool IsEarned(int index, int rows, bool completed) => index == Goals.Length ? completed : rows >= Goals[index];
    public static bool IsAvailableForBuild => !GameBuildVariant.IsDemo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()
    {
        HighestCompletedRows = 0;
        ProgressChanged = null;
        GameCompleted = false;
        CompletionEarned = null;
    }

    public static void NotifyGameCompleted()
    {
        if (!IsAvailableForBuild || GameCompleted) return;
        GameCompleted = true;
        CompletionEarned?.Invoke();
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
