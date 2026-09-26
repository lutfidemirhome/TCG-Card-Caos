/// <summary>
/// The existing enhanced jump is always available through Space.
/// It is independent of the upgrade system and has no hotbar toggle.
/// </summary>
public static class PlayerJumpSkill
{
    public const float DoubleJumpHeightMultiplier = 5f;

    public static float HeightMultiplier => DoubleJumpHeightMultiplier;
}
