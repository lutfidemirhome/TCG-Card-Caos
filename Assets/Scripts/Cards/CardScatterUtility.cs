using System.Collections.Generic;
using UnityEngine;

public static class CardScatterUtility
{
    public const int DefaultScatterCount = 100;
    public const int StressTestScatterCount = 5000;
    public const string ScatterRootName = "ScatteredCards";
    public const string TestCardPrefix = "Card_";

    public static void SpawnScatteredCards(int count = DefaultScatterCount)
    {
        CardCatalog.Reload();
        List<CardDefinition> definitions = BuildScatterDefinitions();
        if (definitions.Count == 0)
        {
            Debug.LogError(
                "CardScatterUtility: No CardDefinition assets found. "
                + "Run Tools/generate_card_definitions.py or TCG Card Caos → Import Normal Common Cards From Art.");
            return;
        }

        CardFactory.InvalidateGroundCache();
        Transform scatterRoot = EnsureScatterRoot();
        float groundY = CardFactory.GroundHeightOffset();
        int spawnCount = Mathf.Min(count, definitions.Count);
        var positions = GenerateScatterPositions(spawnCount);

        Debug.Log(
            "CardScatterUtility: Spawning "
            + spawnCount
            + " cards (catalog "
            + CardCatalog.Count
            + " definitions).");

        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 xz = positions[i];
            var position = new Vector3(xz.x, groundY, xz.y);
            var rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            CardDefinition definition = definitions[i];

            WorldCard card = CardFactory.CreateWorldCard(
                position,
                rotation,
                definition,
                paletteIndex: 0,
                cardName: TestCardPrefix + definition.DefinitionId);

            card.transform.SetParent(scatterRoot, true);
        }
    }

    static List<CardDefinition> BuildScatterDefinitions()
    {
        var definitions = new List<CardDefinition>(CardCatalog.Count);
        IReadOnlyList<CardDefinition> loaded = CardCatalog.All;
        for (int i = 0; i < loaded.Count; i++)
        {
            CardDefinition definition = loaded[i];
            if (definition != null)
                definitions.Add(definition);
        }

        definitions.Sort((a, b) => string.CompareOrdinal(a.DefinitionId, b.DefinitionId));
        return definitions;
    }

    public static int CountScatterCards()
    {
        int count = 0;
        WorldCard[] cards = Object.FindObjectsByType<WorldCard>(FindObjectsSortMode.None);
        for (int i = 0; i < cards.Length; i++)
        {
            WorldCard card = cards[i];
            if (card != null && IsScatterCard(card.name))
                count++;
        }

        return count;
    }

    /// <summary>Move settled world cards onto the current floor top (after placing Floor tiles).</summary>
    public static int SnapCardsToFloor()
    {
        CardFactory.InvalidateGroundCache();
        float groundY = CardFactory.GroundHeightOffset();
        int snapped = 0;

        WorldCard[] cards = Object.FindObjectsByType<WorldCard>(FindObjectsSortMode.None);
        for (int i = 0; i < cards.Length; i++)
        {
            WorldCard card = cards[i];
            if (card == null || card.IsInHand)
                continue;
            if (card.GetComponent<Rigidbody>() != null)
                continue;

            Vector3 p = card.transform.position;
            if (Mathf.Abs(p.y - groundY) < 0.0005f)
                continue;

            p.y = groundY;
            card.transform.position = p;
            CardGroundStack.ApplyStackHeight(card);
            snapped++;
        }

        return snapped;
    }

    public static void ClearTestCards()
    {
        WorldCard[] cards = Object.FindObjectsByType<WorldCard>(FindObjectsSortMode.None);
        for (int i = 0; i < cards.Length; i++)
        {
            WorldCard card = cards[i];
            if (card == null || !IsScatterCard(card.name))
                continue;

            if (Application.isPlaying)
                Object.Destroy(card.gameObject);
            else
                Object.DestroyImmediate(card.gameObject);
        }

        GameObject scatterRoot = GameObject.Find(ScatterRootName);
        if (scatterRoot == null)
            return;

        if (Application.isPlaying)
            Object.Destroy(scatterRoot);
        else
            Object.DestroyImmediate(scatterRoot);
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
                candidate = GetGridFallbackPosition(i, count);

            positions.Add(candidate);
        }

        return positions;
    }

    static bool IsScatterCard(string objectName)
    {
        return objectName.StartsWith(TestCardPrefix) || objectName.StartsWith("TestCard_");
    }

    public static bool IsScatterCardObject(string objectName) => IsScatterCard(objectName);

    public static bool SceneNeedsScatterRefresh()
    {
        if (CountScatterCards() < DefaultScatterCount)
            return true;

        return HasInvalidScatterCards();
    }

    static bool HasInvalidScatterCards()
    {
        WorldCard[] cards = Object.FindObjectsByType<WorldCard>(FindObjectsSortMode.None);
        for (int i = 0; i < cards.Length; i++)
        {
            WorldCard card = cards[i];
            if (card == null || !IsScatterCard(card.name))
                continue;

            if (card.Definition == null)
                return true;
        }

        return false;
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
