using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Play-mode layout audit. Seats loose English/Japanese cards, PSA slabs, and pack contents
/// into the correct cabinets. Flip <see cref="Enabled"/> to hide the test button.
/// </summary>
public static class CompletionFillTest
{
    public static bool Enabled = false;

    public static string LastSummary { get; private set; } = string.Empty;

    public static void Run()
    {
        if (!Enabled || !Application.isPlaying)
            return;

        int packsOpened = OpenAllPacks();
        List<WorldCard> looseCards = CollectLooseCards();
        SortRegularCards(looseCards);

        int psaPlaced = PlacePsaCards(looseCards);
        int shelfPlaced = PlaceShelfCards(looseCards);

        LeaveLeftoversOnGround(looseCards);
        RefreshCabinetSigns();

        int leftover = CountRemainingLoose();
        int emptyShelf = CountEmptyShelfSlots();
        int emptyPsa = CountEmptyPsaSlots();

        LastSummary =
            "Pack açıldı: " + packsOpened
            + "  |  Raf: " + shelfPlaced
            + "  |  PSA: " + psaPlaced
            + "  |  Yerde kalan: " + leftover
            + "  |  Boş raf: " + emptyShelf
            + "  |  Boş PSA: " + emptyPsa;

        Debug.Log("[CompletionFillTest] " + LastSummary);
    }

    static int OpenAllPacks()
    {
        PlayerCardHand hand = PlayerCardHand.Instance;
        var heldCards = new List<WorldCard>();
        var heldPacks = new List<WorldBoosterPack>();
        if (hand != null)
            hand.DetachAllHeldItems(heldCards, heldPacks);

        WorldBoosterPack[] packs = Object.FindObjectsByType<WorldBoosterPack>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        int opened = 0;
        for (int i = 0; i < packs.Length; i++)
        {
            WorldBoosterPack pack = packs[i];
            if (pack == null)
                continue;

            opened += ExplodePack(pack) ? 1 : 0;
        }

        return opened;
    }

    static bool ExplodePack(WorldBoosterPack pack)
    {
        if (pack == null)
            return false;

        pack.EnsureContentsPreRolled();
        IReadOnlyList<CardDefinition> contents = pack.RollContents(CardDimensions.CardsPerBoosterPack);
        Vector3 position = pack.transform.position;
        Quaternion rotation = pack.transform.rotation;
        Transform parent = pack.transform.parent;

        for (int i = 0; i < contents.Count; i++)
        {
            CardDefinition definition = contents[i];
            if (definition == null)
                continue;

            WorldCard card = CardFactory.CreateWorldCard(
                position,
                rotation,
                definition,
                paletteIndex: 0,
                cardName: "TestPackCard_" + definition.DefinitionId);
            if (parent != null)
                card.transform.SetParent(parent, true);
        }

        Object.Destroy(pack.gameObject);
        return true;
    }

    static List<WorldCard> CollectLooseCards()
    {
        WorldCard[] all = Object.FindObjectsByType<WorldCard>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        var loose = new List<WorldCard>(all.Length);

        for (int i = 0; i < all.Length; i++)
        {
            WorldCard card = all[i];
            if (card == null || card.IsFlyingToShelf)
                continue;
            if (card.GetComponentInParent<CardShelfSlot>() != null)
                continue;
            if (card.GetComponentInParent<PsaCabinetSlot>() != null)
                continue;

            loose.Add(card);
        }

        return loose;
    }

    static void SortRegularCards(List<WorldCard> cards)
    {
        cards.Sort(CompareRegularCards);
    }

    static int CompareRegularCards(WorldCard a, WorldCard b)
    {
        bool aPsa = a != null && a.UsesPsaSlab;
        bool bPsa = b != null && b.UsesPsaSlab;
        if (aPsa != bPsa)
            return aPsa ? -1 : 1;
        if (aPsa)
            return a.PsaSlotNumber.CompareTo(b.PsaSlotNumber);

        string aCat = a != null && a.Definition != null ? a.Definition.ShelfCategoryId : string.Empty;
        string bCat = b != null && b.Definition != null ? b.Definition.ShelfCategoryId : string.Empty;
        int category = string.CompareOrdinal(aCat, bCat);
        if (category != 0)
            return category;

        CardShelfSeries.TryGetSeriesId(a != null ? a.Definition : null, out string aSeries);
        CardShelfSeries.TryGetSeriesId(b != null ? b.Definition : null, out string bSeries);
        int series = string.CompareOrdinal(aSeries ?? string.Empty, bSeries ?? string.Empty);
        if (series != 0)
            return series;

        int aSlot = a != null && a.Definition != null ? a.Definition.ShelfSlotNumber : 0;
        int bSlot = b != null && b.Definition != null ? b.Definition.ShelfSlotNumber : 0;
        return aSlot.CompareTo(bSlot);
    }

