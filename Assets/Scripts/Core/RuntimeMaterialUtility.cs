using UnityEngine;

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

    public static Material CreateColorMaterial(Color color)
    {
        return CreateSharedColorMaterial(color);
    }
}
