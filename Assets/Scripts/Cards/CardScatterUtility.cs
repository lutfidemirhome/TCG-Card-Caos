using System.Collections.Generic;
using UnityEngine;

public static class CardScatterUtility
{
    public const int DefaultScatterCount = 100;
    public const int StressTestScatterCount = 5000;
    public const string ScatterRootName = "ScatteredCards";
    public const string TestCardPrefix = "TestCard_";

    public static void SpawnScatteredCards(int count = DefaultScatterCount)
    {
        Transform scatterRoot = EnsureScatterRoot();
        float groundY = CardFactory.GroundHeightOffset();
        var positions = GenerateScatterPositions(count);

        for (int i = 0; i < count; i++)
        {
            Vector2 xz = positions[i];
            var position = new Vector3(xz.x, groundY, xz.y);
            var rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            int paletteIndex = i % CardPalette.Count;

            WorldCard card = CardFactory.CreateWorldCard(
                position,
                rotation,
                paletteIndex,
                cardDefinitionId: i,
                cardName: TestCardPrefix + (i + 1));

            card.transform.SetParent(scatterRoot, true);
        }
    }

    public static void ClearTestCards()
    {
        Transform scatterRoot = GameObject.Find(ScatterRootName)?.transform;
        if (scatterRoot != null)
        {
            if (Application.isPlaying)
                Object.Destroy(scatterRoot.gameObject);
            else
                Object.DestroyImmediate(scatterRoot.gameObject);

            return;
        }

        WorldCard[] cards = Object.FindObjectsByType<WorldCard>(FindObjectsSortMode.None);
        foreach (WorldCard card in cards)
        {
            if (!card.name.StartsWith(TestCardPrefix))
                continue;

            if (Application.isPlaying)
                Object.Destroy(card.gameObject);
            else
                Object.DestroyImmediate(card.gameObject);
        }
    }

    static Transform EnsureScatterRoot()
    {
        GameObject existing = GameObject.Find(ScatterRootName);
        if (existing != null)
            return existing.transform;

        var root = new GameObject(ScatterRootName);
        return root.transform;
    }

    static List<Vector2> GenerateScatterPositions(int count)
    {
        var positions = new List<Vector2>(count);
        float minSpacing = CardDimensions.ScatterMinSpacing;
        float minSpacingSq = minSpacing * minSpacing;

        int maxAttempts = Mathf.Max(24, count * 4);
        for (int i = 0; i < count; i++)
        {
            Vector2 candidate = Vector2.zero;
            bool found = false;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                candidate = new Vector2(
                    Random.Range(CardDimensions.ScatterMinX, CardDimensions.ScatterMaxX),
                    Random.Range(CardDimensions.ScatterMinZ, CardDimensions.ScatterMaxZ));

                if (IsFarEnough(candidate, positions, minSpacingSq))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                candidate = GetGridFallbackPosition(i, count);
            }

            positions.Add(candidate);
        }

        return positions;
    }

    static bool IsFarEnough(Vector2 candidate, List<Vector2> existing, float minSpacingSq)
    {
        for (int i = 0; i < existing.Count; i++)
        {
            if ((existing[i] - candidate).sqrMagnitude < minSpacingSq)
                return false;
        }

        return true;
    }

    static Vector2 GetGridFallbackPosition(int index, int count)
    {
        int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
        int row = index / columns;
        int column = index % columns;

        float xSpan = CardDimensions.ScatterMaxX - CardDimensions.ScatterMinX;
        float zSpan = CardDimensions.ScatterMaxZ - CardDimensions.ScatterMinZ;
        float xStep = xSpan / columns;
        float zStep = zSpan / Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));

        return new Vector2(
            CardDimensions.ScatterMinX + (column + 0.5f) * xStep,
            CardDimensions.ScatterMinZ + (row + 0.5f) * zStep);
    }
}
