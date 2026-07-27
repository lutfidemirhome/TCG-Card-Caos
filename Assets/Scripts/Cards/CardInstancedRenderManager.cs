using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// GPU-instanced draws for static world cards that share mesh/material batches.
/// </summary>
public class CardInstancedRenderManager : MonoBehaviour
{
    const int MaxInstancesPerBatch = 1023;

    [SerializeField] float drawDistance = 12f;
    [SerializeField] float colliderDistance = 9f;
    [SerializeField] float colliderUpdateInterval = 0.25f;

    static CardInstancedRenderManager _instance;

    readonly HashSet<WorldCard>[] _cardsByPalette = new HashSet<WorldCard>[CardPalette.Count];
    readonly Matrix4x4[] _matrixBuffer = new Matrix4x4[MaxInstancesPerBatch];
    readonly List<int> _scratchPaletteIndices = new List<int>(CardPalette.Count);
    readonly Plane[] _frustumPlanes = new Plane[6];

    Camera _camera;
    float _colliderUpdateTimer;
    float _drawDistanceSq;
    float _colliderDistanceSq;

    public static CardInstancedRenderManager Instance => _instance;

    public static CardInstancedRenderManager EnsureExists()
    {
        if (_instance != null)
            return _instance;

        var existing = FindFirstObjectByType<CardInstancedRenderManager>();
        if (existing != null)
        {
            _instance = existing;
            return _instance;
        }

        var managerObject = new GameObject(nameof(CardInstancedRenderManager));
        return managerObject.AddComponent<CardInstancedRenderManager>();
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

        for (int i = 0; i < _cardsByPalette.Length; i++)
            _cardsByPalette[i] = new HashSet<WorldCard>();
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
        if (card == null || !card.CanUseInstancedRendering)
            return;

        _cardsByPalette[GetPaletteIndex(card)].Add(card);
    }

    public void Unregister(WorldCard card)
    {
        if (card == null)
            return;

        _cardsByPalette[GetPaletteIndex(card)].Remove(card);
    }

    void DrawInstancedCards()
    {
        if (_camera == null)
            return;

        CardArtLibrary.EnsureLoaded();

        Mesh mesh = CardArtLibrary.InstancedCardMesh;
        if (mesh == null)
            return;

        Vector3 cameraPosition = _camera.transform.position;
        GeometryUtility.CalculateFrustumPlanes(_camera, _frustumPlanes);
        _scratchPaletteIndices.Clear();

        for (int paletteIndex = 0; paletteIndex < _cardsByPalette.Length; paletteIndex++)
        {
            if (_cardsByPalette[paletteIndex].Count > 0)
                _scratchPaletteIndices.Add(paletteIndex);
        }

        for (int i = 0; i < _scratchPaletteIndices.Count; i++)
        {
            int paletteIndex = _scratchPaletteIndices[i];
            HashSet<WorldCard> cards = _cardsByPalette[paletteIndex];
            Material frontMaterial = CardArtLibrary.GetFrontMaterial(paletteIndex, CardTextureQuality.World);
            if (frontMaterial == null)
                continue;

            int writeIndex = 0;
            foreach (WorldCard card in cards)
            {
                if (card == null || !card.CanUseInstancedRendering)
                    continue;

                if (!ShouldRenderCard(card, cameraPosition))
                    continue;

                _matrixBuffer[writeIndex++] = card.GetInstancedDrawMatrix();
                if (writeIndex < MaxInstancesPerBatch)
                    continue;

                DrawBatch(mesh, frontMaterial, _matrixBuffer, writeIndex);
                writeIndex = 0;
            }

            if (writeIndex > 0)
                DrawBatch(mesh, frontMaterial, _matrixBuffer, writeIndex);
        }
    }

    bool ShouldRenderCard(WorldCard card, Vector3 cameraPosition)
    {
        Vector3 cardPosition = card.transform.position;
        if ((cardPosition - cameraPosition).sqrMagnitude > _drawDistanceSq)
            return false;

        return GeometryUtility.TestPlanesAABB(_frustumPlanes, card.GetInstancedCullBounds());
    }

    void UpdateColliderStates()
    {
        if (_camera == null)
            return;

        _colliderUpdateTimer -= Time.deltaTime;
        if (_colliderUpdateTimer > 0f)
            return;

        _colliderUpdateTimer = colliderUpdateInterval;
        Vector3 cameraPosition = _camera.transform.position;

        for (int paletteIndex = 0; paletteIndex < _cardsByPalette.Length; paletteIndex++)
        {
            HashSet<WorldCard> cards = _cardsByPalette[paletteIndex];
            foreach (WorldCard card in cards)
            {
                if (card == null || !card.CanUseInstancedRendering)
                    continue;

                bool enableCollider = (card.transform.position - cameraPosition).sqrMagnitude <= _colliderDistanceSq;
                card.SetWorldColliderEnabled(enableCollider);
            }
        }
    }

    static void DrawBatch(Mesh mesh, Material frontMaterial, Matrix4x4[] matrices, int count)
    {
        // Ground cards lie face-up; the back submesh faces into the floor and is never visible.
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
