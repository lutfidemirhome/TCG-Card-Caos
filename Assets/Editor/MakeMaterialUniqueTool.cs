using System.IO;
using UnityEditor;
using UnityEngine;

public static class MakeMaterialUniqueTool
{
    const string MenuPath = "TCG Card Chaos/Make Selected Materials Unique";

    [MenuItem(MenuPath)]
    public static void MakeSelectedMaterialsUnique()
    {
        GameObject[] selection = Selection.gameObjects;
        if (selection.Length == 0)
        {
            EditorUtility.DisplayDialog("Material Unique", "Once bir veya daha fazla obje sec.", "Tamam");
            return;
        }

        int duplicated = 0;
        Undo.SetCurrentGroupName("Make Materials Unique");
        int undoGroup = Undo.GetCurrentGroup();

        for (int i = 0; i < selection.Length; i++)
        {
            Renderer[] renderers = selection[i].GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
                duplicated += MakeRendererMaterialsUnique(renderers[r]);
        }

        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.DisplayDialog(
            "Material Unique",
            duplicated > 0
                ? $"{duplicated} material kopyalandi.\nArtik texture degisikligi sadece secili objeye uygulanir."
                : "Yeni material olusturulmadi. Zaten unique olabilir veya Renderer bulunamadi.",
            "Tamam");
    }

    [MenuItem(MenuPath, true)]
    static bool ValidateMakeSelectedMaterialsUnique() => Selection.gameObjects.Length > 0;

    static int MakeRendererMaterialsUnique(Renderer renderer)
    {
        if (renderer == null)
            return 0;

        Material[] sharedMaterials = renderer.sharedMaterials;
        bool changed = false;
        int duplicated = 0;

        for (int i = 0; i < sharedMaterials.Length; i++)
        {
            Material source = sharedMaterials[i];
            if (source == null || !AssetDatabase.Contains(source))
                continue;

            Material copy = DuplicateMaterialAsset(source, renderer.gameObject.name);
            if (copy == null)
                continue;

            sharedMaterials[i] = copy;
            changed = true;
            duplicated++;
        }

        if (changed)
        {
            Undo.RecordObject(renderer, "Make Materials Unique");
            renderer.sharedMaterials = sharedMaterials;
            EditorUtility.SetDirty(renderer);
        }

        return duplicated;
    }

    static Material DuplicateMaterialAsset(Material source, string ownerName)
    {
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(sourcePath))
            return null;

        string directory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
        string baseName = Path.GetFileNameWithoutExtension(sourcePath);
        string extension = Path.GetExtension(sourcePath);
        string safeOwner = string.IsNullOrWhiteSpace(ownerName) ? "Instance" : ownerName.Replace(' ', '_');

        string targetPath = AssetDatabase.GenerateUniqueAssetPath(
            $"{directory}/{baseName}_{safeOwner}{extension}");

        if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            return null;

        return AssetDatabase.LoadAssetAtPath<Material>(targetPath);
    }
}
