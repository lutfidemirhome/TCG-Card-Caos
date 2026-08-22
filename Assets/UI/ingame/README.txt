In-Game Pause UI
================

Same workflow as the main menu: edit layout in the scene, swap art on Image components.

1. Scene
   Assets/Scenes/MainScene.unity
   Hierarchy root: InGamePauseCanvas
     └── Panel_Pause          (inactive until ESC)
           ├── Background     (solid color; tweak Color + alpha)
           ├── EscHint
           ├── Logo           (reuses MainMenu tcg_demo_logo)
           └── Panel          (panel_4_ingame)
                 └── ButtonColumn → Resume / Save / Load / Settings / Quit

2. Art
   Assets/UI/ingame/          (pause-only slices: panel_4_ingame, esc_icon)
   Assets/UI/ingame/Hud/      (always-on HUD: hud_stats_panel, hud_hand_panel)
   Assets/UI/MainMenu/Art/    (logo + button sprites reused)

3. Unity menu
   TCG Card Caos → UI → Add In-Game Pause Menu
   TCG Card Caos → UI → Add In-Game HUD

ESC opens the panel and pauses the game. ESC again (or Resume) continues.
Load Game uses the same Load Game overlay as the main menu.
