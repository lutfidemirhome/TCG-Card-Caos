using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ExteriorWallToonEditor
{
    const string ScenePath = "Assets/Scenes/MainScene.unity";

    [MenuItem("TCG Card Caos/Apply Store Wall Shading To Exterior Walls")]
    public static void ApplyToMainScene()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Material wallTemplate = ExteriorWallToonUtility.LoadWallTemplate();
        if (wallTemplate == null)
        {
            EditorUtility.DisplayDialog(
                "Material bulunamadi",
                "Assets/Art/Materials/Wall.mat bulunamadi.",
                "Tamam");
            return;
        }

        int changedSlots = ExteriorWallToonUtility.ApplyAll(wallTemplate, useSharedMaterials: true);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "Dis duvar gölgesi",
            changedSlots > 0
                ? $"Exterior ev duvarlarina magaza duvar gölgesi uygulandi.\n\nGuncellenen material slot: {changedSlots}\nRoom ve Door_c5bu08 dokunulmadi."
                : "Guncellenecek House_*_Wall_* objesi bulunamadi.\n\nHouse_01_Wall_01 sahneye eklendi mi kontrol et.",
            "Tamam");
    }

    [MenuItem("TCG Card Caos/Apply Store Wall Shading To Selected")]
    public static void ApplyToSelected()
    {
        Material wallTemplate = ExteriorWallToonUtility.LoadWallTemplate();
        if (wallTemplate == null)
            return;

        int changedSlots = 0;
        GameObject[] selection = Selection.gameObjects;
        for (int i = 0; i < selection.Length; i++)
        {
            MeshRenderer[] renderers = selection[i].GetComponentsInChildren<MeshRenderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                if (ExteriorWallToonUtility.IsProtectedTransform(renderers[r].transform))
                    continue;

                changedSlots += ExteriorWallToonUtility.ApplyToRenderer(
                    renderers[r],
                    wallTemplate,
                    useSharedMaterials: true);
            }
        }

        if (changedSlots > 0)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"Store wall shading applied to {changedSlots} material slot(s).");
    }
}
