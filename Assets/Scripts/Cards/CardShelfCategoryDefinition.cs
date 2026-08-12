using UnityEngine;

/// <summary>
/// One cabinet category (e.g. Normal Common). Assign per shelf prefab — holds the category id and sign material.
/// </summary>
[CreateAssetMenu(fileName = "ShelfCategory", menuName = "TCG Card Caos/Shelf Category Definition")]
public class CardShelfCategoryDefinition : ScriptableObject
{
    [SerializeField] string categoryId = CardShelfCategories.NormalCommon;
    [Tooltip("How many card seats fit on each shelf row for this cabinet (e.g. 10 = Common, 5 = Uncommon).")]
    [SerializeField] [Range(1, 10)] int slotsPerRow = CardShelfCategories.DefaultSlotsPerRow;
    [Tooltip("Sign material (front + back quad). Swap Base Map with your category design.")]
    [SerializeField] Material signMaterial;
    [Tooltip("Compensates sign mesh non-uniform scale so the label art keeps its aspect ratio.")]
    [SerializeField] Vector2 signBaseMapScale = Vector2.one;
    [SerializeField] Vector2 signBaseMapOffset = Vector2.zero;

    public string CategoryId => categoryId;
    public int SlotsPerRow => UnityEngine.Mathf.Clamp(slotsPerRow, CardShelfCategories.MinSlotNumber, CardShelfCategories.MaxSlotNumber);
    public Material SignMaterial => signMaterial;
    public Vector2 SignBaseMapScale => signBaseMapScale;
    public Vector2 SignBaseMapOffset => signBaseMapOffset;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(categoryId))
            categoryId = name;

        if (slotsPerRow <= 0)
            slotsPerRow = CardShelfCategories.GetDefaultSlotsPerRow(categoryId);
    }
#endif
}
