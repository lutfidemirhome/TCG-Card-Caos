using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Keeps cabinet labels readable from the aisle without sampling full-res mips on every sign.
/// </summary>
public static class CabinetSignSharpener
{
    const int Aniso = 8;
    const float MipBias = -0.85f;

    public static void Apply()
    {
        CardShelf[] shelves = Object.FindObjectsByType<CardShelf>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < shelves.Length; i++)
        {
            CardShelf shelf = shelves[i];
            if (shelf == null)
                continue;

            SharpenNamed(shelf.transform, CabinetSignCompleteOverlay.SignObjectName);
            SharpenNamed(shelf.transform, "CategorySignBack");
        }
    }

    public static void SharpenTexture(Texture texture)
    {
        if (texture == null)
            return;

        texture.filterMode = FilterMode.Bilinear;
        if (texture.anisoLevel < Aniso)
            texture.anisoLevel = Aniso;
        if (texture.mipMapBias > MipBias)
            texture.mipMapBias = MipBias;
    }

    static void SharpenNamed(Transform root, string objectName)
    {
        Transform found = FindNamed(root, objectName);
        if (found == null)
            return;

        var renderer = found.GetComponent<MeshRenderer>();
        if (renderer == null)
            return;

        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;

        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
            SharpenMaterial(materials[i]);
    }

    static void SharpenMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseMap"))
            SharpenTexture(material.GetTexture("_BaseMap"));
        if (material.HasProperty("_MainTex"))
            SharpenTexture(material.GetTexture("_MainTex"));
    }

    static Transform FindNamed(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform nested = FindNamed(parent.GetChild(i), name);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
