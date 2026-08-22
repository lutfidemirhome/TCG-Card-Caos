using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// GPU-instanced draws for static world cards that share mesh/material batches.
/// Definition-art cards batch by <see cref="CardDefinition.DefinitionId"/>; face-down cards share one back batch.
/// </summary>
public class CardInstancedRenderManager : MonoBehaviour
{
    public const string BackBatchKey = "__back__";
    public const string PaletteBatchPrefix = "palette:";

    const int MaxInstancesPerBatch = 1023;
    const int CardsRegisteredPerFrame = 32;

    [SerializeField] float drawDistance = 40f;

    static CardInstancedRenderManager _instance;

    readonly Dictionary<string, HashSet<WorldCard>> _cardsByBatchKey = new Dictionary<string, HashSet<WorldCard>>(160);
    readonly Dictionary<WorldCard, string> _batchKeyByCard = new Dictionary<WorldCard, string>(2048);
    readonly Matrix4x4[] _matrixBuffer = new Matrix4x4[MaxInstancesPerBatch];
    readonly List<string> _scratchBatchKeys = new List<string>(160);
    readonly List<WorldCard> _drawSortScratch = new List<WorldCard>(2048);
    readonly Plane[] _frustumPlanes = new Plane[6];

    Camera _camera;
    float _drawDistanceSq;
    Coroutine _playModeSetupRoutine;

    public static CardInstancedRenderManager Instance => _instance;

    public static bool DeferGroundRegistration { get; private set; }

    public static bool IsGameplayReady { get; private set; }

    public static void ResetGameplayReady()
    {
        IsGameplayReady = false;
    }

    public static void BeginBulkGroundLoad()
    {
        DeferGroundRegistration = true;
    }

    public void SchedulePlayModeSetup()
    {
        if (_playModeSetupRoutine != null)
            StopCoroutine(_playModeSetupRoutine);

        _playModeSetupRoutine = StartCoroutine(PlayModeSetupRoutine());
    }

    IEnumerator PlayModeSetupRoutine()
    {
        yield return null;

        CardArtLibrary.EnsureLoaded();
        GameSaveManager.EnsureExists();

        GameLoadMode loadMode = GameSceneLoader.PendingLoadMode;
        string pendingSlotId = GameSceneLoader.PendingSlotId;
        bool restoreFromSave = loadMode == GameLoadMode.Continue || loadMode == GameLoadMode.LoadSlot;

        if (restoreFromSave)
        {
            if (string.IsNullOrEmpty(pendingSlotId) && loadMode == GameLoadMode.Continue)
            {
                SaveSlotMetadata latest = GameSaveManager.GetLatestValidSave();
                pendingSlotId = latest != null ? latest.slotId : null;
            }

            if (!string.IsNullOrEmpty(pendingSlotId))
            {
                yield return GameSaveRestore.RestoreRoutine(pendingSlotId);
                if (GameSaveRestore.LastRestoreSucceeded)
                    GameSaveManager.NotifySaveRestored();
                else
                {
                    yield return SpawnNewWorldRoutine();
                    GameSaveManager.NotifyNewGameWorldReady();
                }
            }
            else
            {
                Debug.LogWarning("[Save] Continue/Load requested but no valid slot was found. Starting a new scatter.");
                yield return SpawnNewWorldRoutine();
                GameSaveManager.NotifyNewGameWorldReady();
            }
        }
        else
        {
            yield return SpawnNewWorldRoutine();
            GameSaveManager.NotifyNewGameWorldReady();
        }

        GameSceneLoader.ClearPendingLoad();
        DeferGroundRegistration = false;
        yield return RegisterAllGroundCardsRoutine();
        yield return CardGroundStack.RebuildAllAsync();
        yield return null;

        Debug.Log(
            "TCG Card Caos: Play mode card setup complete ("
            + (CardScatterUtility.CountScatterCards() - CardScatterUtility.CountScatterPsaCards())
            + " ground cards + "
            + CardScatterUtility.CountScatterPacks()
            + " packs + "
            + CardScatterUtility.CountScatterPsaCards()
            + " PSA cards).");

        IsGameplayReady = true;
        _playModeSetupRoutine = null;
    }

