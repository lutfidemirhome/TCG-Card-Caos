using System.IO;
using UnityEngine;
using UnityEditor;

public static class MinorKeyPreviewBuilder
{
    [MenuItem("TCG Card Chaos/Minor Skills/Bake Key Inspect Images")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        const string folder = "Assets/UI/Skills/Minor/KeyPreviews";
        Directory.CreateDirectory(folder);
        for (int i = 1; i <= 4; i++)
        {
            string path = $"Assets/Prefabs/MinorSkills/MinorKey{i}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var preview = new PreviewRenderUtility();
            try
            {
                var model = Object.Instantiate(prefab.transform.Find("Visual").gameObject);
                foreach (var outline in model.GetComponentsInChildren<Outline>(true)) Object.DestroyImmediate(outline);
                preview.AddSingleGO(model);
                var renderers = model.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                preview.camera.orthographic = true;
                preview.camera.orthographicSize = bounds.size.x * .58f;
                preview.camera.transform.position = bounds.center + Vector3.up * 3f;
                preview.camera.transform.LookAt(bounds.center, Vector3.left);
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = Color.clear;
                preview.camera.nearClipPlane = .01f;
                preview.camera.farClipPlane = 10;
                preview.ambientColor = new Color(.55f, .55f, .55f);
                preview.lights[0].intensity = 1.4f;
                preview.lights[0].transform.rotation = Quaternion.Euler(65, 20, 15);
                preview.lights[1].intensity = .8f;
                preview.lights[1].transform.rotation = Quaternion.Euler(110, 200, 0);
                preview.BeginPreview(new Rect(0, 0, 512, 768), GUIStyle.none);
                preview.Render(true);
                var rendered = preview.EndPreview();
                var old = RenderTexture.active;
                var copy = RenderTexture.GetTemporary(512, 768, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(rendered, copy);
                RenderTexture.active = copy;
                var png = new Texture2D(512, 768, TextureFormat.RGBA32, false);
                png.ReadPixels(new Rect(0, 0, 512, 768), 0, 0); png.Apply();
                File.WriteAllBytes($"{folder}/Key{i}.png", png.EncodeToPNG());
                Object.DestroyImmediate(png); RenderTexture.active = old; RenderTexture.ReleaseTemporary(copy);
            }
            finally { preview.Cleanup(); }
            string imagePath = $"{folder}/Key{i}.png";
            AssetDatabase.ImportAsset(imagePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.maxTextureSize = 1024; importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var so = new SerializedObject(root.GetComponent<WorldMinorSkillKey>());
                so.FindProperty("inspectSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        File.WriteAllText("Temp/key-preview-build.txt", "Four transparent key images baked and assigned.");
    }
}
