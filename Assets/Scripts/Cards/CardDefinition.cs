using UnityEngine;

/// <summary>
/// Authoring asset for one playable card.
/// Assign shelf category + fixed slot (1–10, left → right on a row).
/// Create via Assets → Create → TCG Card Chaos → Card Definition.
/// </summary>
[CreateAssetMenu(fileName = "CardDefinition", menuName = "TCG Card Chaos/Card Definition")]
public class CardDefinition : ScriptableObject
{
    [SerializeField] string definitionId = "normal_common_01";
    [SerializeField] string displayName = "Normal Common 1";
    [SerializeField] string shelfCategoryId = CardShelfCategories.NormalCommon;
    [SerializeField] [Range(1, 10)] int shelfSlotNumber = 1;

    [Header("Art (assign when ready)")]
    [SerializeField] Texture2D frontTexture;
    [SerializeField] Sprite categorySymbol;

    public string DefinitionId => definitionId;
    public string DisplayName => displayName;
    public string ShelfCategoryId => shelfCategoryId;
    public int ShelfSlotNumber => CardCatalog.NormalizeSlotNumber(shelfSlotNumber);
    public Texture2D FrontTexture => frontTexture;
    public Sprite CategorySymbol => categorySymbol;

#if UNITY_EDITOR
    void OnValidate()
    {
        shelfSlotNumber = CardCatalog.NormalizeSlotNumber(shelfSlotNumber);
        if (string.IsNullOrWhiteSpace(definitionId))
            definitionId = name;
    }
#endif
}
