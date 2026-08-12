#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class MaterialFixEditor
{
    public static Material GetOrCreateLitMaterial(string path, Color color)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            ApplyBaseColor(existing, color);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader);
        ApplyBaseColor(material, color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static void ApplyBaseColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        material.color = color;
    }
}
#endif
