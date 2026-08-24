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
    public const string SaveGameAutoName = "save.auto_name";
    public const string SaveGameManualName = "save.manual_name";

    // In-game pause
    public const string PauseBack = "pause.back";
    public const string PauseResume = "pause.resume";
    public const string PauseSave = "pause.save";

    // Loading overlay
    public const string UiLoading = "ui.loading";
    public const string UiLoadingDisclaimer = "ui.loading.disclaimer";

    // World interaction prompts
    public const string PromptPressE = "prompt.press_e";
    public const string PromptPressF = "prompt.press_f";
    public const string PromptPickUp = "prompt.pick_up";
    public const string PromptPlaceCard = "prompt.place_card";
    public const string PromptPlacePsa = "prompt.place_psa";
    public const string PromptOpenPack = "prompt.open_pack";
    public const string PromptCollectCards = "prompt.collect_cards";
    public const string PromptHandFull = "prompt.hand_full";
    public const string PromptHandFullShort = "prompt.hand_full_short";
    public const string PromptNeedHandSlots = "prompt.need_hand_slots";

    // Settings
    public const string SettingsSave = "settings.save";
    public const string SettingsBack = "settings.back";
    public const string SettingsLanguage = "settings.language";
    public const string SettingsResolution = "settings.resolution";
    public const string SettingsFullscreen = "settings.fullscreen";
    public const string SettingsQuality = "settings.quality";
    public const string SettingsFov = "settings.fov";
    public const string SettingsSensitivity = "settings.sensitivity";
    public const string SettingsInvertY = "settings.invert_y";
    public const string SettingsInvertX = "settings.invert_x";
    public const string SettingsMaster = "settings.master";
    public const string SettingsMusic = "settings.music";
    public const string SettingsSfx = "settings.sfx";
    public const string SettingsQualityLow = "settings.quality_low";
    public const string SettingsQualityMedium = "settings.quality_medium";
    public const string SettingsQualityHigh = "settings.quality_high";

    // First-launch welcome popup
    public const string WelcomeTitle = "welcome.title";
    public const string WelcomeBody = "welcome.body";
    public const string WelcomeStart = "welcome.start";

    // Demo complete popup
    public const string DemoCompleteTitle = "demo_complete.title";
    public const string DemoCompleteBody = "demo_complete.body";
    public const string DemoCompleteWishlist = "demo_complete.wishlist";

    // In-game tutorial hints (inline <sprite name="W"> tags)
    public const string TutorialMove = "tutorial.move";
    public const string TutorialPickup = "tutorial.pickup";
    public const string TutorialDrop = "tutorial.drop";
}
