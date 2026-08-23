public static class InteractPrompt
{
    public static string Format(string action) =>
        Localization.Format(LocalizationKeys.PromptPressE, action ?? string.Empty);
}

public static class PackActionPrompt
{
    public static string Format(string action) =>
        Localization.Format(LocalizationKeys.PromptPressF, action ?? string.Empty);
}
