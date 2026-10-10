/// <summary>
/// One Space press jumps normally until the Long Jump chest is opened.
/// Unlocking restores the original enhanced height; never adds an airborne jump.
/// </summary>
public static class PlayerJumpSkill
{
    public const float LongJumpHeightMultiplier = 5f;

    public static float HeightMultiplier => MinorSkillProgress.IsUnlocked(MinorSkillProgress.LongJump)
        ? LongJumpHeightMultiplier : 1f;
}
