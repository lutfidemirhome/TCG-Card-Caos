using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CardWorldAtlasBuilder
{
    const string Folder = "Assets/Resources/Cards/WorldAtlas";
    const int Size = 4096, TileWidth = 128, TileHeight = 256, Padding = 16;
    const int Columns = Size / TileWidth, PerPage = Columns * (Size / TileHeight);

    [MenuItem("TCG Card Chaos/Diagnostics/Bake Distant Card Atlases")]
    public static void Bake()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play mode before baking.");
        Directory.CreateDirectory(Folder);
        var definitions = AssetDatabase.FindAssets("t:CardDefinition", new[] { "Assets/Resources/Cards/Definitions" })
            .Select(id => AssetDatabase.LoadAssetAtPath<CardDefinition>(AssetDatabase.GUIDToAssetPath(id)))
            .Where(d => d && d.FrontTexture).OrderBy(d => d.DefinitionId, System.StringComparer.Ordinal).ToArray();
        var entries = new List<CardWorldAtlas.Entry>(definitions.Length);
        var pages = new List<Material>();
        var previousRT = RenderTexture.active;
        bool previousSRGB = GL.sRGBWrite;
        const int width = TileWidth - Padding * 2, height = TileHeight - Padding * 2;
        var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var readback = new Texture2D(width, height, TextureFormat.RGB24, false, false);
        rt.Create();
        try
        {
            for (int start = 0; start < definitions.Length; start += PerPage)
            {
                int page = start / PerPage;
                var pixels = new Color32[Size * Size];
                int count = Mathf.Min(PerPage, definitions.Length - start);
                for (int i = 0; i < count; i++)
                {
                    var definition = definitions[start + i];
                    GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                    Graphics.Blit(definition.FrontTexture, rt);
                    RenderTexture.active = rt;
                    readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                    var source = readback.GetPixels32();
                    int x = i % Columns * TileWidth, y = i / Columns * TileHeight;
                    // Extruded edges prevent neighboring art bleeding into ordinary world mip levels.
                    for (int ty = 0; ty < TileHeight; ty++)
                    {
                        int sy = Mathf.Clamp(ty - Padding, 0, height - 1) * width;
                        int dest = (y + ty) * Size + x;
                        for (int tx = 0; tx < TileWidth; tx++)
                            pixels[dest + tx] = source[sy + Mathf.Clamp(tx - Padding, 0, width - 1)];
                    }
                    entries.Add(new CardWorldAtlas.Entry { definitionId = definition.DefinitionId, page = page,
                        uvRect = new Vector4((width - 1f) / Size, (height - 1f) / Size, (x + Padding + .5f) / Size, (y + Padding + .5f) / Size) });
                }
                RenderTexture.active = previousRT;
                var atlas = new Texture2D(Size, Size, TextureFormat.RGB24, false, false);
                atlas.SetPixels32(pixels); atlas.Apply(false);
                string texturePath = $"{Folder}/Page{page:D2}.png";
                File.WriteAllBytes(texturePath, atlas.EncodeToPNG());
                Object.DestroyImmediate(atlas);
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true; importer.alphaSource = TextureImporterAlphaSource.None;
                importer.mipmapEnabled = true; importer.streamingMipmaps = false;
                importer.isReadable = false; importer.maxTextureSize = Size;
                importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4; importer.textureCompression = TextureImporterCompression.CompressedHQ;
                var defaults = importer.GetDefaultPlatformTextureSettings();
                defaults.maxTextureSize = Size; defaults.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SetPlatformTextureSettings(defaults);
                var standalone = importer.GetPlatformTextureSettings("Standalone");
                standalone.maxTextureSize = Size; standalone.overridden = true;
                standalone.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SetPlatformTextureSettings(standalone);
                importer.SaveAndReimport();
                string materialPath = $"{Folder}/Page{page:D2}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (!material) { material = new Material(Shader.Find("TCG/Card Color Preserving")); AssetDatabase.CreateAsset(material, materialPath); }
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                material.SetColor("_BaseColor", Color.white);
                material.EnableKeyword("_CARD_SINGLE_PASS"); material.EnableKeyword("_CARD_WORLD_ATLAS");
                material.enableInstancing = true; material.renderQueue = 2000;
                EditorUtility.SetDirty(material); pages.Add(material);
                File.WriteAllText("Temp/world-atlas-bake.txt", $"Page {page + 1}, cards {entries.Count}/{definitions.Length}");
            }
            string path = Folder + "/Catalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<CardWorldAtlas>(path);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<CardWorldAtlas>(); AssetDatabase.CreateAsset(catalog, path); }
            catalog.pages = pages.ToArray(); catalog.entries = entries.ToArray(); EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/world-atlas-bake.txt", $"COMPLETE: {entries.Count} cards, {pages.Count} pages.");
            Debug.Log($"Baked distant card atlases: {entries.Count} cards, {pages.Count} pages.");
        }
        finally
        {
            RenderTexture.active = previousRT; GL.sRGBWrite = previousSRGB;
            rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(readback);
        }
    }
}
