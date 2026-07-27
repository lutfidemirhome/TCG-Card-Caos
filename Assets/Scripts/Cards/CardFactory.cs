using UnityEngine;
using UnityEngine.Rendering;

public static class CardFactory
{
    public static WorldCard CreateWorldCard(
        Vector3 position,
        Quaternion rotation,
        int paletteIndex,
        int cardDefinitionId,
        string cardName = "Card")
    {
        CardArtLibrary.EnsureLoaded();

        var root = new GameObject(cardName);
        root.transform.SetPositionAndRotation(position, rotation);

        var visualGo = new GameObject("CardVisual");
        visualGo.transform.SetParent(root.transform, false);
        visualGo.transform.localRotation = CardArtLibrary.WorldVisualRotation;

        var meshFilter = visualGo.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CardArtLibrary.CardMesh;

        var meshRenderer = visualGo.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterials = CardArtLibrary.GetCardMaterials(paletteIndex);
        DisableShadows(meshRenderer);

        var collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(CardDimensions.Width, CardDimensions.Thickness, CardDimensions.Height);
        collider.center = Vector3.zero;

        var card = root.AddComponent<WorldCard>();
        card.Initialize(cardDefinitionId, paletteIndex);
        return card;
    }

    public static WorldCard CreateWorldCard(Vector3 position, Quaternion rotation, Color frontColor, string cardName = "Card")
    {
        int paletteIndex = 0;
        for (int i = 0; i < CardPalette.Count; i++)
        {
            if (CardPalette.GetColor(i) == frontColor)
            {
                paletteIndex = i;
                break;
            }
        }

        return CreateWorldCard(position, rotation, paletteIndex, cardDefinitionId: 0, cardName);
    }

    static void DisableShadows(MeshRenderer renderer)
    {
        if (renderer == null)
            return;

        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    public static float GroundHeightOffset()
    {
        return CardDimensions.Thickness * 0.6f + 0.001f;
    }
}
