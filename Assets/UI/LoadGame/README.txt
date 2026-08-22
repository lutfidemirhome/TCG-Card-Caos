Load Game UI
=============

Same workflow as the main menu: edit layout in the scene, swap art on Image components.

1. Scene (edit here)
   Assets/Scenes/MenuScene.unity
   Hierarchy:
     MainMenuCanvas
       └── Panel_LoadGame          (inactive overlay)
             ├── Background          (solid color; tweak Color + alpha in Inspector)
             ├── Title
             ├── ListFrame / slots
             ├── Scrollbar
             ├── Button_Cancel
             └── Panel_LoadConfirm (inactive; enable only when editing)
                   └── Band         (solid color; tweak Color + alpha in Inspector)

2. Art slices
   Assets/UI/LoadGame/Art/
     confirm_band.png, yes_button.png, no_button.png, ...

3. Unity menu
   TCG Card Caos → UI → Open Main Menu Scene
   TCG Card Caos → UI → Add Load Game Confirm Dialog   (adds Panel_LoadConfirm if missing)
   TCG Card Caos → UI → Add Load Game Panel            (rebuilds full load game overlay)

Enable Panel_LoadConfirm in Hierarchy to preview the confirm band and edit Label materials.
Leave it inactive before saving; runtime shows it only after a save slot is clicked.
