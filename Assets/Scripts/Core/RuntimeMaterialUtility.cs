using UnityEngine;

static class RuntimeMaterialUtility
{
    public static Material CreateColorMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader);
        material.color = color;
        return material;
    }
}
