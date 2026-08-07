using UnityEngine;

public static class CardFactory
{
    const float MaxFloorSurfaceY = 1.25f;
    const float MaxFloorThickness = 0.85f;
    const float MinFloorSpan = 0.75f;

    static float _cachedSurfaceY;
    static int _cachedFrame = -1;
    static bool _hasCachedSurface;

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

    /// <summary>World Y for a flat card resting on the current floor surface.</summary>
    public static float GroundHeightOffset()
    {
        float halfThickness = CardDimensions.Thickness * CardDimensions.WorldCardScale * 0.5f;
        return GroundSurfaceY() + halfThickness + 0.002f;
    }

    /// <summary>Top of the walkable floor mesh/collider (not y=0).</summary>
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
        float best = 0f;
        bool found = false;

        Collider[] colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || !col.enabled || col.isTrigger)
                continue;
            if (col.GetComponentInParent<WorldCard>() != null)
                continue;
            if (col.GetComponentInParent<FirstPersonController>() != null)
                continue;
            if (col is CharacterController)
                continue;

            if (!LooksLikeFloor(col))
                continue;

            float top = col.bounds.max.y;
            if (!found || top > best)
            {
                best = top;
                found = true;
            }
        }

        if (!found)
        {
            // Fallback: renderer bounds for Floor* without waiting on colliders.
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;
                if (!NameLooksLikeFloor(renderer.gameObject.name))
                    continue;

                Bounds b = renderer.bounds;
                if (b.size.y > MaxFloorThickness || b.max.y > MaxFloorSurfaceY)
                    continue;
                if (b.size.x < MinFloorSpan && b.size.z < MinFloorSpan)
                    continue;

                if (!found || b.max.y > best)
                {
                    best = b.max.y;
                    found = true;
                }
            }
        }

        return found ? best : 0f;
    }

    static bool LooksLikeFloor(Collider col)
    {
        Bounds b = col.bounds;
        if (b.max.y > MaxFloorSurfaceY)
            return false;
        if (b.size.y > MaxFloorThickness)
            return false;

        float span = Mathf.Max(b.size.x, b.size.z);
        if (span < MinFloorSpan)
            return false;

        // Prefer explicitly named floor tiles; also accept wide flat colliders (plane ground).
        if (NameLooksLikeFloor(col.gameObject.name))
            return true;

        return b.size.y <= 0.35f && span >= 2f;
    }

    static bool NameLooksLikeFloor(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        return name.StartsWith("Floor", System.StringComparison.OrdinalIgnoreCase)
            || name.Equals("Ground", System.StringComparison.OrdinalIgnoreCase);
    }
}
