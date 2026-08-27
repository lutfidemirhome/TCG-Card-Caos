Settings UI
===========

Hierarchy (not Play-only):
  MenuScene  → MainMenuCanvas / Panel_Settings
  MainScene  → InGamePauseCanvas / Panel_Settings

Create / refresh (does not rebuild pause or HUD):
  TCG Card Chaos → UI → Add Settings Panel

Panel starts disabled. Enable the eye icon to edit Rect / Image / TMP,
then disable it again. Play uses the authored objects.

Your slices in Assets/UI/Settings/Art/:
  language_button.png      language dropdown
  resolution_button.png    resolution dropdown
  medium_button.png        graphics quality dropdown
  check_bg.png             checkbox + value box
  approval_icon.png        checked mark
  settings_bar_bg.png      slider track
  settings_bar.png         slider fill
  bar_circle.png           slider handle

Reused from earlier UI:
  Assets/UI/LoadGame/Art/load_game_bg.png   fullscreen background
  Assets/UI/LoadGame/Art/panel_1.png        settings panel
  Assets/UI/LoadGame/Art/yes_button.png     Save
  Assets/UI/ingame/esc_icon.png             ESC back
