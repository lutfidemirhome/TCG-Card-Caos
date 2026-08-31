using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// HUD and save metadata. Demo counts the six demo cabinets; full counts every shop cabinet.
/// Progress is cached until gameplay marks the world dirty.
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

    static readonly List<CardShelf> AllShelves = new List<CardShelf>(128);
    static readonly List<PsaCabinet> AllPsaCabinets = new List<PsaCabinet>(8);
    static int _cachedSceneHandle = int.MinValue;
    static int _lockedTotalCards = -1;
    static int _cachedFullCardTotal = -1;
    static bool _cacheValid;
    static Snapshot _cachedSnapshot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _lockedTotalCards = -1;
        _cachedFullCardTotal = -1;
        _cacheValid = false;
        ClearCabinetCache();
    }

    public static void InvalidateCache()
    {
        _cacheValid = false;
    }

    public static void LockTotalFromWorld()
    {
        CaptureTotals(out _, out int totalCards, out _, out _, out _, out _);
        _lockedTotalCards = totalCards;
    }

    public static void ClearLockedTotal()
    {
        _lockedTotalCards = -1;
    }

    public static Snapshot Capture()
    {
        if (!_cacheValid)
            RebuildCache();

        Snapshot snapshot = _cachedSnapshot;
        if (_lockedTotalCards > 0)
            snapshot = new Snapshot(
                snapshot.cardsPlaced,
                _lockedTotalCards,
                snapshot.shelvesCompleted,
                snapshot.totalShelves,
                snapshot.cabinetsCompleted,
                snapshot.totalCabinets);

        return snapshot;
    }

    static void RebuildCache()
    {
        CaptureTotals(
            out int cardsPlaced,
            out int totalCards,
            out int shelvesCompleted,
            out int totalShelves,
            out int cabinetsCompleted,
            out int totalCabinets);

        _cachedSnapshot = new Snapshot(
            cardsPlaced,
            totalCards,
            shelvesCompleted,
            totalShelves,
            cabinetsCompleted,
            totalCabinets);
        _cacheValid = true;
    }

    static void CaptureTotals(
        out int cardsPlaced,
        out int totalCards,
        out int shelvesCompleted,
        out int totalShelves,
        out int cabinetsCompleted,
        out int totalCabinets)
    {
        if (GameBuildVariant.IsDemo)
        {
            DemoShelfTargets.CollectProgress(out cardsPlaced, out shelvesCompleted, out cabinetsCompleted);
            totalCards = ResolveDemoCardTotal();
            totalShelves = GameHudLimits.MaxShelves;
            totalCabinets = GameHudLimits.MaxShelves;
            return;
        }

        ResolveAllCabinets();
        cardsPlaced = 0;
        shelvesCompleted = 0;
        cabinetsCompleted = 0;

        for (int i = 0; i < AllShelves.Count; i++)
        {
            CardShelf shelf = AllShelves[i];
            if (shelf == null)
                continue;

            shelf.CollectHudProgress(out int placed, out bool complete);
            cardsPlaced += placed;
            if (complete)
            {
                shelvesCompleted++;
                cabinetsCompleted++;
            }
        }

        for (int i = 0; i < AllPsaCabinets.Count; i++)
        {
            PsaCabinet cabinet = AllPsaCabinets[i];
            if (cabinet == null)
                continue;

            cabinet.CollectHudProgress(out int placed, out bool complete);
            cardsPlaced += placed;
            if (complete)
            {
                shelvesCompleted++;
                cabinetsCompleted++;
            }
        }

        totalShelves = CountLive(AllShelves) + CountLive(AllPsaCabinets);
        totalCabinets = totalShelves;
        totalCards = ResolveFullCardTotal();
    }

    static int ResolveFullCardTotal()
    {
        if (_cachedFullCardTotal > 0)
            return _cachedFullCardTotal;

        CardCatalog.EnsureLoaded();
        _cachedFullCardTotal = CardCatalog.Count + PsaArtLibrary.CountAllVariants();
        return _cachedFullCardTotal;
    }

    static int ResolveDemoCardTotal()
    {
        PhysicsLevelLayout layout = PhysicsLevelLayout.FindExisting();
        if (layout != null)
            return layout.DemoOwnedCardTotal;

        return GameHudLimits.MaxPlacedCards;
    }

    static void ResolveAllCabinets()
    {
        Scene active = SceneManager.GetActiveScene();
        if (_cachedSceneHandle == active.handle && (AllShelves.Count > 0 || AllPsaCabinets.Count > 0))
            return;

        ClearCabinetCache();
        _cachedSceneHandle = active.handle;

        CardShelf[] shelves = Object.FindObjectsByType<CardShelf>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < shelves.Length; i++)
        {
            if (shelves[i] != null)
                AllShelves.Add(shelves[i]);
        }

        PsaCabinet[] cabinets = Object.FindObjectsByType<PsaCabinet>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < cabinets.Length; i++)
        {
            if (cabinets[i] != null)
                AllPsaCabinets.Add(cabinets[i]);
        }
    }

    static void ClearCabinetCache()
    {
        AllShelves.Clear();
        AllPsaCabinets.Clear();
        _cachedSceneHandle = int.MinValue;
        _cacheValid = false;
    }

    static int CountLive<T>(List<T> items) where T : Object
    {
        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
                count++;
        }

        return count;
    }
}
