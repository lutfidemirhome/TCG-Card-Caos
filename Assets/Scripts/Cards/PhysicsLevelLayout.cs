using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene root for physically authored card piles. Demo and Main never share folders.
/// </summary>
[DisallowMultipleComponent]
public class PhysicsLevelLayout : MonoBehaviour
{
    public const string RootName = "Physics_Card_Level";
    public const string DemoAreaName = "Demo_Area";
    public const string DemoCardsName = "Demo_Cards";
    public const string DemoVolumeName = "Demo_SpawnVolume";
    public const string MainLevelName = "Main_Level";
    public const string MainVolumeName = "Main_SpawnVolume";
    public const string BatchPrefix = "Batch_";
    public const string MixBatchPrefix = "Mix_";
    public const int MixBatchCount = 10;
    public const int MixPackCount = 100;

    [Header("Demo")]
    [SerializeField] int demoRegularCount = 235;
    [SerializeField] int demoPsaCount = 4;
    [SerializeField] int demoPackCount = 5;

    [Header("Main batches")]
    [SerializeField] int mainBatchSize = 250;
    [SerializeField] int mainPsaCount = 0;
    [SerializeField] int mainPackCount = 0;
    [SerializeField] int currentMainBatchIndex = 1;

    [Header("Spawn")]
    [SerializeField] float spawnPadding = 0.08f;
    [SerializeField] float rotationRandomness = 1f;
    [SerializeField] float heightBias = 0.35f;
    [SerializeField] float minSpawnSpacing = 0.08f;

    [SerializeField] PhysicsCardSpawnVolume demoVolume;
    [SerializeField] PhysicsCardSpawnVolume mainVolume;
    [SerializeField] Transform demoCardsRoot;
    [SerializeField] Transform mainLevelRoot;

    static bool _authoredSuspended;

    public int DemoRegularCount { get => demoRegularCount; set => demoRegularCount = Mathf.Max(0, value); }
    public int DemoPsaCount { get => demoPsaCount; set => demoPsaCount = Mathf.Max(0, value); }
    public int DemoPackCount { get => demoPackCount; set => demoPackCount = Mathf.Max(0, value); }
    public int MainBatchSize { get => mainBatchSize; set => mainBatchSize = Mathf.Max(1, value); }
    public int MainPsaCount { get => mainPsaCount; set => mainPsaCount = Mathf.Max(0, value); }
    public int MainPackCount { get => mainPackCount; set => mainPackCount = Mathf.Max(0, value); }
    public int CurrentMainBatchIndex { get => currentMainBatchIndex; set => currentMainBatchIndex = Mathf.Max(1, value); }
    public float SpawnPadding { get => spawnPadding; set => spawnPadding = Mathf.Clamp01(value); }
    public float RotationRandomness { get => rotationRandomness; set => rotationRandomness = Mathf.Clamp01(value); }
    public float HeightBias { get => heightBias; set => heightBias = Mathf.Max(0f, value); }
    public float MinSpawnSpacing { get => minSpawnSpacing; set => minSpawnSpacing = Mathf.Max(0f, value); }
    public PhysicsCardSpawnVolume DemoVolume
    {
        get => demoVolume;
        set => demoVolume = value;
    }

    public PhysicsCardSpawnVolume MainVolume
    {
        get => mainVolume;
        set => mainVolume = value;
    }
    public Transform DemoCardsRoot => demoCardsRoot;
    public Transform MainLevelRoot => mainLevelRoot;

    public int DemoConfiguredTotal => demoRegularCount + demoPsaCount + demoPackCount;

    /// <summary>HUD / save denominator: floor cards + PSA + unopened pack contents.</summary>
    public int DemoOwnedCardTotal =>
        DemoRegularCount + DemoPsaCount + DemoPackCount * CardDimensions.CardsPerBoosterPack;

    public void BindHierarchy(
        PhysicsCardSpawnVolume demo,
        PhysicsCardSpawnVolume main,
        Transform demoCards,
        Transform mainLevel)
    {
        demoVolume = demo;
        mainVolume = main;
        demoCardsRoot = demoCards;
        mainLevelRoot = mainLevel;
    }

    public static PhysicsLevelLayout FindExisting()
    {
        return Object.FindFirstObjectByType<PhysicsLevelLayout>();
    }

    public static bool HasAuthoredPlayableItems()
    {
        if (_authoredSuspended)
            return false;

        PhysicsLevelItem[] items = Object.FindObjectsByType<PhysicsLevelItem>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        return items != null && items.Length > 0;
    }

    public static void SuspendAuthoredItemsForSaveRestore()
    {
        _authoredSuspended = true;
        SetAuthoredFoldersActive(false);
    }

    public static void RestoreAuthoredItemsAfterFailedLoad()
    {
        _authoredSuspended = false;
        SetAuthoredFoldersActive(true);
    }

    public static void NotifyNewGameUsingAuthoredLayout()
    {
        _authoredSuspended = false;
        SetAuthoredFoldersActive(true);
    }

    static void SetAuthoredFoldersActive(bool active)
    {
        PhysicsLevelLayout layout = FindExisting();
        if (layout == null)
            return;

        if (layout.demoCardsRoot != null)
            layout.demoCardsRoot.gameObject.SetActive(active);

        if (layout.mainLevelRoot == null)
            return;

        for (int i = 0; i < layout.mainLevelRoot.childCount; i++)
        {
            Transform child = layout.mainLevelRoot.GetChild(i);
            if (child != null
                && (child.name.StartsWith(BatchPrefix) || child.name.StartsWith(MixBatchPrefix)))
                child.gameObject.SetActive(active);
        }
    }

    public static WorldCard[] CollectAuthoredWorldCards()
    {
        if (_authoredSuspended)
            return System.Array.Empty<WorldCard>();

        PhysicsLevelItem[] items = Object.FindObjectsByType<PhysicsLevelItem>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        var cards = new List<WorldCard>(items.Length);
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null)
                continue;

            WorldCard card = items[i].GetComponent<WorldCard>();
            if (card != null && !card.IsInHand)
                cards.Add(card);
        }

        return cards.ToArray();
    }

    public static WorldBoosterPack[] CollectAuthoredWorldPacks()
    {
        if (_authoredSuspended)
            return System.Array.Empty<WorldBoosterPack>();

        PhysicsLevelItem[] items = Object.FindObjectsByType<PhysicsLevelItem>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        var packs = new List<WorldBoosterPack>(items.Length);
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null)
                continue;

            WorldBoosterPack pack = items[i].GetComponent<WorldBoosterPack>();
            if (pack != null && !pack.IsInHand)
                packs.Add(pack);
        }

        return packs.ToArray();
    }

    public static string FormatBatchName(int index)
    {
        return BatchPrefix + index.ToString("000");
    }

    public static string FormatMixBatchName(int index)
    {
        return MixBatchPrefix + Mathf.Clamp(index, 1, MixBatchCount).ToString("00");
    }

    public static int MixPacksPerBatch => MixPackCount / MixBatchCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _authoredSuspended = false;
    }
}
