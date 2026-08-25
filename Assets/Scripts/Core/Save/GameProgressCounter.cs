using UnityEngine;

/// <summary>
/// Demo HUD and save metadata. Counts only the six demo cabinets, not Mix shelves.
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
        _lockedTotalCards = ResolveDemoCardTotal();
    }

    public static void ClearLockedTotal()
    {
        _lockedTotalCards = -1;
    }

    public static Snapshot Capture()
    {
        DemoShelfTargets.CollectProgress(out int cardsPlaced, out int shelvesCompleted, out int cabinetsCompleted);
        int totalCards = _lockedTotalCards > 0 ? _lockedTotalCards : ResolveDemoCardTotal();

        return new Snapshot(
            cardsPlaced,
            totalCards,
            shelvesCompleted,
            GameHudLimits.MaxShelves,
            cabinetsCompleted,
            GameHudLimits.MaxShelves);
    }

    static int ResolveDemoCardTotal()
    {
        PhysicsLevelLayout layout = PhysicsLevelLayout.FindExisting();
        if (layout != null)
            return layout.DemoOwnedCardTotal;

        return GameHudLimits.MaxPlacedCards;
    }
}
