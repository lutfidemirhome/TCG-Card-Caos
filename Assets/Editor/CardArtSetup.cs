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

        Material frontMaterialTemplate = AssetDatabase.LoadAssetAtPath<Material>(CardArtLibrary.FrontMaterialAssetPath);
        Material backMaterialTemplate = AssetDatabase.LoadAssetAtPath<Material>(CardArtLibrary.BackMaterialAssetPath);

        if (frontMaterialTemplate == null || backMaterialTemplate == null)
        {
            Debug.LogError(
                "TCG Card Caos: Card art setup failed. Ensure CardFront.mat and CardBack.mat exist under Assets/Art/Cards.");
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("TCG Card Caos", "Baking texture LOD assets...", 0.2f);

            Texture2D frontWorldTexture = CopyTextureWithMaxSize(
                CardArtLibrary.FrontTextureAssetPath,
                ResourcesCardsFolder + "/card_front_world.png",
                CardTextureSettings.WorldMaxSize);
            Texture2D backWorldTexture = CopyTextureWithMaxSize(
                CardArtLibrary.BackTextureAssetPath,
                ResourcesCardsFolder + "/card_back_world.png",
                CardTextureSettings.WorldMaxSize);
            Texture2D frontDetailTexture = CopyTextureWithMaxSize(
                CardArtLibrary.FrontTextureAssetPath,
                ResourcesCardsFolder + "/card_front_detail.png",
                CardTextureSettings.DetailMaxSize);
            Texture2D backDetailTexture = CopyTextureWithMaxSize(
                CardArtLibrary.BackTextureAssetPath,
                ResourcesCardsFolder + "/card_back_detail.png",
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

            EditorUtility.DisplayProgressBar("TCG Card Caos", "Baking box card mesh...", 0.55f);

            Mesh instancedMesh = CardMeshBuilder.CreatePrototypeInstancedQuad();
            Vector2[] instancedUvs = instancedMesh.uv;
            SaveMeshAsset(instancedMesh, ResourcesCardsFolder + "/InstancedCardMesh.asset");

            EditorUtility.DisplayProgressBar("TCG Card Caos", "Baking detail card mesh...", 0.8f);

            Mesh detailMesh = CardMeshBuilder.CreatePrototypeCardMesh();
            detailMesh.name = "TradingCardMesh";
            SaveMeshAsset(detailMesh, ResourcesCardsFolder + "/TradingCardMesh.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CardArtLibrary.ResetCache();

            Debug.Log(
                "TCG Card Caos: Card art setup complete (1024×1434 box mesh, no FBX). "
                + "World textures: "
                + CardTextureSettings.WorldMaxSize
                + "px. Detail textures: "
                + CardTextureSettings.DetailMaxSize
                + "px. Mesh: "
                + detailMesh.vertexCount
                + " verts, "
                + CardMeshBuilder.CountTriangles(detailMesh)
                + " tris. Size "
                + CardModelDimensions.Width.ToString("0.###")
                + " × "
                + CardModelDimensions.Height.ToString("0.###")
                + " m. Instanced UV: "
                + FormatUv(instancedUvs[0]) + " "
                + FormatUv(instancedUvs[1]) + " "
                + FormatUv(instancedUvs[2]) + " "
                + FormatUv(instancedUvs[3]));
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    static string FormatUv(Vector2 uv)
    {
        return "(" + uv.x.ToString("0.000") + "," + uv.y.ToString("0.000") + ")";
    }

    static Texture2D CopyTextureWithMaxSize(string sourceAssetPath, string destinationAssetPath, int maxTextureSize)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(sourceAssetPath) == null)
        {
            Debug.LogError("TCG Card Caos: Missing texture at " + sourceAssetPath);
            return null;
        }

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
