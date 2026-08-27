In-Game HUD
============

Always-visible gameplay counters (top-left stats + bottom-right hand).

1. Drop your combined art here:
   Assets/UI/ingame/Hud/

   Expected files:
     hud_stats_panel.png   top-left (background + shelf + card icons)
     hud_hand_panel.png    bottom-right (background + hand icon)

2. Scene (edit layout here)
   Assets/Scenes/MainScene.unity
   Hierarchy root: InGameHudCanvas
     ├── Panel_TopLeft
     │     ├── Background
     │     ├── ShelvesValue
     │     └── CardsValue
     └── Panel_Hand
           ├── Background
           └── HandValue

3. Unity menu
   TCG Card Chaos → UI → Add In-Game HUD

After you drop the two png files, run the menu item once so sprites are assigned.
