using UnityEngine;
using UnityEngine.Rendering;

public static class CardFactory
{
    public static WorldCard CreateWorldCard(Vector3 position, Quaternion rotation, Color frontColor, string cardName = "Card")
    {
        var root = new GameObject(cardName);
        root.transform.SetPositionAndRotation(position, rotation);

        CreateCardBody(root.transform, frontColor);

        var collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(CardDimensions.Width, CardDimensions.Thickness, CardDimensions.Height);
        collider.center = Vector3.zero;

        return root.AddComponent<WorldCard>();
    }

    static void CreateCardBody(Transform root, Color frontColor)
    {
        var border = GameObject.CreatePrimitive(PrimitiveType.Cube);
        border.name = "Border";
        border.transform.SetParent(root, false);
        border.transform.localScale = new Vector3(
            CardDimensions.Width * 1.04f,
            CardDimensions.Thickness,
            CardDimensions.Height * 1.04f);
        DestroyComponent(border.GetComponent<BoxCollider>());
        border.GetComponent<MeshRenderer>().sharedMaterial =
            RuntimeMaterialUtility.CreateColorMaterial(new Color(0.85f, 0.72f, 0.2f));
        DisableShadows(border.GetComponent<MeshRenderer>());

        var face = GameObject.CreatePrimitive(PrimitiveType.Cube);
        face.name = "Face";
        face.transform.SetParent(root, false);
        face.transform.localScale = new Vector3(
            CardDimensions.Width,
            CardDimensions.Thickness * 1.2f,
            CardDimensions.Height);
        DestroyComponent(face.GetComponent<BoxCollider>());
        face.GetComponent<MeshRenderer>().sharedMaterial =
            RuntimeMaterialUtility.CreateColorMaterial(frontColor);
        DisableShadows(face.GetComponent<MeshRenderer>());
    }

    static void DisableShadows(MeshRenderer renderer)
    {
        if (renderer == null)
            return;

        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    static void DestroyComponent(Object component)
    {
        if (component == null)
            return;

        if (Application.isPlaying)
            Object.Destroy(component);
        else
            Object.DestroyImmediate(component);
    }

    public static float GroundHeightOffset()
    {
        return CardDimensions.Thickness * 0.6f + 0.001f;
    }
}
