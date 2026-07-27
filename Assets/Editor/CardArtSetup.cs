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
        Material frontMaterialTemplate = AssetDatabase.LoadAssetAtPath<Material>(CardArtLibrary.FrontMaterialAssetPath);
        Material backMaterialTemplate = AssetDatabase.LoadAssetAtPath<Material>(CardArtLibrary.BackMaterialAssetPath);

        if (modelRoot == null || frontMaterialTemplate == null || backMaterialTemplate == null)
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
            EditorUtility.DisplayProgressBar("TCG Card Caos", "Baking texture LOD assets...", 0.2f);

            Texture2D frontWorldTexture = CopyTextureWithMaxSize(
                CardArtLibrary.FrontTextureAssetPath,
                ResourcesCardsFolder + "/yzma_world.png",
                CardTextureSettings.WorldMaxSize);
            Texture2D backWorldTexture = CopyTextureWithMaxSize(
                CardArtLibrary.BackTextureAssetPath,
                ResourcesCardsFolder + "/lorcana_back_world.png",
                CardTextureSettings.WorldMaxSize);
            Texture2D frontDetailTexture = CopyTextureWithMaxSize(
                CardArtLibrary.FrontTextureAssetPath,
                ResourcesCardsFolder + "/yzma_detail.png",
                CardTextureSettings.DetailMaxSize);
            Texture2D backDetailTexture = CopyTextureWithMaxSize(
                CardArtLibrary.BackTextureAssetPath,
                ResourcesCardsFolder + "/lorcana_back_detail.png",
                CardTextureSettings.DetailMaxSize);

            SaveMaterialAsset(
                CreateMaterial(frontMaterialTemplate, frontWorldTexture, "CardFrontWorld", enableInstancing: true),
                ResourcesCardsFolder + "/CardFrontWorld.mat");
            SaveMaterialAsset(
                CreateMaterial(backMaterialTemplate, backWorldTexture, "CardBackWorld", enableInstancing: true),
                ResourcesCardsFolder + "/CardBackWorld.mat");
            SaveMaterialAsset(
                CreateMaterial(frontMaterialTemplate, frontDetailTexture, "CardFrontDetail", enableInstancing: false),
                ResourcesCardsFolder + "/CardFrontDetail.mat");
            SaveMaterialAsset(
                CreateMaterial(backMaterialTemplate, backDetailTexture, "CardBackDetail", enableInstancing: false),
                ResourcesCardsFolder + "/CardBackDetail.mat");

            EditorUtility.DisplayProgressBar("TCG Card Caos", "Baking instanced ground quad...", 0.55f);

            Mesh instancedMesh = CardMeshBuilder.CreateInstancedGroundCardMesh(readableSource);
            Vector2[] instancedUvs = instancedMesh.uv;
            SaveMeshAsset(instancedMesh, ResourcesCardsFolder + "/InstancedCardMesh.asset");

            EditorUtility.DisplayProgressBar("TCG Card Caos", "Baking detail card mesh...", 0.8f);

            Mesh detailMesh = Object.Instantiate(meshFilter.sharedMesh);
            detailMesh.name = "TradingCardMesh";
            SaveMeshAsset(detailMesh, ResourcesCardsFolder + "/TradingCardMesh.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CardArtLibrary.ResetCache();

            Debug.Log(
                "TCG Card Caos: Card art setup complete. World textures: "
                + CardTextureSettings.WorldMaxSize
                + "px. Detail textures: "
                + CardTextureSettings.DetailMaxSize
                + "px. Instanced UV corners: "
                + FormatUv(instancedUvs[0]) + " "
                + FormatUv(instancedUvs[1]) + " "
                + FormatUv(instancedUvs[2]) + " "
                + FormatUv(instancedUvs[3]));
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Object.DestroyImmediate(readableSource);
        }
    }

    static string FormatUv(Vector2 uv)
    {
        return "(" + uv.x.ToString("0.000") + "," + uv.y.ToString("0.000") + ")";
    }

    static Texture2D CopyTextureWithMaxSize(string sourceAssetPath, string destinationAssetPath, int maxTextureSize)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(destinationAssetPath) != null)
            AssetDatabase.DeleteAsset(destinationAssetPath);

        if (!AssetDatabase.CopyAsset(sourceAssetPath, destinationAssetPath))
        {
            Debug.LogError("TCG Card Caos: Failed to copy texture " + sourceAssetPath);
            return null;
        }

        AssetDatabase.ImportAsset(destinationAssetPath, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(destinationAssetPath) as TextureImporter;
        if (importer != null)
        {
            importer.maxTextureSize = maxTextureSize;
            importer.mipmapEnabled = true;

            TextureImporterPlatformSettings platformSettings = importer.GetDefaultPlatformTextureSettings();
            platformSettings.maxTextureSize = maxTextureSize;
            importer.SetPlatformTextureSettings(platformSettings);

            var standaloneSettings = importer.GetPlatformTextureSettings("Standalone");
            standaloneSettings.maxTextureSize = maxTextureSize;
            standaloneSettings.overridden = true;
            importer.SetPlatformTextureSettings(standaloneSettings);

            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(destinationAssetPath);
    }

    static Material CreateMaterial(Material template, Texture2D texture, string materialName, bool enableInstancing)
    {
        var material = new Material(template) { name = materialName };
        if (texture != null)
            material.SetTexture("_BaseMap", texture);

        material.enableInstancing = enableInstancing;
        return material;
    }

    static void SaveMeshAsset(Mesh mesh, string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null)
            AssetDatabase.DeleteAsset(assetPath);

        AssetDatabase.CreateAsset(mesh, assetPath);
    }

    static void SaveMaterialAsset(Material material, string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(assetPath) != null)
            AssetDatabase.DeleteAsset(assetPath);

        AssetDatabase.CreateAsset(material, assetPath);
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
