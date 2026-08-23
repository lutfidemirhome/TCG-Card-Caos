/// <summary>
/// Every translatable UI string is referenced by one of these keys. Keep them grouped by screen
/// so the translator hand-off CSV stays readable.
/// </summary>
public static class LocalizationKeys
{
    // Main menu buttons
    public const string MenuContinue = "menu.continue";
    public const string MenuNewGame = "menu.new_game";
    public const string MenuLoadGame = "menu.load_game";
    public const string MenuSettings = "menu.settings";
    public const string MenuQuit = "menu.quit";
    public const string MenuFeedback = "menu.feedback";

    // Main menu roadmap panel
    public const string MenuRoadmapTitle = "menu.roadmap.title";
    public const string MenuRoadmapItems = "menu.roadmap.items";
    public const string MenuFollowUs = "menu.follow_us";

    // Load Game screen
    public const string LoadGameTitle = "load.title";
    public const string LoadGameCancel = "load.cancel";
    public const string LoadGameDate = "load.date";
    public const string LoadGamePlayTime = "load.play_time";
    public const string LoadGameCardsPlaced = "load.cards_placed";
    public const string LoadGameShelves = "load.shelves";
    public const string LoadGameEmpty = "load.empty";
    public const string LoadGameConfirmMessage = "load.confirm_message";
    public const string LoadGameConfirmYes = "load.confirm_yes";
    public const string LoadGameConfirmNo = "load.confirm_no";

    // Save Game screen
    public const string SaveGameTitle = "save.title";
    public const string SaveGameEmptySlot = "save.empty_slot";
    public const string SaveGameNotAvailable = "save.not_available";
    public const string SaveGameDeleteHint = "save.delete";
    public const string SaveGameOverwriteConfirm = "save.overwrite_confirm";
    public const string SaveGameDeleteConfirm = "save.delete_confirm";
    public const string SaveGameSaving = "save.saving";
    public const string SaveGameSaved = "save.saved";

    // In-game pause
    public const string PauseBack = "pause.back";
    public const string PauseResume = "pause.resume";
    public const string PauseSave = "pause.save";

    // Loading overlay
    public const string UiLoading = "ui.loading";
}
