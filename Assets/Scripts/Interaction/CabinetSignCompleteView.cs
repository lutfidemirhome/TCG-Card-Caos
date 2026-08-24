using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Runtime overlay quad on a cabinet sign. Destroys its instanced material with the object.
/// </summary>
public sealed class CabinetSignCompleteView : MonoBehaviour
{
    Material _material;

    public void Apply(Texture2D texture, MeshRenderer signRenderer)
    {
        if (texture == null)
            return;

        EnsureMaterial();
        _material.SetTexture("_BaseMap", texture);
        _material.mainTexture = texture;

        if (signRenderer != null && signRenderer.sharedMaterial != null)
            CopyUv(signRenderer.sharedMaterial, _material);

        var renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = _material;
    }

    void OnDestroy()
    {
        if (_material != null)
            Destroy(_material);
        _material = null;
    }

    void EnsureMaterial()
    {
        if (_material != null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");

        _material = new Material(shader)
        {
            name = "CabinetSignComplete",
            color = Color.white,
            renderQueue = (int)RenderQueue.Transparent,
        };

        if (_material.HasProperty("_Surface"))
            _material.SetFloat("_Surface", 1f);
        _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _material.SetOverrideTag("RenderType", "Transparent");

        if (_material.HasProperty("_SrcBlend"))
            _material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (_material.HasProperty("_DstBlend"))
            _material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (_material.HasProperty("_ZWrite"))
            _material.SetFloat("_ZWrite", 0f);
        if (_material.HasProperty("_Cull"))
            _material.SetFloat("_Cull", 0f);
        if (_material.HasProperty("_AlphaClip"))
            _material.SetFloat("_AlphaClip", 0f);
    }

    static void CopyUv(Material source, Material dest)
    {
        if (source.HasProperty("_BaseMap") && dest.HasProperty("_BaseMap"))
        {
            dest.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
            dest.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
        }

        dest.mainTextureScale = source.mainTextureScale;
        dest.mainTextureOffset = source.mainTextureOffset;
    }
}
