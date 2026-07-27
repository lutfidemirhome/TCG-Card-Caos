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
        root.transform.localScale = Vector3.one * CardDimensions.WorldCardScale;

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

    public static float GroundHeightOffset()
    {
        return CardDimensions.Thickness * CardDimensions.WorldCardScale * 0.6f + 0.001f;
    }
}
