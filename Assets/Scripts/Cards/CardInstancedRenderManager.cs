using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// GPU-instanced draws for static world cards that share mesh/material batches.
/// </summary>
public class CardInstancedRenderManager : MonoBehaviour
{
    const int MaxInstancesPerBatch = 1023;

    [SerializeField] float drawDistance = 40f;
    [SerializeField] float colliderDistance = 25f;
    [SerializeField] float colliderUpdateInterval = 0.25f;

    static CardInstancedRenderManager _instance;

    readonly HashSet<WorldCard>[] _cardsByPalette = new HashSet<WorldCard>[CardPalette.Count];
    readonly Matrix4x4[] _matrixBuffer = new Matrix4x4[MaxInstancesPerBatch];
    readonly List<int> _scratchPaletteIndices = new List<int>(CardPalette.Count);
    readonly List<WorldCard> _drawSortScratch = new List<WorldCard>(512);
    readonly Plane[] _frustumPlanes = new Plane[6];

    Camera _camera;
    float _colliderUpdateTimer;
    float _drawDistanceSq;
    float _colliderDistanceSq;

    public static CardInstancedRenderManager Instance => _instance;

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
        for (int i = 0; i < _cardsByPalette.Length; i++)
        {
            if (_cardsByPalette[i] == null)
                _cardsByPalette[i] = new HashSet<WorldCard>();
        }

        if (_drawDistanceSq <= 0f || _colliderDistanceSq <= 0f)
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
        colliderDistance = Mathf.Max(2f, colliderDistance);
        _drawDistanceSq = drawDistance * drawDistance;
        _colliderDistanceSq = colliderDistance * colliderDistance;
    }

    void LateUpdate()
    {
        if (_camera == null)
            _camera = Camera.main;

        DrawInstancedCards();
        UpdateColliderStates();
    }

    public void Register(WorldCard card)
    {
        if (card == null || card.IsInHand)
            return;

        EnsureBucketsInitialized();
        CardGroundStack.Track(card);
        CardGroundStack.RefreshCluster(card);

        if (!card.CanUseInstancedRendering)
            return;

        _cardsByPalette[GetPaletteIndex(card)].Add(card);
    }

    public void Unregister(WorldCard card)
    {
        if (card == null)
            return;

        EnsureBucketsInitialized();
        HashSet<WorldCard> bucket = _cardsByPalette[GetPaletteIndex(card)];
        if (bucket != null)
            bucket.Remove(card);
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

        Mesh mesh = CardArtLibrary.InstancedCardMesh;
        if (mesh == null)
            return;

        GeometryUtility.CalculateFrustumPlanes(_camera, _frustumPlanes);
        _scratchPaletteIndices.Clear();

        for (int paletteIndex = 0; paletteIndex < _cardsByPalette.Length; paletteIndex++)
        {
            HashSet<WorldCard> bucket = _cardsByPalette[paletteIndex];
            if (bucket != null && bucket.Count > 0)
                _scratchPaletteIndices.Add(paletteIndex);
        }

        for (int i = 0; i < _scratchPaletteIndices.Count; i++)
        {
            int paletteIndex = _scratchPaletteIndices[i];
            HashSet<WorldCard> cards = _cardsByPalette[paletteIndex];
            Material frontMaterial = CardArtLibrary.GetFrontMaterial(paletteIndex, CardTextureQuality.World);
            if (frontMaterial == null)
                continue;

            _drawSortScratch.Clear();
            foreach (WorldCard card in cards)
            {
                if (card == null || !card.CanUseInstancedRendering)
                    continue;
                if (!ShouldRenderCard(card))
                    continue;

                _drawSortScratch.Add(card);
            }

            _drawSortScratch.Sort(CompareDrawOrder);

            int writeIndex = 0;
            for (int c = 0; c < _drawSortScratch.Count; c++)
            {
                _matrixBuffer[writeIndex++] = _drawSortScratch[c].GetInstancedDrawMatrix();
                if (writeIndex < MaxInstancesPerBatch)
                    continue;

                DrawBatch(mesh, frontMaterial, _matrixBuffer, writeIndex);
                writeIndex = 0;
            }

            if (writeIndex > 0)
                DrawBatch(mesh, frontMaterial, _matrixBuffer, writeIndex);
        }
    }

    static int CompareDrawOrder(WorldCard a, WorldCard b)
    {
        int layerCompare = a.GroundStackLayer.CompareTo(b.GroundStackLayer);
        if (layerCompare != 0)
            return layerCompare;

        return a.GetInstanceID().CompareTo(b.GetInstanceID());
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

    void UpdateColliderStates()
    {
        if (_camera == null)
            return;

        EnsureBucketsInitialized();
        _colliderUpdateTimer -= Time.deltaTime;
        if (_colliderUpdateTimer > 0f)
            return;

        _colliderUpdateTimer = colliderUpdateInterval;
        Vector3 cameraPosition = _camera.transform.position;
        GeometryUtility.CalculateFrustumPlanes(_camera, _frustumPlanes);

        for (int paletteIndex = 0; paletteIndex < _cardsByPalette.Length; paletteIndex++)
        {
            HashSet<WorldCard> cards = _cardsByPalette[paletteIndex];
            if (cards == null)
                continue;

            foreach (WorldCard card in cards)
            {
                if (card == null || !card.CanUseInstancedRendering)
                    continue;

                Bounds bounds = card.GetInstancedCullBounds();
                bool inView = GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds);
                if (!inView)
                {
                    card.SetWorldColliderEnabled(false);
                    continue;
                }

                Vector3 closestPoint = bounds.ClosestPoint(cameraPosition);
                bool nearEnough = (closestPoint - cameraPosition).sqrMagnitude <= _colliderDistanceSq;
                card.SetWorldColliderEnabled(nearEnough);
            }
        }
    }

    static void DrawBatch(Mesh mesh, Material frontMaterial, Matrix4x4[] matrices, int count)
    {
        Graphics.DrawMeshInstanced(
            mesh,
            0,
            frontMaterial,
            matrices,
            count,
            properties: null,
            ShadowCastingMode.Off,
            receiveShadows: false);
    }

    static int GetPaletteIndex(WorldCard card)
    {
        int paletteIndex = card.PaletteIndex % CardPalette.Count;
        if (paletteIndex < 0)
            paletteIndex += CardPalette.Count;

        return paletteIndex;
    }
}
