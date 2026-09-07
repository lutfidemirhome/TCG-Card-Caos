using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines which cards can appear when a booster pack is opened.
/// English and Japanese packs never share a card pool.
/// </summary>
[CreateAssetMenu(fileName = "BoosterPackDefinition", menuName = "TCG Card Chaos/Booster Pack Definition")]
public class BoosterPackDefinition : ScriptableObject
{
    [SerializeField] PackCardSet packSet = PackCardSet.English;

    [Tooltip("When set, only cards from this shelf category can appear. Empty = the pack language pool.")]
    [SerializeField] string shelfCategoryId;

    public PackCardSet PackSet => packSet;

    public string ShelfCategoryId => shelfCategoryId;

    public IReadOnlyList<CardDefinition> BuildCardPool()
    {
        CardCatalog.EnsureLoaded();
        bool wantJapanese = packSet == PackCardSet.Japanese;
        var pool = new List<CardDefinition>(CardCatalog.Count);

        IReadOnlyList<CardDefinition> all = CardCatalog.All;
        for (int i = 0; i < all.Count; i++)
        {
            CardDefinition definition = all[i];
            if (definition == null || definition.FrontTexture == null)
                continue;

            if (definition.IsJapanese != wantJapanese)
                continue;

            if (!string.IsNullOrWhiteSpace(shelfCategoryId))
            {
                if (definition.ShelfCategoryId != shelfCategoryId)
                    continue;
            }
            else if (!wantJapanese && !CardScatterUtility.IsLiveGroundCategory(definition.ShelfCategoryId))
            {
                continue;
            }

            pool.Add(definition);
        }

        return pool;
    }
}
