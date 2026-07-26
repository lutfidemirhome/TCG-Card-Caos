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

    public static Material CreateColorMaterial(Color color)
    {
        return CreateSharedColorMaterial(color);
    }
}
