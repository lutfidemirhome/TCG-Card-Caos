using System.IO;
using UnityEditor;
using UnityEngine;

public static class CardArtSetup
{
    const string ResourcesCardsFolder = "Assets/Resources/Cards";

    [MenuItem("TCG Card Caos/Setup Card Art")]
    public static void SetupCardArtMenu()
    {
        SetupCardArt();
    }

    [MenuItem("TCG Card Caos/Refresh Card Textures From Templates")]
    public static void RefreshBakedTexturesMenu()
    {
        RefreshBakedTextures();
    }

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

        // Shared back template must keep horizontal U flip — all future card imports reuse these materials.
        CardArtLibrary.ApplyBackTextureUFlip(backMaterialTemplate);

        try
        {
            EditorUtility.DisplayProgressBar("TCG Card Caos", "Baking texture LOD assets...", 0.2f);
            BakeRuntimeTexturesAndMaterials(frontMaterialTemplate, backMaterialTemplate);

            EditorUtility.DisplayProgressBar("TCG Card Caos", "Baking box card mesh...", 0.55f);

            Mesh instancedMesh = CardMeshBuilder.CreatePrototypeInstancedQuad();
            Vector2[] instancedUvs = instancedMesh.uv;
            SaveMeshAsset(instancedMesh, ResourcesCardsFolder + "/InstancedCardMesh.asset");

            Mesh instancedBackMesh = CardMeshBuilder.CreatePrototypeInstancedBackQuad();
            SaveMeshAsset(instancedBackMesh, ResourcesCardsFolder + "/InstancedCardBackMesh.asset");

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

    /// <summary>
    /// Re-bakes Resources card textures/materials from Art/Cards templates (no mesh rebuild).
    /// Called automatically when kart_arka_template or the front template PNG is reimported.
    /// </summary>
    public static void RefreshBakedTextures()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder(ResourcesCardsFolder);

        Material frontMaterialTemplate = AssetDatabase.LoadAssetAtPath<Material>(CardArtLibrary.FrontMaterialAssetPath);
        Material backMaterialTemplate = AssetDatabase.LoadAssetAtPath<Material>(CardArtLibrary.BackMaterialAssetPath);
        if (frontMaterialTemplate == null || backMaterialTemplate == null)
        {
            Debug.LogError(
                "TCG Card Caos: Texture refresh failed. Ensure CardFront.mat and CardBack.mat exist under Assets/Art/Cards.");
            return;
        }

        CardArtLibrary.ApplyBackTextureUFlip(backMaterialTemplate);

        try
        {
            EditorUtility.DisplayProgressBar("TCG Card Caos", "Refreshing card textures...", 0.5f);
            BakeRuntimeTexturesAndMaterials(frontMaterialTemplate, backMaterialTemplate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CardArtLibrary.ResetCache();
            Debug.Log(
                "TCG Card Caos: Refreshed runtime card textures from "
                + CardArtLibrary.FrontTextureAssetPath
                + " and "
                + CardArtLibrary.BackTextureAssetPath
                + ".");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    static void BakeRuntimeTexturesAndMaterials(Material frontMaterialTemplate, Material backMaterialTemplate)
    {
        Texture2D frontWorldTexture = CopyTextureWithMaxSize(
            CardArtLibrary.FrontTextureAssetPath,
            ResourcesCardsFolder + "/card_front_world.png",
            CardTextureSettings.WorldMaxSize);
        Texture2D backWorldTexture = CopyTextureWithMaxSize(
            CardArtLibrary.BackTextureAssetPath,
            ResourcesCardsFolder + "/card_back_world.png",
            CardTextureSettings.DetailMaxSize);
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

        if (materialName.StartsWith("CardBack"))
            CardArtLibrary.ApplyBackTextureUFlip(material);

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
