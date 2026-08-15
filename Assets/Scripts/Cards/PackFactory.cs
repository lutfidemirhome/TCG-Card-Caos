using UnityEngine;

public static class PackFactory
{
    public static WorldBoosterPack CreateWorldPack(
        Vector3 position,
        Quaternion rotation,
        BoosterPackDefinition packDefinition = null,
        string packName = "Booster Pack")
    {
        CardArtLibrary.EnsureLoaded();

        var root = new GameObject(packName);
        root.transform.SetPositionAndRotation(position, rotation);
        root.transform.localScale = Vector3.one * CardDimensions.GroundCardScale;

        var collider = root.AddComponent<BoxCollider>();
        ApplyFlatPackCollider(collider);
        collider.isTrigger = false;

        var pack = root.AddComponent<WorldBoosterPack>();
        pack.Initialize(packDefinition);
        return pack;
    }

    public static void ApplyFlatPackCollider(BoxCollider collider)
    {
        if (collider == null)
            return;

        collider.center = Vector3.zero;
        collider.size = new Vector3(
            CardDimensions.Width,
            CardDimensions.Thickness,
            CardDimensions.Height);
        CardCollisionUtility.ApplyToCollider(collider);
    }
}