    IEnumerator SpawnNewWorldRoutine()
    {
        int runtimeTarget = CardScatterUtility.ConsumeRuntimePlayScatterCount();
        if (runtimeTarget > 0)
        {
            yield return CardScatterUtility.SpawnScatteredCardsAsync(runtimeTarget);
            yield break;
        }

        CardScatterUtility.ClearTestCards();
        yield return CardScatterUtility.SpawnScatteredCardsAsync(CardScatterUtility.FullScatterCount);
    }

    IEnumerator RegisterAllGroundCardsRoutine()
    {
        Transform scatterRoot = GameObject.Find(CardScatterUtility.ScatterRootName)?.transform;
        if (scatterRoot == null)
            yield break;

        int processed = 0;
        for (int i = 0; i < scatterRoot.childCount; i++)
        {
            WorldCard card = scatterRoot.GetChild(i).GetComponent<WorldCard>();
            if (card == null || card.IsInHand)
                continue;

            card.RegisterForInstancedGround();
            processed++;
            if (processed % CardsRegisteredPerFrame == 0)
                yield return null;
        }
    }

    public static CardInstancedRenderManager EnsureExists()
    {
        if (_instance != null)
        {
            _instance.EnsureBucketsInitialized();
            return _instance;
        }

        var existing = FindFirstObjectByType<CardInstancedRenderManager>();
        if (existing != null)
        {
            _instance = existing;
            _instance.EnsureBucketsInitialized();
            return _instance;
        }

        var managerObject = new GameObject(nameof(CardInstancedRenderManager));
        var created = managerObject.AddComponent<CardInstancedRenderManager>();
        created.EnsureBucketsInitialized();
        return created;
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        CacheDistances();
        EnsureBucketsInitialized();
    }

