using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Stacks a complete overlay on <c>CategorySign</c> when the cabinet is full.
/// Loads PNGs from Resources/ShelfSigns/Complete/.
/// </summary>
public static class CabinetSignCompleteOverlay
{
    public const string SignObjectName = "CategorySign";
    public const string OverlayObjectName = "CategorySignComplete";
    public const string ResourceFolder = "ShelfSigns/Complete/";

    const float OverlayForwardOffset = 0.004f;

    static Texture2D _tex100;
    static Texture2D _tex50;
    static Texture2D _tex30;
    static bool _texturesLoaded;

    public static void Refresh(CardShelf shelf)
    {
        if (shelf == null)
            return;

        Transform sign = FindNamed(shelf.transform, SignObjectName);
        if (sign == null)
            return;

        bool complete = shelf.IsComplete();
        Transform overlay = sign.Find(OverlayObjectName);

        if (!complete)
        {
            if (overlay != null)
                overlay.gameObject.SetActive(false);
            return;
        }

        Texture2D texture = TextureForSlotsPerRow(shelf.SlotsPerRow);
        if (texture == null)
            return;

        if (overlay == null)
            overlay = CreateOverlay(sign, texture);
        else
            overlay.gameObject.SetActive(true);

        var view = overlay.GetComponent<CabinetSignCompleteView>();
        if (view != null)
            view.Apply(texture, sign.GetComponent<MeshRenderer>());
    }

    static Texture2D TextureForSlotsPerRow(int slotsPerRow)
    {
        EnsureTextures();
        if (slotsPerRow <= 3)
            return _tex30;
        if (slotsPerRow <= 5)
            return _tex50;
        return _tex100;
    }

    static void EnsureTextures()
    {
        if (_texturesLoaded)
            return;

        _texturesLoaded = true;
        _tex100 = Resources.Load<Texture2D>(ResourceFolder + "sign_complete_100");
        _tex50 = Resources.Load<Texture2D>(ResourceFolder + "sign_complete_50");
        _tex30 = Resources.Load<Texture2D>(ResourceFolder + "sign_complete_30");
    }

    static Transform CreateOverlay(Transform sign, Texture2D texture)
    {
        var go = new GameObject(OverlayObjectName);
        go.transform.SetParent(sign, false);
        go.transform.localPosition = Vector3.forward * OverlayForwardOffset;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var sourceFilter = sign.GetComponent<MeshFilter>();
        var filter = go.AddComponent<MeshFilter>();
        if (sourceFilter != null)
            filter.sharedMesh = sourceFilter.sharedMesh;

        var renderer = go.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;

        var view = go.AddComponent<CabinetSignCompleteView>();
        view.Apply(texture, sign.GetComponent<MeshRenderer>());
        return go.transform;
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
