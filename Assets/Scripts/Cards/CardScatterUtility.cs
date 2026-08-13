using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CardScatterUtility
{
    public const int DefaultScatterCount = 100;
    public const int UncommonScatterCount = 50;
    public const int FullScatterCount = DefaultScatterCount + UncommonScatterCount;
    public const int StressTestScatterCount = 5000;
    public const string ScatterRootName = "ScatteredCards";
    public const string TestCardPrefix = "Card_";

    const int CardsPerSpawnFrame = 250;
    const int BulkGridScatterThreshold = 256;
    const string RuntimePlayScatterSessionKey = "TCGCardCaos.RuntimePlayScatterCount";

#if !UNITY_EDITOR
    static int _runtimePlayScatterCount;
#endif

    public static void PrepareRuntimePlayScatter(int count)
    {
#if UNITY_EDITOR
        UnityEditor.SessionState.SetInt(RuntimePlayScatterSessionKey, Mathf.Max(0, count));
#else
        _runtimePlayScatterCount = Mathf.Max(0, count);
#endif
    }

    public static int ConsumeRuntimePlayScatterCount()
    {
#if UNITY_EDITOR
        int count = UnityEditor.SessionState.GetInt(RuntimePlayScatterSessionKey, 0);
        UnityEditor.SessionState.SetInt(RuntimePlayScatterSessionKey, 0);
        return count;
#else
        int count = _runtimePlayScatterCount;
        _runtimePlayScatterCount = 0;
        return count;
#endif
    }

    public static void SpawnScatteredCards(int count = FullScatterCount)
    {
        SpawnScatteredCards(count, shelfCategoryId: null);
    }

    public static void SpawnAllTestCards()
    {
        ClearTestCards();
        SpawnScatteredCards(FullScatterCount, shelfCategoryId: null);
    }

    public static void SpawnScatteredCards(int count, string shelfCategoryId)
    {
        CardCatalog.Reload();
        CardArtLibrary.EnsureLoaded();
        List<CardDefinition> definitions = BuildScatterDefinitions(shelfCategoryId);
        if (definitions.Count == 0)
        {
            Debug.LogError(
                "CardScatterUtility: No CardDefinition assets found"
                + (string.IsNullOrWhiteSpace(shelfCategoryId) ? "." : " for category '" + shelfCategoryId + "'.")
                + " Run TCG Card Caos → Import Normal Common/Uncommon Cards From Art.");
            return;
        }

        CardFactory.InvalidateGroundCache();
        Transform scatterRoot = EnsureScatterRoot();
        float groundY = CardFactory.GroundHeightOffset();
        bool reuseDefinitions = count > definitions.Count;
        int spawnCount = reuseDefinitions ? count : Mathf.Min(count, definitions.Count);
        var positions = GenerateScatterPositions(spawnCount);

        Debug.Log(
            "CardScatterUtility: Spawning "
            + spawnCount
            + " cards (catalog "
            + CardCatalog.Count
            + " definitions"
            + (reuseDefinitions ? ", reusing definitions" : string.Empty)
            + ").");

        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 xz = positions[i];
            var position = new Vector3(xz.x, groundY, xz.y);
            var rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            CardDefinition definition = reuseDefinitions
                ? definitions[i % definitions.Count]
                : definitions[i];

            WorldCard card = CardFactory.CreateWorldCard(
                position,
                rotation,
                definition,
                paletteIndex: 0,
                cardName: TestCardPrefix + definition.DefinitionId,
                ensureArtLoaded: false);

            card.transform.SetParent(scatterRoot, true);
        }

        CardGroundStack.RebuildAll();
    }

    public static IEnumerator SpawnScatteredCardsAsync(int count, string shelfCategoryId = null)
    {
        CardCatalog.Reload();
        CardArtLibrary.EnsureLoaded();
        List<CardDefinition> definitions = BuildScatterDefinitions(shelfCategoryId);
        if (definitions.Count == 0)
        {
            Debug.LogError(
                "CardScatterUtility: No CardDefinition assets found"
                + (string.IsNullOrWhiteSpace(shelfCategoryId) ? "." : " for category '" + shelfCategoryId + "'.")
                + " Run TCG Card Caos → Import Normal Common/Uncommon Cards From Art.");
            yield break;
        }

        CardFactory.InvalidateGroundCache();
        Transform scatterRoot = EnsureScatterRoot();
        float groundY = CardFactory.GroundHeightOffset();
        bool reuseDefinitions = count > definitions.Count;
        int spawnCount = reuseDefinitions ? count : Mathf.Min(count, definitions.Count);
        var positions = GenerateScatterPositions(spawnCount);

        Debug.Log(
            "CardScatterUtility: Spawning "
            + spawnCount
            + " cards at runtime (catalog "
            + CardCatalog.Count
            + " definitions"
            + (reuseDefinitions ? ", reusing definitions" : string.Empty)
            + ").");

        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 xz = positions[i];
            var position = new Vector3(xz.x, groundY, xz.y);
            var rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            CardDefinition definition = reuseDefinitions
                ? definitions[i % definitions.Count]
                : definitions[i];

            WorldCard card = CardFactory.CreateWorldCard(
                position,
                rotation,
                definition,
                paletteIndex: 0,
                cardName: TestCardPrefix + definition.DefinitionId,
                ensureArtLoaded: false);

            card.transform.SetParent(scatterRoot, true);

            if (i > 0 && i % CardsPerSpawnFrame == 0)
                yield return null;
        }
    }

    static List<CardDefinition> BuildScatterDefinitions(string shelfCategoryId = null)
    {
        var definitions = new List<CardDefinition>(CardCatalog.Count);
        IReadOnlyList<CardDefinition> loaded = CardCatalog.All;
        for (int i = 0; i < loaded.Count; i++)
        {
            CardDefinition definition = loaded[i];
            if (definition == null)
                continue;

            if (!string.IsNullOrWhiteSpace(shelfCategoryId)
                && !string.Equals(definition.ShelfCategoryId, shelfCategoryId, System.StringComparison.Ordinal))
            {
                continue;
            }

            definitions.Add(definition);
        }

        definitions.Sort((a, b) => string.CompareOrdinal(a.DefinitionId, b.DefinitionId));
        return definitions;
    }

    public static int CountScatterCards()
    {
        Transform scatterRoot = FindScatterRootTransform();
        if (scatterRoot == null)
            return 0;

        int count = 0;
        for (int i = 0; i < scatterRoot.childCount; i++)
        {
            if (scatterRoot.GetChild(i).GetComponent<WorldCard>() != null)
                count++;
        }

        return count;
    }

    /// <summary>Move scattered cards onto the current floor top (scatter root only — fast for large counts).</summary>
    public static int SnapScatterCardsToFloor()
    {
        Transform scatterRoot = FindScatterRootTransform();
        if (scatterRoot == null)
            return 0;

        CardFactory.InvalidateGroundCache();
        float groundY = CardFactory.GroundHeightOffset();
        int snapped = 0;

        for (int i = 0; i < scatterRoot.childCount; i++)
        {
            WorldCard card = scatterRoot.GetChild(i).GetComponent<WorldCard>();
            if (card == null || card.IsInHand)
                continue;
            if (card.GetComponent<Rigidbody>() != null)
                continue;

            Vector3 p = card.transform.position;
            if (Mathf.Abs(p.y - groundY) < 0.0005f)
            {
                CardGroundStack.Track(card);
                continue;
            }

            p.y = groundY;
            card.transform.position = p;
            CardGroundStack.Track(card);
            snapped++;
        }

        return snapped;
    }

    /// <summary>Move settled world cards onto the current floor top (after placing Floor tiles).</summary>
    public static int SnapCardsToFloor()
    {
        int snapped = SnapScatterCardsToFloor();
        WorldCard[] shelfCards = Object.FindObjectsByType<WorldCard>(FindObjectsSortMode.None);
        CardFactory.InvalidateGroundCache();
        float groundY = CardFactory.GroundHeightOffset();
        for (int i = 0; i < shelfCards.Length; i++)
        {
            WorldCard card = shelfCards[i];
            if (card == null || card.IsInHand || IsScatterCard(card))
                continue;
            if (card.GetComponent<Rigidbody>() != null)
                continue;

            Vector3 p = card.transform.position;
            if (Mathf.Abs(p.y - groundY) < 0.0005f)
                continue;

            p.y = groundY;
            card.transform.position = p;
            CardGroundStack.Track(card);
            snapped++;
        }

        return snapped;
    }

    public static void ClearTestCards()
    {
        Transform scatterRoot = FindScatterRootTransform();
        if (scatterRoot == null)
            return;

        for (int i = scatterRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = scatterRoot.GetChild(i).gameObject;
            if (Application.isPlaying)
                Object.Destroy(child);
            else
                Object.DestroyImmediate(child);
        }

        GameObject rootObject = scatterRoot.gameObject;
        if (Application.isPlaying)
            Object.Destroy(rootObject);
        else
            Object.DestroyImmediate(rootObject);

        CardGroundStack.ClearAll();
        CardGroundQuery.ClearShelfCards();
        CardInteractionFocus.ClearFocus();
    }

    static Transform EnsureScatterRoot()
    {
        Transform existing = FindScatterRootTransform();
        if (existing != null)
            return existing;

        var root = new GameObject(ScatterRootName);
        return root.transform;
    }

    static Transform FindScatterRootTransform()
    {
        GameObject existing = GameObject.Find(ScatterRootName);
        return existing != null ? existing.transform : null;
    }

    static List<Vector2> GenerateScatterPositions(int count)
    {
        // Dense stress tests intentionally overlap so random piles form.
        if (count >= BulkGridScatterThreshold)
            return GeneratePiledScatterPositions(count);

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

    /// <summary>
    /// Random pile centers inside the shop scatter box; several cards land near each center.
    /// </summary>
    static List<Vector2> GeneratePiledScatterPositions(int count)
    {
        int pileCount = Mathf.Clamp(count / 7, 48, 900);
        var pileCenters = new Vector2[pileCount];
        for (int i = 0; i < pileCount; i++)
        {
            pileCenters[i] = new Vector2(
                Random.Range(CardDimensions.ScatterMinX, CardDimensions.ScatterMaxX),
                Random.Range(CardDimensions.ScatterMinZ, CardDimensions.ScatterMaxZ));
        }

        float pileRadius = Mathf.Max(
            CardDimensions.Width,
            CardDimensions.Height) * CardDimensions.WorldCardScale * 0.35f;

        var positions = new List<Vector2>(count);
        for (int i = 0; i < count; i++)
        {
            Vector2 center = pileCenters[Random.Range(0, pileCount)];
            Vector2 offset = Random.insideUnitCircle * pileRadius;
            positions.Add(center + offset);
        }

        return positions;
    }

    static float GetBulkScatterSpacing()
    {
        float diagonal = Mathf.Sqrt(
            CardDimensions.Width * CardDimensions.Width
            + CardDimensions.Height * CardDimensions.Height) * CardDimensions.WorldCardScale;
        return Mathf.Max(CardDimensions.ScatterMinSpacing, diagonal + 0.04f);
    }

    public static bool IsScatterCard(WorldCard card)
    {
        if (card == null)
            return false;

        Transform parent = card.transform.parent;
        while (parent != null)
        {
            if (parent.name == ScatterRootName)
                return true;

            parent = parent.parent;
        }

        return false;
    }

    public static bool IsScatterCardObject(string objectName)
    {
        return objectName.StartsWith(TestCardPrefix) || objectName.StartsWith("TestCard_");
    }

    public static bool SceneNeedsScatterRefresh()
    {
        if (CountScatterCards() < FullScatterCount)
            return true;

        return HasInvalidScatterCards();
    }

    static bool HasInvalidScatterCards()
    {
        Transform scatterRoot = FindScatterRootTransform();
        if (scatterRoot == null)
            return true;

        for (int i = 0; i < scatterRoot.childCount; i++)
        {
            WorldCard card = scatterRoot.GetChild(i).GetComponent<WorldCard>();
            if (card != null && card.Definition == null)
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
        float spacing = GetBulkScatterSpacing();
        int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
        int row = index / columns;
        int column = index % columns;

        float centerX = (CardDimensions.ScatterMinX + CardDimensions.ScatterMaxX) * 0.5f;
        float centerZ = (CardDimensions.ScatterMinZ + CardDimensions.ScatterMaxZ) * 0.5f;
        float startX = centerX - (columns - 1) * spacing * 0.5f;
        float startZ = centerZ - (Mathf.CeilToInt(count / (float)columns) - 1) * spacing * 0.5f;

        return new Vector2(startX + column * spacing, startZ + row * spacing);
    }
}