    void EnsureBucketsInitialized()
    {
        if (_drawDistanceSq <= 0f)
            CacheDistances();
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    void OnValidate()
    {
        CacheDistances();
    }

    void CacheDistances()
    {
        drawDistance = Mathf.Max(4f, drawDistance);
        _drawDistanceSq = drawDistance * drawDistance;
    }

    void LateUpdate()
    {
        if (_camera == null)
            _camera = Camera.main;

        if (_camera != null)
            GeometryUtility.CalculateFrustumPlanes(_camera, _frustumPlanes);

        DrawInstancedCards();
        CullGroundPackRenderers();
    }

    public void Register(WorldCard card)
    {
        if (card == null || card.IsInHand)
            return;

        EnsureBucketsInitialized();
        CardGroundStack.Track(card);

        if (!card.CanUseInstancedRendering && !card.CanUseInstancedBackRendering)
            return;

        string batchKey = card.GetInstancedBatchKey();
        if (!_cardsByBatchKey.TryGetValue(batchKey, out HashSet<WorldCard> bucket))
        {
            bucket = new HashSet<WorldCard>();
            _cardsByBatchKey[batchKey] = bucket;
        }

        if (_batchKeyByCard.TryGetValue(card, out string previousKey) && previousKey != batchKey)
        {
            if (_cardsByBatchKey.TryGetValue(previousKey, out HashSet<WorldCard> previousBucket))
                previousBucket.Remove(card);
        }

        bucket.Add(card);
        _batchKeyByCard[card] = batchKey;
    }

    public void Unregister(WorldCard card)
    {
        if (card == null)
            return;

        EnsureBucketsInitialized();
        if (!_batchKeyByCard.TryGetValue(card, out string batchKey))
            return;

        if (_cardsByBatchKey.TryGetValue(batchKey, out HashSet<WorldCard> bucket))
            bucket.Remove(card);

        _batchKeyByCard.Remove(card);
    }

    public static void ReleaseFromGround(WorldCard card)
    {
        Instance?.Unregister(card);
        CardGroundStack.Untrack(card);
    }

    void DrawInstancedCards()
    {
        if (_camera == null)
            return;

        EnsureBucketsInitialized();
        CardArtLibrary.EnsureLoaded();

        Mesh frontMesh = CardArtLibrary.InstancedCardMesh;
        Mesh backMesh = CardArtLibrary.InstancedCardBackMesh;
        if (frontMesh == null)
            return;

        _scratchBatchKeys.Clear();

        foreach (KeyValuePair<string, HashSet<WorldCard>> pair in _cardsByBatchKey)
        {
            if (pair.Value != null && pair.Value.Count > 0)
                _scratchBatchKeys.Add(pair.Key);
        }

        for (int i = 0; i < _scratchBatchKeys.Count; i++)
        {
            string batchKey = _scratchBatchKeys[i];
            if (!_cardsByBatchKey.TryGetValue(batchKey, out HashSet<WorldCard> cards))
                continue;

            bool backFace = batchKey == BackBatchKey;
            Material material = ResolveBatchMaterial(batchKey);
            Mesh mesh = backFace ? backMesh : frontMesh;
            if (material == null || mesh == null)
                continue;

            _drawSortScratch.Clear();
            foreach (WorldCard card in cards)
            {
                if (card == null)
                    continue;
                if (backFace ? !card.CanUseInstancedBackRendering : !card.CanUseInstancedRendering)
                    continue;
                if (!ShouldRenderCard(card))
                    continue;

                _drawSortScratch.Add(card);
            }

            if (_drawSortScratch.Count == 0)
                continue;

            if (_drawSortScratch.Count <= 256)
                _drawSortScratch.Sort(CompareDrawOrder);

            int writeIndex = 0;
            for (int c = 0; c < _drawSortScratch.Count; c++)
            {
                _matrixBuffer[writeIndex++] = _drawSortScratch[c].GetInstancedDrawMatrix();
                if (writeIndex < MaxInstancesPerBatch)
                    continue;

                DrawBatch(mesh, material, _matrixBuffer, writeIndex);
                writeIndex = 0;
            }

            if (writeIndex > 0)
                DrawBatch(mesh, material, _matrixBuffer, writeIndex);
        }
    }

    static Material ResolveBatchMaterial(string batchKey)
    {
        if (batchKey == BackBatchKey)
            return CardArtLibrary.GetInstancedGroundBackMaterial();

        if (batchKey.StartsWith(PaletteBatchPrefix))
        {
            string paletteText = batchKey.Substring(PaletteBatchPrefix.Length);
            if (int.TryParse(paletteText, out int paletteIndex))
                return CardArtLibrary.GetFrontMaterial(paletteIndex, CardTextureQuality.World);
        }

        if (CardCatalog.TryGetById(batchKey, out CardDefinition definition))
            return CardArtLibrary.GetFrontMaterial(definition, CardTextureQuality.World);

        return null;
    }

    static int CompareDrawOrder(WorldCard a, WorldCard b)
    {
        int layerCompare = a.GroundStackLayer.CompareTo(b.GroundStackLayer);
        if (layerCompare != 0)
            return layerCompare;

        return WorldCardDrawOrder.CompareStableInstanceId(a, b);
    }

    bool ShouldRenderCard(WorldCard card)
    {
        Bounds bounds = card.GetInstancedCullBounds();
        if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
            return false;

        if (_camera == null)
            return true;

        Vector3 closestPoint = bounds.ClosestPoint(_camera.transform.position);
        return (closestPoint - _camera.transform.position).sqrMagnitude <= _drawDistanceSq;
    }

    void CullGroundPackRenderers()
    {
        if (_camera == null)
            return;

        CardGroundStack.ForEachTrackedPack(pack =>
        {
            if (pack == null)
                return;

            if (pack.IsInHand || pack.HasActivePhysics || pack.State != WorldBoosterPack.PackState.World)
            {
                pack.SetGroundModelVisible(true);
                return;
            }

            pack.SetGroundModelVisible(ShouldRenderPack(pack));
        });
    }

    bool ShouldRenderPack(WorldBoosterPack pack)
    {
        Bounds bounds = pack.GetCullBounds();
        if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
            return false;

        Vector3 closestPoint = bounds.ClosestPoint(_camera.transform.position);
        return (closestPoint - _camera.transform.position).sqrMagnitude <= _drawDistanceSq;
    }

    static void DrawBatch(Mesh mesh, Material material, Matrix4x4[] matrices, int count)
    {
        Graphics.DrawMeshInstanced(
            mesh,
            0,
            material,
            matrices,
            count,
            properties: null,
            ShadowCastingMode.Off,
            receiveShadows: false,
            CardLayers.WorldCard);
    }
}
