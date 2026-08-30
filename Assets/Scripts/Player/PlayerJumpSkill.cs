/// <summary>
/// Armed by the in-game Double Jump skill button. Space still jumps the same way;
/// only the height scale changes while the skill is on.
/// </summary>
public static class PlayerJumpSkill
{
    public const float DoubleJumpHeightMultiplier = 5f;

    public static bool DoubleJumpArmed { get; set; }

    public static float HeightMultiplier =>
        DoubleJumpArmed ? DoubleJumpHeightMultiplier : 1f;
}
