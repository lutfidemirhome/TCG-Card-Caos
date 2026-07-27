using System.IO;
using UnityEditor;
using UnityEngine;

public static class CardArtSetup
{
    const string ResourcesCardsFolder = "Assets/Resources/Cards";

    public static void SetupCardArt()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder(ResourcesCardsFolder);

        GameObject modelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CardArtLibrary.ModelAssetPath);
        Material frontMaterial = AssetDatabase.LoadAssetAtPath<Material>(CardArtLibrary.FrontMaterialAssetPath);
        Material backMaterial = AssetDatabase.LoadAssetAtPath<Material>(CardArtLibrary.BackMaterialAssetPath);

        if (modelRoot == null || frontMaterial == null || backMaterial == null)
        {
            Debug.LogError(
                "TCG Card Caos: Card art setup failed. Ensure yzma.fbx, CardFront.mat and CardBack.mat exist under Assets/Art/Cards.");
            return;
        }

        MeshFilter meshFilter = modelRoot.GetComponentInChildren<MeshFilter>(true);
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogError("TCG Card Caos: Could not find a mesh inside yzma.fbx.");
            return;
        }

        Mesh readableSource = Object.Instantiate(meshFilter.sharedMesh);
        readableSource.name = meshFilter.sharedMesh.name + "_ReadableCopy";

        try
        {
            EditorUtility.DisplayProgressBar("TCG Card Caos", "Baking instanced ground quad...", 0.35f);

            Mesh instancedMesh = CardMeshBuilder.CreateInstancedGroundCardMesh(readableSource);
            SaveMeshAsset(instancedMesh, ResourcesCardsFolder + "/InstancedCardMesh.asset");

            EditorUtility.DisplayProgressBar("TCG Card Caos", "Baking detail card mesh...", 0.7f);

            Mesh detailMesh = Object.Instantiate(meshFilter.sharedMesh);
            detailMesh.name = "TradingCardMesh";
            SaveMeshAsset(detailMesh, ResourcesCardsFolder + "/TradingCardMesh.asset");

            CopyAssetIfNeeded(CardArtLibrary.FrontMaterialAssetPath, ResourcesCardsFolder + "/CardFront.mat");
            CopyAssetIfNeeded(CardArtLibrary.BackMaterialAssetPath, ResourcesCardsFolder + "/CardBack.mat");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CardArtLibrary.ResetCache();

            Debug.Log(
                "TCG Card Caos: Card art setup complete. Detail mesh: "
                + detailMesh.vertexCount + " verts. Instanced ground quad: "
                + instancedMesh.vertexCount + " verts, "
                + (instancedMesh.triangles.Length / 3) + " tris.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Object.DestroyImmediate(readableSource);
        }
    }

    static void SaveMeshAsset(Mesh mesh, string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null)
            AssetDatabase.DeleteAsset(assetPath);

        AssetDatabase.CreateAsset(mesh, assetPath);
    }

    static void CopyAssetIfNeeded(string sourceAssetPath, string destinationAssetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(destinationAssetPath) != null)
            AssetDatabase.DeleteAsset(destinationAssetPath);

        AssetDatabase.CopyAsset(sourceAssetPath, destinationAssetPath);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
