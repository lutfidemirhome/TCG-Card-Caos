using UnityEngine;

/// <summary>
/// One cabinet category (e.g. Normal Common). Assign per shelf prefab — holds the category id and sign material.
/// </summary>
[CreateAssetMenu(fileName = "ShelfCategory", menuName = "TCG Card Caos/Shelf Category Definition")]
public class CardShelfCategoryDefinition : ScriptableObject
{
    [SerializeField] string categoryId = CardShelfCategories.NormalCommon;
    [Tooltip("Sign material (front + back quad). Swap Base Map with your category design.")]
    [SerializeField] Material signMaterial;

    public string CategoryId => categoryId;
    public Material SignMaterial => signMaterial;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(categoryId))
            categoryId = name;
    }
#endif
}
