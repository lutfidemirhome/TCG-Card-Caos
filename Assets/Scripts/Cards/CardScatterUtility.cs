using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CardScatterUtility
{
    public const int DefaultScatterCount = 100;
    public const int UncommonScatterCount = 50;
    public const int RareScatterCount = 30;
    public const int NormalScatterCount = DefaultScatterCount + UncommonScatterCount + RareScatterCount;
    public const int FireCommonScatterCount = 100;
    public const int FireUncommonScatterCount = 50;
    public const int FireRareScatterCount = 30;
    public const int FireScatterCount = FireCommonScatterCount + FireUncommonScatterCount + FireRareScatterCount;
    public const int GrassCommonScatterCount = 100;
    public const int GrassUncommonScatterCount = 50;
    public const int GrassRareScatterCount = 30;
    public const int GrassScatterCount = GrassCommonScatterCount + GrassUncommonScatterCount + GrassRareScatterCount;
    public const int FullScatterCount = NormalScatterCount + FireScatterCount + GrassScatterCount;
    public const int StressTestScatterCount = 5000;
    public const string ScatterRootName = "ScatteredCards";
    public const string TestCardPrefix = "Card_";
    public const int DefaultPackScatterCount = 12;
    public const string TestPackPrefix = "BoosterPack_";

    const int CardsPerSpawnFrame = 250;
    const float GroundFaceDownRatio = 0.2f;
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
                + " Run TCG Card Caos → Import Cards From Art (or Tools/generate_*_card_definitions.py).");
            return;
        }

        CardFactory.InvalidateGroundCache();
        Transform scatterRoot = EnsureScatterRoot();
        float groundY = CardFactory.GroundHeightOffset();
        bool reuseDefinitions = count > definitions.Count;
        int spawnCount = reuseDefinitions ? count : Mathf.Min(count, definitions.Count);
        var positions = GenerateScatterPositions(spawnCount);
        HashSet<int> backFacingIndices = PickBackFacingIndices(spawnCount);

        Debug.Log(
            "CardScatterUtility: Spawning "
            + spawnCount
            + " cards (catalog "
            + CardCatalog.Count
            + " definitions"
            + (reuseDefinitions ? ", reusing definitions" : string.Empty)
            + ", "
            + backFacingIndices.Count
            + " showing back"
            + ").");

        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 xz = positions[i];
            var position = new Vector3(xz.x, groundY, xz.y);
            var rotation = GenerateScatterRotation();
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

            card.SetGroundShowsBack(backFacingIndices.Contains(i));
            card.transform.SetParent(scatterRoot, true);
        }

        SpawnScatteredPacks(scatterRoot, groundY, positions);

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
                + " Run TCG Card Caos → Import Cards From Art (or Tools/generate_*_card_definitions.py).");
            yield break;
        }

        CardFactory.InvalidateGroundCache();
        Transform scatterRoot = EnsureScatterRoot();
        float groundY = CardFactory.GroundHeightOffset();
        bool reuseDefinitions = count > definitions.Count;
        int spawnCount = reuseDefinitions ? count : Mathf.Min(count, definitions.Count);
        var positions = GenerateScatterPositions(spawnCount);
        HashSet<int> backFacingIndices = PickBackFacingIndices(spawnCount);

        Debug.Log(
            "CardScatterUtility: Spawning "
            + spawnCount
            + " cards at runtime (catalog "
            + CardCatalog.Count
            + " definitions"
            + (reuseDefinitions ? ", reusing definitions" : string.Empty)
            + ", "
            + backFacingIndices.Count
            + " showing back"
            + ").");

        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 xz = positions[i];
            var position = new Vector3(xz.x, groundY, xz.y);
            var rotation = GenerateScatterRotation();
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

            card.SetGroundShowsBack(backFacingIndices.Contains(i));
            card.transform.SetParent(scatterRoot, true);

            if (i > 0 && i % CardsPerSpawnFrame == 0)
                yield return null;
        }

        SpawnScatteredPacks(scatterRoot, groundY, positions);
    }

    static void SpawnScatteredPacks(Transform scatterRoot, float groundY, List<Vector2> occupiedCardPositions)
    {
        SpawnScatteredPacks(scatterRoot, groundY, occupiedCardPositions, DefaultPackScatterCount);
    }

    public static void SpawnScatteredPacks(
        Transform scatterRoot,
        float groundY,
        List<Vector2> occupiedCardPositions,
        int count)
    {
        if (count <= 0 || scatterRoot == null)
            return;

        var packPositions = GenerateScatterPositions(count, occupiedCardPositions);
        BoosterPackDefinition definition = Resources.Load<BoosterPackDefinition>("Cards/BoosterPackDefinition");

        for (int i = 0; i < packPositions.Count; i++)
        {
            Vector2 xz = packPositions[i];
            var position = new Vector3(xz.x, groundY, xz.y);
            var rotation = GenerateScatterRotation();

            WorldBoosterPack pack = PackFactory.CreateWorldPack(
                position,
                rotation,
                definition,
                packName: TestPackPrefix + (i + 1).ToString("00"));
            pack.transform.SetParent(scatterRoot, true);
            CardGroundStack.ApplyStackHeight(pack, placeOnTop: true);
        }
    }

    static HashSet<int> PickBackFacingIndices(int spawnCount)
    {
        int backFacingCount = Mathf.Clamp(Mathf.RoundToInt(spawnCount * GroundFaceDownRatio), 0, spawnCount);
        var indices = new int[spawnCount];
        for (int i = 0; i < spawnCount; i++)
            indices[i] = i;

        for (int i = 0; i < backFacingCount; i++)
        {
            int swapIndex = Random.Range(i, spawnCount);
            int temp = indices[i];
            indices[i] = indices[swapIndex];
            indices[swapIndex] = temp;
        }

        var backFacingIndices = new HashSet<int>(backFacingCount);
        for (int i = 0; i < backFacingCount; i++)
            backFacingIndices.Add(indices[i]);

        return backFacingIndices;
    }

    static Quaternion GenerateScatterRotation()
    {
        return Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }

    public static void ApplyGroundFaceDownDistribution()
    {
        Transform scatterRoot = FindScatterRootTransform();
        if (scatterRoot == null)
            return;

        var cards = new List<WorldCard>(scatterRoot.childCount);
        for (int i = 0; i < scatterRoot.childCount; i++)
        {
            WorldCard card = scatterRoot.GetChild(i).GetComponent<WorldCard>();
            if (card != null && !card.IsInHand)
                cards.Add(card);
        }

        if (cards.Count == 0)
            return;

        HashSet<int> backFacingIndices = PickBackFacingIndices(cards.Count);
        for (int i = 0; i < cards.Count; i++)
        {
            float yaw = cards[i].transform.rotation.eulerAngles.y;
            cards[i].transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cards[i].SetGroundShowsBack(backFacingIndices.Contains(i));
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

    /// <summary>
    /// Irregular random scatter across the full zone with a soft minimum spacing
    /// (no neat rows, no intentional piles). Spacing compresses if the zone is full.
    /// </summary>
    static List<Vector2> GenerateScatterPositions(int count, IReadOnlyList<Vector2> avoid = null)
    {
        ScatterRegion region = ScatterRegion.FromScene();
        float width = Mathf.Max(0.01f, region.MaxX - region.MinX);
        float depth = Mathf.Max(0.01f, region.MaxZ - region.MinZ);
        float area = width * depth;

        // Target spacing from preferred card size, then compress if needed to fit count.
        float preferred = GetPreferredScatterSpacing();
        float packingSpacing = Mathf.Sqrt(area / Mathf.Max(1, count));
        float minSpacing = Mathf.Min(preferred, packingSpacing * 0.92f);
        minSpacing = Mathf.Max(0.08f, minSpacing);

        var positions = new List<Vector2>(count);
        int maxAttempts = Mathf.Max(40, count * 12);

        for (int i = 0; i < count; i++)
        {
            Vector2 candidate = Vector2.zero;
            bool found = false;
            float spacing = minSpacing;
            float spacingSq = spacing * spacing;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Soften spacing late so remaining cards still land randomly, not on a grid.
                if (attempt > 0 && attempt % 16 == 0)
                {
                    spacing = Mathf.Max(0.06f, spacing * 0.92f);
                    spacingSq = spacing * spacing;
                }

                candidate = region.RandomXZ();
                if (IsFarEnough(candidate, positions, spacingSq)
                    && IsFarEnough(candidate, avoid, spacingSq))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // Last resort: random point with tiny offset from a random existing card —
                // still irregular, never a row/column lattice.
                candidate = region.RandomXZ();
                if (positions.Count > 0)
                {
                    Vector2 near = positions[Random.Range(0, positions.Count)];
                    candidate = region.Clamp(near + Random.insideUnitCircle * (minSpacing * 0.75f));
                }
                else if (avoid != null && avoid.Count > 0)
                {
                    Vector2 near = avoid[Random.Range(0, avoid.Count)];
                    candidate = region.Clamp(near + Random.insideUnitCircle * (minSpacing * 0.75f));
                }
            }

            positions.Add(region.Clamp(candidate));
        }

        return positions;
    }

    static bool IsFarEnough(Vector2 candidate, IReadOnlyList<Vector2> existing, float minSpacingSq)
    {
        if (existing == null || existing.Count == 0)
            return true;

        for (int i = 0; i < existing.Count; i++)
        {
            if ((existing[i] - candidate).sqrMagnitude < minSpacingSq)
                return false;
        }

        return true;
    }

    static float GetPreferredScatterSpacing()
    {
        float diagonal = Mathf.Sqrt(
            CardDimensions.Width * CardDimensions.Width
            + CardDimensions.Height * CardDimensions.Height) * CardDimensions.GroundCardScale;
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

        if (HasInvalidScatterCards())
            return true;

        // Old pile-based scatters stay spread-broken until refreshed.
        return HasClusteredScatterCards();
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

    static bool HasClusteredScatterCards()
    {
        Transform scatterRoot = FindScatterRootTransform();
        if (scatterRoot == null)
            return true;

        float clusterDist = GetPreferredScatterSpacing() * 0.5f;
        float clusterDistSq = clusterDist * clusterDist;
        var positions = new List<Vector2>(scatterRoot.childCount);
        for (int i = 0; i < scatterRoot.childCount; i++)
        {
            WorldCard card = scatterRoot.GetChild(i).GetComponent<WorldCard>();
            if (card == null)
                continue;

            Vector3 p = card.transform.position;
            positions.Add(new Vector2(p.x, p.z));
        }

        if (positions.Count < 8)
            return false;

        int crowded = 0;
        for (int i = 0; i < positions.Count; i++)
        {
            int neighbors = 0;
            for (int j = 0; j < positions.Count; j++)
            {
                if (i == j)
                    continue;
                if ((positions[i] - positions[j]).sqrMagnitude > clusterDistSq)
                    continue;

                neighbors++;
                if (neighbors >= 3)
                {
                    crowded++;
                    break;
                }
            }
        }

        return crowded > positions.Count / 8;
    }

    readonly struct ScatterRegion
    {
        public readonly float MinX;
        public readonly float MaxX;
        public readonly float MinZ;
        public readonly float MaxZ;
        public readonly CardScatterZone Zone;

        ScatterRegion(float minX, float maxX, float minZ, float maxZ, CardScatterZone zone)
        {
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
            Zone = zone;
        }

        public static ScatterRegion FromScene()
        {
            CardScatterZone zone = CardScatterZone.FindActive();
            if (zone != null)
            {
                zone.GetWorldAabb(out float minX, out float maxX, out float minZ, out float maxZ);
                return new ScatterRegion(minX, maxX, minZ, maxZ, zone);
            }

            return new ScatterRegion(
                CardDimensions.ScatterMinX,
                CardDimensions.ScatterMaxX,
                CardDimensions.ScatterMinZ,
                CardDimensions.ScatterMaxZ,
                null);
        }

        public Vector2 RandomXZ()
        {
            if (Zone != null)
                return Zone.GetRandomXZ();

            return new Vector2(
                Random.Range(MinX, MaxX),
                Random.Range(MinZ, MaxZ));
        }

        public Vector2 Clamp(Vector2 xz)
        {
            if (Zone != null)
                return Zone.ClampXZ(xz);

            return new Vector2(
                Mathf.Clamp(xz.x, MinX, MaxX),
                Mathf.Clamp(xz.y, MinZ, MaxZ));
        }
    }
}
