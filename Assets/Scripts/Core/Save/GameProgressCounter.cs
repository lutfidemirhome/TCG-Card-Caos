using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Live world progress for HUD and save metadata. Does not change layout or objects.
/// </summary>
public static class GameProgressCounter
{
    public readonly struct Snapshot
    {
        public readonly int cardsPlaced;
        public readonly int totalCards;
        public readonly int shelvesCompleted;
        public readonly int totalShelves;
        public readonly int cabinetsCompleted;
        public readonly int totalCabinets;

        public Snapshot(
            int cardsPlaced,
            int totalCards,
            int shelvesCompleted,
            int totalShelves,
            int cabinetsCompleted,
            int totalCabinets)
        {
            this.cardsPlaced = cardsPlaced;
            this.totalCards = totalCards;
            this.shelvesCompleted = shelvesCompleted;
            this.totalShelves = totalShelves;
            this.cabinetsCompleted = cabinetsCompleted;
            this.totalCabinets = totalCabinets;
        }
    }

    static int _lockedTotalCards = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _lockedTotalCards = -1;
    }

    public static void LockTotalFromWorld()
    {
        _lockedTotalCards = CountOwnedCardsLive();
    }

    public static void ClearLockedTotal()
    {
        _lockedTotalCards = -1;
    }

    public static Snapshot Capture()
    {
        int cardsPlaced = 0;
        int shelvesCompleted = 0;
        int cabinetsCompleted = 0;
        int totalShelves = GameHudLimits.MaxShelves;
        int totalCabinets = GameHudLimits.MaxShelves;
        int totalCards = _lockedTotalCards > 0 ? _lockedTotalCards : CountOwnedCardsLive();

        var countedPsaRoots = new HashSet<Transform>();

        CardShelf[] shelves = Object.FindObjectsByType<CardShelf>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < shelves.Length; i++)
        {
            CardShelf shelf = shelves[i];
            if (shelf == null)
                continue;

            cardsPlaced += shelf.CountCorrectlyPlacedCards();
            if (shelf.IsComplete())
                shelvesCompleted++;
        }

        PsaCabinet[] cabinets = Object.FindObjectsByType<PsaCabinet>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < cabinets.Length; i++)
        {
            PsaCabinet cabinet = cabinets[i];
            if (cabinet == null || !countedPsaRoots.Add(cabinet.transform))
                continue;

            cardsPlaced += cabinet.CountCorrectlyPlacedCards();
            if (cabinet.IsComplete())
            {
                shelvesCompleted++;
                cabinetsCompleted++;
            }
        }

        CountOrphanPsaUnits(countedPsaRoots, ref cardsPlaced, ref shelvesCompleted, ref cabinetsCompleted);

        return new Snapshot(
            cardsPlaced,
            totalCards,
            shelvesCompleted,
            totalShelves,
            cabinetsCompleted,
            totalCabinets);
    }

    static int CountOwnedCardsLive()
    {
        int total = 0;
        WorldCard[] cards = Object.FindObjectsByType<WorldCard>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] != null)
                total++;
        }

        WorldBoosterPack[] packs = Object.FindObjectsByType<WorldBoosterPack>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < packs.Length; i++)
        {
            WorldBoosterPack pack = packs[i];
            if (pack == null)
                continue;

            IReadOnlyList<CardDefinition> contents = pack.PeekPreRolledContents();
            if (contents != null && contents.Count > 0)
            {
                total += contents.Count;
                continue;
            }

            total += CardDimensions.CardsPerBoosterPack;
        }

        return total;
    }

    static void CountOrphanPsaUnits(
        HashSet<Transform> countedRoots,
        ref int cardsPlaced,
        ref int shelvesCompleted,
        ref int cabinetsCompleted)
    {
        PsaCabinetSlot[] slots = Object.FindObjectsByType<PsaCabinetSlot>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot == null || !PsaArtLibrary.IsCabinetSlotNumber(slot.SlotNumber))
                continue;

            Transform root = ResolvePsaUnitRoot(slot);
            if (root == null || !countedRoots.Add(root))
                continue;

            PsaCabinetSlot[] unitSlots = root.GetComponentsInChildren<PsaCabinetSlot>(true);
            cardsPlaced += CountCorrectPsaSlots(unitSlots);
            if (IsPsaSlotGroupComplete(unitSlots))
            {
                shelvesCompleted++;
                cabinetsCompleted++;
            }
        }
    }

    static Transform ResolvePsaUnitRoot(PsaCabinetSlot slot)
    {
        if (slot == null)
            return null;

        PsaCabinet cabinet = slot.GetComponentInParent<PsaCabinet>();
        if (cabinet != null)
            return cabinet.transform;

        Transform current = slot.transform;
        Transform best = current;
        while (current != null)
        {
            if (CountCabinetSlotsUnder(current) > 0)
                best = current;
            current = current.parent;
        }

        return best;
    }

    static int CountCabinetSlotsUnder(Transform root)
    {
        if (root == null)
            return 0;

        PsaCabinetSlot[] slots = root.GetComponentsInChildren<PsaCabinetSlot>(true);
        int count = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot != null && PsaArtLibrary.IsCabinetSlotNumber(slot.SlotNumber))
                count++;
        }

        return count;
    }

    static int CountCorrectPsaSlots(PsaCabinetSlot[] slots)
    {
        int count = 0;
        if (slots == null)
            return 0;

        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot == null || slot.IsEmpty || !PsaArtLibrary.IsCabinetSlotNumber(slot.SlotNumber))
                continue;

            WorldCard card = slot.OccupiedCard;
            if (card != null && !card.IsInHand && slot.IsCorrectPlacement(card))
                count++;
        }

        return count;
    }

    static bool IsPsaSlotGroupComplete(PsaCabinetSlot[] slots)
    {
        bool hasCabinetSlot = false;
        if (slots == null)
            return false;

        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot == null || !PsaArtLibrary.IsCabinetSlotNumber(slot.SlotNumber))
                continue;

            hasCabinetSlot = true;
            WorldCard card = slot.OccupiedCard;
            if (slot.IsEmpty || card == null || card.IsInHand || !slot.IsCorrectPlacement(card))
                return false;
        }

        return hasCabinetSlot;
    }
}