    static int PlacePsaCards(List<WorldCard> looseCards)
    {
        PsaCabinetSlot[] slots = Object.FindObjectsByType<PsaCabinetSlot>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        int placed = 0;
        for (int i = 0; i < looseCards.Count; i++)
        {
            WorldCard card = looseCards[i];
            if (card == null || !card.UsesPsaSlab)
                continue;

            PsaCabinetSlot slot = FindEmptyPsaSlot(slots, card);
            if (slot == null)
                continue;

            slot.RestoreOccupiedCard(card, playPlacementFeedback: false);
            looseCards[i] = null;
            placed++;
        }

        return placed;
    }

    static PsaCabinetSlot FindEmptyPsaSlot(PsaCabinetSlot[] slots, WorldCard card)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot == null)
                continue;

            slot.RefreshOccupancy();
            if (slot.IsEmpty && slot.IsCorrectPlacement(card))
                return slot;
        }

        return null;
    }

    static int PlaceShelfCards(List<WorldCard> looseCards)
    {
        CardShelf[] shelves = Object.FindObjectsByType<CardShelf>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < shelves.Length; i++)
        {
            if (shelves[i] != null)
                shelves[i].RefreshSlotCache();
        }

        CardShelfSlot[] slots = Object.FindObjectsByType<CardShelfSlot>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        int placed = 0;
        for (int i = 0; i < looseCards.Count; i++)
        {
            WorldCard card = looseCards[i];
            if (card == null || card.UsesPsaSlab || card.Definition == null)
                continue;

            if (!TryPlaceOnMatchingSlot(card, slots))
                continue;

            looseCards[i] = null;
            placed++;
        }

        return placed;
    }

    static bool TryPlaceOnMatchingSlot(WorldCard card, CardShelfSlot[] slots)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            CardShelfSlot slot = slots[i];
            if (slot == null)
                continue;

            slot.RefreshOccupancy();
            if (!slot.IsEmpty)
                continue;

            CardShelf shelf = slot.GetComponentInParent<CardShelf>();
            if (shelf == null || !shelf.IsCorrectPlacement(card, slot))
                continue;

            slot.RestoreOccupiedCard(card, shelf.SurfacePadding, true, playPlacementFeedback: false);
            return true;
        }

        return false;
    }

    static void LeaveLeftoversOnGround(List<WorldCard> looseCards)
    {
        Transform scatterRoot = GameObject.Find(CardScatterUtility.ScatterRootName)?.transform;

        for (int i = 0; i < looseCards.Count; i++)
        {
            WorldCard card = looseCards[i];
            if (card == null)
                continue;

            Vector3 position = card.transform.position;
            Quaternion rotation = card.transform.rotation;
            card.PlaceOnSurface(scatterRoot, position, rotation);
        }
    }

    static void RefreshCabinetSigns()
    {
        CardShelf[] shelves = Object.FindObjectsByType<CardShelf>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < shelves.Length; i++)
            CabinetSignCompleteOverlay.Refresh(shelves[i]);
    }

    static int CountRemainingLoose()
    {
        return CollectLooseCards().Count;
    }

    static int CountEmptyShelfSlots()
    {
        CardShelfSlot[] slots = Object.FindObjectsByType<CardShelfSlot>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        int empty = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].IsEmpty)
                empty++;
        }

        return empty;
    }

    static int CountEmptyPsaSlots()
    {
        PsaCabinetSlot[] slots = Object.FindObjectsByType<PsaCabinetSlot>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        int empty = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
            {
                slots[i].RefreshOccupancy();
                if (slots[i].IsEmpty)
                    empty++;
            }
        }

        return empty;
    }
}
