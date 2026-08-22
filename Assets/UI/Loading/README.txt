Loading Screen UI
=================

Drop the three slices here, then they appear on LoadingCanvas in MenuScene.

1. Scene (edit layout here)
   Assets/Scenes/MenuScene.unity
   Same overlay is also in MainScene so in-game Load Save matches the menu.
   Hierarchy root: LoadingCanvas
   Runtime always uses the Resources prefab so every load path looks the same.
     └── Panel_Loading          (inactive until a load starts; enable it to edit)
           ├── Background       (loading_bg)
           ├── Tint             (dark blue overlay — tweak Color alpha)
           ├── Logo             (reuses MainMenu tcg_demo_logo)
           ├── Spinner
           │     ├── SpinnerBase     (dark ring)
           │     └── SpinnerYellow   (the icon whose name contains "sarı" or "yellow" — this rotates)
           └── Label            (Baloo 2 ExtraBold, loc key ui.loading)

2. Art — drop files in:
   Assets/UI/Loading/Art/

   Expected names:
     loading_bg.png              full-screen shop photo
     loading_spinner.png         dark ring (bottom layer)
     loading_spinner_sarı.png    yellow/orange arc (top layer, rotates)

   The file whose name contains "sarı" or "yellow" is always placed on top of the other ring.

3. Logo
   Assets/UI/MainMenu/Art/tcg_demo_logo.png  (already assigned)

4. Prefab (used when loading a save from inside the game, MenuScene is not loaded)
   Assets/Resources/UI/LoadingScreen.prefab

5. Unity menu
   TCG Card Caos → UI → Add Loading Screen
   After you drop the three pngs, run this once so they are assigned and the prefab is updated.
