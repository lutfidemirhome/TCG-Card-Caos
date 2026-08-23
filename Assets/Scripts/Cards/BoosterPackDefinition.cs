using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines which cards can appear when a booster pack is opened.
/// </summary>
[CreateAssetMenu(fileName = "BoosterPackDefinition", menuName = "TCG Card Caos/Booster Pack Definition")]
public class BoosterPackDefinition : ScriptableObject
{
    [Tooltip("When set, only cards from this shelf category can appear. Empty = any catalog card.")]
    [SerializeField] string shelfCategoryId;

    public string ShelfCategoryId => shelfCategoryId;

    public IReadOnlyList<CardDefinition> BuildCardPool()
    {
        CardCatalog.Reload();
        var pool = new List<CardDefinition>(CardCatalog.Count);

        IReadOnlyList<CardDefinition> all = CardCatalog.All;
        for (int i = 0; i < all.Count; i++)
        {
            CardDefinition definition = all[i];
            if (definition == null)
                continue;

            if (!string.IsNullOrWhiteSpace(shelfCategoryId))
            {
                if (definition.ShelfCategoryId != shelfCategoryId)
                    continue;
            }
            else if (!CardScatterUtility.IsLiveGroundCategory(definition.ShelfCategoryId))
            {
                continue;
            }

            pool.Add(definition);
        }

        return pool;
    }
}
