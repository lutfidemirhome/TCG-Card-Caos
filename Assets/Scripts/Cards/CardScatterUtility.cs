using UnityEngine;

public static class CardScatterUtility
{
    public const int DefaultScatterCount = 20;

    static readonly Color[] CardColors =
    {
        new Color(0.25f, 0.55f, 0.95f),
        new Color(0.95f, 0.35f, 0.35f),
        new Color(0.35f, 0.85f, 0.45f),
        new Color(0.85f, 0.65f, 0.2f),
        new Color(0.65f, 0.4f, 0.95f),
        new Color(0.95f, 0.55f, 0.15f),
        new Color(0.2f, 0.75f, 0.8f),
        new Color(0.8f, 0.3f, 0.55f),
    };

    public static void SpawnScatteredCards(int count = DefaultScatterCount)
    {
        float groundY = CardFactory.GroundHeightOffset();

        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(-3.2f, 3.2f);
            float z = Random.Range(1.4f, 6.2f);
            var position = new Vector3(x, groundY, z);
            var rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Color color = CardColors[i % CardColors.Length];

            CardFactory.CreateWorldCard(position, rotation, color, "TestCard_" + (i + 1));
        }
    }

    public static void ClearTestCards()
    {
        WorldCard[] cards = Object.FindObjectsByType<WorldCard>(FindObjectsSortMode.None);
        foreach (WorldCard card in cards)
        {
            if (!card.name.StartsWith("TestCard_"))
                continue;

            if (Application.isPlaying)
                Object.Destroy(card.gameObject);
            else
                Object.DestroyImmediate(card.gameObject);
        }
    }
}
