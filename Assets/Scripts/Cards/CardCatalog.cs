using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime lookup for <see cref="CardDefinition"/> assets under Resources/Cards/Definitions.
/// </summary>
public static class CardCatalog
{
    const string ResourcePath = "Cards/Definitions";

    static readonly List<CardDefinition> Definitions = new List<CardDefinition>(64);
    static readonly HashSet<CardDefinition> DefinitionSet = new HashSet<CardDefinition>();
    static readonly Dictionary<string, CardDefinition> ById = new Dictionary<string, CardDefinition>(64);
    static readonly Dictionary<CategorySlotKey, CardDefinition> ByCategorySlot =
        new Dictionary<CategorySlotKey, CardDefinition>(64);

    static bool _loaded;

    readonly struct CategorySlotKey : IEquatable<CategorySlotKey>
    {
        readonly string _categoryId;
        readonly int _slotNumber;

        public CategorySlotKey(string categoryId, int slotNumber)
        {
            _categoryId = categoryId ?? string.Empty;
            _slotNumber = slotNumber;
        }

        public bool Equals(CategorySlotKey other)
        {
            return _slotNumber == other._slotNumber
                && string.Equals(_categoryId, other._categoryId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is CategorySlotKey other && Equals(other);

        public override int GetHashCode()
        {
            return HashCode.Combine(_categoryId, _slotNumber);
        }
    }

    public static int Count
    {
        get
        {
            EnsureLoaded();
            return Definitions.Count;
        }
    }

    public static IReadOnlyList<CardDefinition> All
    {
        get
        {
            EnsureLoaded();
            return Definitions;
        }
    }

    public static int NormalizeSlotNumber(int slotNumber)
    {
        return Mathf.Clamp(slotNumber, CardShelfCategories.MinSlotNumber, CardShelfCategories.MaxSlotNumber);
    }

    public static void EnsureLoaded()
    {
        if (_loaded)
            return;

        LoadFromResources();
    }

    public static void Reload()
    {
        _loaded = false;
        LoadFromResources();
    }

    public static bool TryGetById(string definitionId, out CardDefinition definition)
    {
        EnsureLoaded();
        definition = null;
        if (string.IsNullOrWhiteSpace(definitionId))
            return false;

        return ById.TryGetValue(definitionId, out definition);
    }

    public static bool TryGetByCategorySlot(string categoryId, int slotNumber, out CardDefinition definition)
    {
        EnsureLoaded();
        definition = null;
        if (string.IsNullOrWhiteSpace(categoryId))
            return false;

        slotNumber = NormalizeSlotNumber(slotNumber);
        return ByCategorySlot.TryGetValue(new CategorySlotKey(categoryId, slotNumber), out definition);
    }

    static void LoadFromResources()
    {
        if (_loaded)
            return;

        _loaded = true;
        Definitions.Clear();
        DefinitionSet.Clear();
        ById.Clear();
        ByCategorySlot.Clear();

        CardDefinition[] loaded = Resources.LoadAll<CardDefinition>(ResourcePath);
        for (int i = 0; i < loaded.Length; i++)
        {
            Register(loaded[i]);
        }
    }

    static void Register(CardDefinition definition)
    {
        if (definition == null)
            return;

        if (DefinitionSet.Add(definition))
            Definitions.Add(definition);

        if (!string.IsNullOrWhiteSpace(definition.DefinitionId))
            ById[definition.DefinitionId] = definition;

        if (!string.IsNullOrWhiteSpace(definition.ShelfCategoryId))
        {
            var key = new CategorySlotKey(definition.ShelfCategoryId, definition.ShelfSlotNumber);
            ByCategorySlot[key] = definition;
        }
    }
}
