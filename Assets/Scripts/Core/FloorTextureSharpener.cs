using UnityEngine;

/// <summary>
/// Supermarket floor tiles are viewed almost edge-on. Default aniso=1 then picks a
/// blurry mip a few metres out, same as ground cards.
/// </summary>
public static class FloorTextureSharpener
{
    const int Aniso = 16;
    const float MipBias = -1f;

    public static void Apply()
    {
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !LooksLikeFloor(renderer))
                continue;

            Material[] materials = renderer.sharedMaterials;
            for (int m = 0; m < materials.Length; m++)
                SharpenMaterial(materials[m]);
        }
    }

    static bool LooksLikeFloor(Renderer renderer)
    {
        string objectName = renderer.gameObject.name;
        if (!string.IsNullOrEmpty(objectName)
            && (objectName.StartsWith("Floor", System.StringComparison.OrdinalIgnoreCase)
                || objectName.Equals("Ground", System.StringComparison.OrdinalIgnoreCase)))
            return true;

        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material != null
                && material.name.IndexOf("TileFloor", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    static void SharpenMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseMap"))
            Sharpen(material.GetTexture("_BaseMap"));
        if (material.HasProperty("_MainTex"))
            Sharpen(material.GetTexture("_MainTex"));
        if (material.HasProperty("_BumpMap"))
            Sharpen(material.GetTexture("_BumpMap"));
    }

    static void Sharpen(Texture texture)
    {
        if (texture == null)
            return;

        if (texture.anisoLevel < Aniso)
            texture.anisoLevel = Aniso;
        texture.mipMapBias = MipBias;
    }
}
