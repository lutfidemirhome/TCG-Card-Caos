using UnityEngine;

public static class CardFactory
{
    const float MaxFloorSurfaceY = 0.45f;
    const float MaxFloorThickness = 0.85f;
    const float MinFloorSpan = 0.75f;

    static float _cachedSurfaceY;
    static int _cachedFrame = -1;
    static bool _hasCachedSurface;

    public static WorldCard CreateWorldCard(
        Vector3 position,
        Quaternion rotation,
        CardDefinition cardDefinition,
        int paletteIndex,
        string cardName = null,
        bool ensureArtLoaded = true)
    {
        if (ensureArtLoaded)
            CardArtLibrary.EnsureLoaded();

        string resolvedName = cardName;
        if (string.IsNullOrWhiteSpace(resolvedName))
            resolvedName = cardDefinition != null ? cardDefinition.DisplayName : "Card";

        var root = new GameObject(resolvedName);
        CardLayers.ApplyToGameObject(root);
        root.transform.SetPositionAndRotation(position, rotation);
        root.transform.localScale = Vector3.one * CardDimensions.WorldCardScale;

        var collider = root.AddComponent<BoxCollider>();
        CardCollisionUtility.ApplyFlatWorldSize(collider);
        collider.isTrigger = true;
        collider.enabled = false;

        var card = root.AddComponent<WorldCard>();
        card.Initialize(cardDefinition, paletteIndex);
        return card;
    }

    public static WorldCard CreateWorldCard(
        Vector3 position,
        Quaternion rotation,
        int paletteIndex,
        int cardDefinitionId,
        string cardName = "Card")
    {
        CardDefinition definition = ResolveDefinition(cardDefinitionId);
        return CreateWorldCard(position, rotation, definition, paletteIndex, cardName);
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

    /// <summary>World Y for a flat card resting on the current floor surface.</summary>
    public static float GroundHeightOffset()
    {
        float halfThickness = CardDimensions.Thickness * CardDimensions.WorldCardScale * 0.5f;
        return GroundSurfaceY() + halfThickness + 0.002f;
    }

    /// <summary>Top of the walkable floor mesh/collider (not furniture shelves).</summary>
    public static float GroundSurfaceY()
    {
        if (_hasCachedSurface && _cachedFrame == Time.frameCount)
            return _cachedSurfaceY;

        float surfaceY = DetectFloorSurfaceY();
        _cachedSurfaceY = surfaceY;
        _cachedFrame = Time.frameCount;
        _hasCachedSurface = true;
        return surfaceY;
    }

    public static void InvalidateGroundCache()
    {
        _hasCachedSurface = false;
        _cachedFrame = -1;
    }

    static float DetectFloorSurfaceY()
    {
        float bestNamed = float.NegativeInfinity;
        bool foundNamed = false;
        float bestPlane = 0f;
        bool foundPlane = false;

        Collider[] colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
        CardLayers.EnsureInitialized();
        int worldCardLayer = CardLayers.WorldCard;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || !col.enabled || col.isTrigger)
                continue;
            if (col.gameObject.layer == worldCardLayer)
                continue;
            if (ShouldIgnoreGroundCandidate(col))
                continue;

            Bounds b = col.bounds;
            if (b.size.y > MaxFloorThickness || b.max.y > MaxFloorSurfaceY)
                continue;

            float span = Mathf.Max(b.size.x, b.size.z);
            if (span < MinFloorSpan)
                continue;

            string objectName = col.gameObject.name;
            if (NameLooksLikeFloor(objectName))
            {
                if (!foundNamed || b.max.y > bestNamed)
                {
                    bestNamed = b.max.y;
                    foundNamed = true;
                }

                continue;
            }

            // Anonymous ground plane only — never furniture boards (shelves sit higher).
            if (b.size.y <= 0.2f && span >= 2f && b.max.y <= 0.2f)
            {
                if (!foundPlane || b.max.y > bestPlane)
                {
                    bestPlane = b.max.y;
                    foundPlane = true;
                }
            }
        }

        if (foundNamed)
            return bestNamed;

        if (foundPlane)
            return bestPlane;

        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        float bestRenderer = 0f;
        bool foundRenderer = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;
            if (!NameLooksLikeFloor(renderer.gameObject.name))
                continue;
            if (renderer.GetComponentInParent<CardShelf>() != null)
                continue;

            Bounds b = renderer.bounds;
            if (b.size.y > MaxFloorThickness || b.max.y > MaxFloorSurfaceY)
                continue;
            if (b.size.x < MinFloorSpan && b.size.z < MinFloorSpan)
                continue;

            if (!foundRenderer || b.max.y > bestRenderer)
            {
                bestRenderer = b.max.y;
                foundRenderer = true;
            }
        }

        return foundRenderer ? bestRenderer : 0f;
    }

    static bool ShouldIgnoreGroundCandidate(Collider col)
    {
        if (col.GetComponentInParent<WorldCard>() != null)
            return true;
        if (col.GetComponentInParent<FirstPersonController>() != null)
            return true;
        if (col.GetComponentInParent<CardShelf>() != null)
            return true;
        if (col is CharacterController)
            return true;

        string objectName = col.gameObject.name;
        if (objectName.StartsWith("Shelf", System.StringComparison.OrdinalIgnoreCase)
            || objectName.StartsWith("Wall", System.StringComparison.OrdinalIgnoreCase)
            || objectName.StartsWith("Ceiling", System.StringComparison.OrdinalIgnoreCase)
            || objectName.StartsWith("Pillar", System.StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    static bool NameLooksLikeFloor(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        return name.StartsWith("Floor", System.StringComparison.OrdinalIgnoreCase)
            || name.Equals("Ground", System.StringComparison.OrdinalIgnoreCase);
    }

    static CardDefinition ResolveDefinition(int cardDefinitionId)
    {
        string id = CardShelfCategories.NormalCommon + "_" + (cardDefinitionId + 1).ToString("00");
        if (CardCatalog.TryGetById(id, out CardDefinition byId))
            return byId;

        int slot = (cardDefinitionId % CardShelfCategories.SlotsPerRow) + 1;
        if (CardCatalog.TryGetByCategorySlot(CardShelfCategories.NormalCommon, slot, out CardDefinition bySlot))
            return bySlot;

        return null;
    }
}
