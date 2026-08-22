using System.Collections.Generic;
using UnityEngine;

public static class PackFactory
{
    public static WorldBoosterPack CreateWorldPack(
        Vector3 position,
        Quaternion rotation,
        BoosterPackDefinition packDefinition = null,
        string packName = "Booster Pack",
        int packVariantIndex = 1,
        IReadOnlyList<CardDefinition> preRolledContents = null)
    {
        CardArtLibrary.EnsureLoaded();

        var root = new GameObject(packName);
        root.transform.SetPositionAndRotation(position, rotation);
        root.transform.localScale = Vector3.one * CardDimensions.GroundCardScale;
        CardLayers.ApplyToGameObject(root);

        var collider = root.AddComponent<BoxCollider>();
        ApplyFlatPackCollider(collider);
        collider.isTrigger = false;

        var pack = root.AddComponent<WorldBoosterPack>();
        pack.Initialize(packDefinition, packVariantIndex, preRolledContents);
        PersistentId.GetOrCreate(root).AssignNew();
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
