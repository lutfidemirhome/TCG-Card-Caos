using UnityEngine;

public enum CardSkill { Assemble, Sort, ShelfGuide, Insight, Autoshelving }

/// <summary>Level tables transcribed from Skilller.pdf. Index zero means locked.</summary>
public static class SkillCatalog
{
    public const int Count = 5;
    public static readonly string[] Keys = { "assemble", "sort", "guide", "insight", "autoshelf" };
    static readonly int[][] Cooldowns = {
        new[] { 0,120,110,100,90,80,70,60,40,30,10 },
        new[] { 0,30,25,20,10,5 },
        new[] { 0,60,50,40,30,30,25,25,20,10,5 },
        new[] { 0,60,55,50,50,40,40,30,30,20,20 },
        new[] { 0,100,90,80,70,60,50,40,30,20,10 }
    };
    static readonly int[][] Effects = {
        new[] { 0,1,2,3,4,5,6,7,8,9,9 }, new[] { 0,0,0,0,0,0 },
        new[] { 0,15,20,25,30,35,40,45,50,55,60 },
        new[] { 0,7,10,12,17,20,23,27,30,35,40 },
        new[] { 0,10,15,20,25,30,35,40,45,50,55 }
    };
    // Written intervals take precedence over skipped/repeated handwritten row numbers.
    public static readonly int[] Milestones = {
        2,3,4,5,6,7,8,9,10,11,12,14,16,18,21,24,27,31,35,39,44,49,
        55,61,68,75,83,91,100,109,119,129,140,152,165,179,194,214,239,269,
        309,359,409,459,509
    };
    public static int MaxLevel(int skill) => Cooldowns[skill].Length - 1;
    public static int MaxCooldown(int skill)
    {
        int maximum = 0;
        foreach (int cooldown in Cooldowns[skill]) maximum = Mathf.Max(maximum, cooldown);
        return maximum;
    }
    public static int Cooldown(int skill, int level) => Cooldowns[skill][Mathf.Clamp(level, 0, MaxLevel(skill))];
    public static int Effect(int skill, int level) => Effects[skill][Mathf.Clamp(level, 0, MaxLevel(skill))];
}
