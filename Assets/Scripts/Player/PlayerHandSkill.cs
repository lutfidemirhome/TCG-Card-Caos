using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Skill 2: pull matching ground items into the hand using the same E pickup flight.
/// Selected normal card → same series. Selected PSA → same PSA slot. Selected pack → one of each pack type.
/// </summary>
public static class PlayerHandSkill
{
    static readonly List<WorldCard> CardScratch = new List<WorldCard>(64);
    static readonly List<WorldBoosterPack> PackScratch = new List<WorldBoosterPack>(16);

    public static bool TryActivate()
    {
        PlayerCardHand hand = PlayerCardHand.Instance;
        if (hand == null || hand.IsHandInputLocked || hand.AvailableSlots <= 0)
            return false;

        if (hand.IsPackSelected)
            return PullMatchingPacks(hand);

        WorldCard selected = hand.SelectedHeldCard;
        if (selected == null)
            return false;

        return selected.UsesPsaSlab
            ? PullMatchingPsa(hand, selected)
            : PullMatchingSeries(hand, selected);
    }

    static bool PullMatchingSeries(PlayerCardHand hand, WorldCard selected)
    {
        if (!CardShelfSeries.TryGetSeriesId(selected.Definition, out string seriesId))
            return false;

        CollectGroundCards(CardScratch, card =>
            !card.UsesPsaSlab
            && CardShelfSeries.TryGetSeriesId(card.Definition, out string other)
            && other == seriesId);

        return PickupCards(hand, CardScratch);
    }

    static bool PullMatchingPsa(PlayerCardHand hand, WorldCard selected)
    {
        int slot = selected.PsaSlotNumber;
        CollectGroundCards(CardScratch, card =>
            card.UsesPsaSlab && card.PsaSlotNumber == slot);

        return PickupCards(hand, CardScratch);
    }

    static bool PullMatchingPacks(PlayerCardHand hand)
    {
        WorldBoosterPack[] allPacks = Object.FindObjectsByType<WorldBoosterPack>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        bool[] heldVariant = new bool[PackArtLibrary.PackVariantCount + 1];
        for (int i = 0; i < allPacks.Length; i++)
        {
            WorldBoosterPack pack = allPacks[i];
            if (pack != null && pack.IsInHand)
                heldVariant[pack.PackVariantIndex] = true;
        }

        PackScratch.Clear();
        bool[] queuedVariant = new bool[PackArtLibrary.PackVariantCount + 1];
        for (int i = 0; i < allPacks.Length; i++)
        {
            WorldBoosterPack pack = allPacks[i];
            if (!IsGroundPack(pack))
                continue;

            int variant = pack.PackVariantIndex;
            if (variant < 1 || variant > PackArtLibrary.PackVariantCount)
                continue;
            if (heldVariant[variant] || queuedVariant[variant])
                continue;

            PackScratch.Add(pack);
            queuedVariant[variant] = true;
        }

        SortPacksByDistance(PackScratch, hand.transform.position);

        bool any = false;
        bool playSound = true;
        for (int i = 0; i < PackScratch.Count && hand.AvailableSlots > 0; i++)
        {
            if (!hand.TryPickupPack(PackScratch[i], playSound))
                continue;

            playSound = false;
            any = true;
        }

        if (any)
            hand.SelectRightmostFanEntry();

        return any;
    }

    static bool PickupCards(PlayerCardHand hand, List<WorldCard> cards)
    {
        SortCardsByDistance(cards, hand.transform.position);

        bool any = false;
        bool playSound = true;
        for (int i = 0; i < cards.Count && hand.AvailableSlots > 0; i++)
        {
            if (!hand.TryPickup(cards[i], playSound))
                continue;

            playSound = false;
            any = true;
        }

        if (any)
            hand.SelectRightmostFanEntry();

        return any;
    }

    static void CollectGroundCards(List<WorldCard> results, System.Predicate<WorldCard> match)
    {
        results.Clear();
        WorldCard[] cards = Object.FindObjectsByType<WorldCard>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < cards.Length; i++)
        {
            WorldCard card = cards[i];
            if (!IsGroundCard(card) || !match(card))
                continue;

            results.Add(card);
        }
    }

    static bool IsGroundCard(WorldCard card)
    {
        if (card == null || card.IsInHand || card.IsFlyingToShelf || card.IsShelfRowCompleteLocked)
            return false;
        if (card.IsPhysicsSimulating)
            return false;
        if (card.GetComponentInParent<CardShelfSlot>() != null)
            return false;
        if (card.GetComponentInParent<PsaCabinetSlot>() != null)
            return false;

        return true;
    }

    static bool IsGroundPack(WorldBoosterPack pack)
    {
        if (pack == null || pack.IsInHand || pack.State == WorldBoosterPack.PackState.FlyingToHand)
            return false;
        if (pack.IsPhysicsSimulating)
            return false;

        return true;
    }

    static void SortCardsByDistance(List<WorldCard> cards, Vector3 origin)
    {
        cards.Sort((a, b) =>
            (a.transform.position - origin).sqrMagnitude.CompareTo(
                (b.transform.position - origin).sqrMagnitude));
    }

    static void SortPacksByDistance(List<WorldBoosterPack> packs, Vector3 origin)
    {
        packs.Sort((a, b) =>
            (a.transform.position - origin).sqrMagnitude.CompareTo(
                (b.transform.position - origin).sqrMagnitude));
    }
}
