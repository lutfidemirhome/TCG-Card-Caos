using UnityEngine;
using UnityEngine.Rendering;

static class RuntimeMaterialUtility
{
    public static Material CreateSharedColorMaterial(Color color, bool enableInstancing = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader);
        material.color = color;

        if (enableInstancing)
            material.enableInstancing = true;

        return material;
    }

    public static Material CreateUnlitMaterial(Color color, bool enableInstancing = false, int renderQueue = (int)RenderQueue.Geometry)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        var material = new Material(shader);
        material.color = color;
        material.renderQueue = renderQueue;

        if (enableInstancing)
            material.enableInstancing = true;

        return material;
    }

    public static Material CreateTransparentUnlitMaterial(Color color, bool enableInstancing = false)
    {
        Material material = CreateUnlitMaterial(color, enableInstancing, (int)RenderQueue.Transparent);
        ApplyAlphaBlend(material, color);
        return material;
    }

    public static void ApplyAlphaBlend(Material material, Color color)
    {
        if (material == null)
            return;

        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_SrcBlendAlpha"))
            material.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
        if (material.HasProperty("_DstBlendAlpha"))
            material.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 0);
        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 0f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    public static Material CreateColorMaterial(Color color)
    {
        return CreateSharedColorMaterial(color);
    }
}
