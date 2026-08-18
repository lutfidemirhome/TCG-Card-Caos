using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Marker for one card seat on a cabinet shelf.
/// In the editor a translucent card-shaped plane is always visible so you can place seats by eye.
/// Parent under an object with <see cref="CardShelf"/>. Forward (blue axis) = card face direction.
/// </summary>
[ExecuteAlways]
public class CardShelfSlot : MonoBehaviour
{
    const string PreviewObjectName = "SlotCardPreview";

    static readonly Color PreviewFillColor = new Color(1f, 0.82f, 0.12f, 0.45f);
    static readonly Color PreviewEdgeColor = new Color(1f, 0.92f, 0.2f, 0.95f);

    static Mesh _sharedCardPlaneMesh;
    static Material _sharedFillMaterial;
    static Material _sharedEdgeMaterial;

    [SerializeField] WorldCard occupiedCard;
    [SerializeField] int rowIndex;
    [SerializeField] int columnIndex;

    Transform _previewRoot;
    MeshRenderer _fillRenderer;
    MeshRenderer _edgeRenderer;
    bool _previewDeferred;

    public bool IsEmpty
    {
        get
        {
            if (occupiedCard == null)
                return true;
            if (occupiedCard.IsInHand)
            {
                occupiedCard = null;
                return true;
            }

            return false;
        }
    }

    public WorldCard OccupiedCard => occupiedCard;

    /// <summary>Row on the cabinet (0 = first ShelfSlots_Level).</summary>
    public int RowIndex => rowIndex;

    /// <summary>Column on the row (0 at shelf local -X).</summary>
    public int ColumnIndex => columnIndex;

    /// <summary>Customer-facing slot number (1 = leftmost when facing the cabinet).</summary>
    public int SlotNumber
    {
        get
        {
            CardShelf shelf = GetComponentInParent<CardShelf>();
            if (shelf != null)
                return shelf.ResolveSlotNumber(this);

            return CardShelfCategories.ColumnToSlotNumber(columnIndex, OwnerShelfSlotsPerRow);
        }
    }

    public int OwnerShelfSlotsPerRow
    {
        get
        {
            CardShelf shelf = GetComponentInParent<CardShelf>();
            return shelf != null
                ? shelf.SlotsPerRow
                : CardShelfCategories.DefaultSlotsPerRow;
        }
    }

    public void ConfigureIndices(int row, int column)
    {
        rowIndex = Mathf.Max(0, row);
        columnIndex = Mathf.Clamp(column, 0, OwnerShelfSlotsPerRow - 1);
    }

    public void SyncIndicesFromName()
    {
        if (CardShelfSlotNaming.TryParse(gameObject.name, out int _, out int column))
            columnIndex = column;
    }

    public void SyncIndicesFromHierarchy()
    {
        Transform levelRoot = transform.parent;
        if (levelRoot != null
            && CardShelfSlotNaming.TryParseLevelRowIndex(levelRoot.name, out int levelRow))
        {
            rowIndex = levelRow;
        }

        SyncIndicesFromName();
    }

    public void Occupy(WorldCard card)
    {
        occupiedCard = card;
        RefreshPreviewVisibility();
    }

    public void ClearIfMatches(WorldCard card)
    {
        if (occupiedCard != card)
            return;

        occupiedCard = null;
        CardGroundQuery.UntrackShelfCard(card);
        card?.SetPlayerAimFocus(false);
        RefreshPreviewVisibility();
    }

    public void RefreshOccupancy()
    {
        if (occupiedCard != null && occupiedCard.IsInHand)
            occupiedCard = null;
        RefreshPreviewVisibility();
    }

    void Awake()
    {
        if (Application.isPlaying)
            HidePreviewForPlayMode();
    }

    void OnEnable()
    {
        SyncIndicesFromHierarchy();
        EnsurePreview();
        if (Application.isPlaying)
            HidePreviewForPlayMode();
#if UNITY_EDITOR
        else if (!IsEditingPrefabAsset())
            RefreshPreviewVisibility();
#else
        else
            RefreshPreviewVisibility();
#endif
    }

    void OnDisable()
    {
        SetPreviewActive(false);
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        if (IsEditingPrefabAsset())
            return;
#endif
        // SetParent/AddComponent are illegal during OnValidate — defer to editor delayCall.
        SchedulePreviewRefresh();
    }

    void SchedulePreviewRefresh()
    {
        if (Application.isPlaying || _previewDeferred)
            return;

        _previewDeferred = true;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += ApplyDeferredPreview;
#else
        _previewDeferred = false;
#endif
    }

#if UNITY_EDITOR
    void ApplyDeferredPreview()
    {
        _previewDeferred = false;
        if (this == null)
            return;

        if (IsEditingPrefabAsset())
            return;

        EnsurePreview();
        RefreshPreviewVisibility();
    }
#endif

    void LateUpdate()
    {
        if (Application.isPlaying)
        {
            HidePreviewForPlayMode();
            return;
        }

#if UNITY_EDITOR
        // Never drive preview visibility on the prefab asset — that dirties 50+ cabinet YAML files.
        if (IsEditingPrefabAsset())
            return;
#endif

        EnsurePreview();
        RefreshPreviewVisibility();
    }

    void RefreshPreviewVisibility()
    {
        bool show = !Application.isPlaying && IsEmpty;
        SetPreviewActive(show);
    }

    /// <summary>
    /// Editor preview uses renderer.enabled only — never GameObject.SetActive on the prefab asset
    /// (that flips m_IsActive and fills SourceTree with ~50 cabinet prefab diffs).
    /// </summary>
    void SetPreviewActive(bool active)
    {
        EnsurePreview();
        if (_previewRoot == null)
            return;

        if (Application.isPlaying)
        {
            HidePreviewForPlayMode();
            return;
        }

#if UNITY_EDITOR
        if (IsEditingPrefabAsset())
            return;
#endif

        SetPreviewRenderersEnabled(active);
    }

    void HidePreviewForPlayMode()
    {
        SetPreviewRenderersEnabled(false);
    }

    void SetPreviewRenderersEnabled(bool enabled)
    {
#if UNITY_EDITOR
        if (IsEditingPrefabAsset())
            return;
#endif

        if (_fillRenderer != null)
            _fillRenderer.enabled = enabled;
        if (_edgeRenderer != null)
            _edgeRenderer.enabled = enabled;
    }

#if UNITY_EDITOR
    bool IsEditingPrefabAsset() =>
        PrefabUtility.IsPartOfPrefabAsset(gameObject);
#endif

    void EnsurePreview()
    {
        if (_previewRoot == null)
        {
            Transform existing = transform.Find(PreviewObjectName);
            if (existing != null)
                _previewRoot = existing;
        }

        if (_previewRoot == null)
        {
            var go = new GameObject(PreviewObjectName);
            go.transform.SetParent(transform, false);
#if UNITY_EDITOR
            if (!IsEditingPrefabAsset())
                go.hideFlags = HideFlags.DontSave;
#endif
            _previewRoot = go.transform;
        }
        else if (!_previewRoot.gameObject.activeSelf)
        {
            // Prefab ships with SlotCardPreview inactive; enable only on scene instances (not prefab assets).
#if UNITY_EDITOR
            if (!IsEditingPrefabAsset())
                _previewRoot.gameObject.SetActive(true);
#else
            _previewRoot.gameObject.SetActive(true);
#endif
        }

        _previewRoot.localPosition = Vector3.zero;
        _previewRoot.localRotation = Quaternion.identity;
        _previewRoot.localScale = Vector3.one;

        EnsureSharedAssets();

        if (_fillRenderer == null)
            _fillRenderer = EnsureChildRenderer(_previewRoot, "Fill", _sharedCardPlaneMesh, _sharedFillMaterial);
        if (_edgeRenderer == null)
            _edgeRenderer = EnsureChildRenderer(_previewRoot, "Edge", CardVisualResources.InteractionBorderFrameMesh, _sharedEdgeMaterial);

        // Card plane: pivot at bottom-center, face along +Z (slot forward).
        Transform fillT = _fillRenderer.transform;
        fillT.localPosition = Vector3.zero;
        fillT.localRotation = Quaternion.identity;
        fillT.localScale = Vector3.one * CardDimensions.WorldCardScale;

        // Border mesh is authored in card-visual XY space (centered). Lift to card center.
        float halfHeight = PreviewCardHeight() * CardDimensions.WorldCardScale * 0.5f;
        Transform edgeT = _edgeRenderer.transform;
        edgeT.localPosition = new Vector3(0f, halfHeight, 0f);
        edgeT.localRotation = Quaternion.identity;
        edgeT.localScale = Vector3.one * CardDimensions.WorldCardScale;
    }

    static MeshRenderer EnsureChildRenderer(Transform parent, string childName, Mesh mesh, Material material)
    {
        Transform child = parent.Find(childName);
        GameObject go = child != null ? child.gameObject : new GameObject(childName);
        if (child == null)
            go.transform.SetParent(parent, false);

        var filter = go.GetComponent<MeshFilter>();
        if (filter == null)
            filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    static void EnsureSharedAssets()
    {
        CardArtLibrary.EnsureLoaded();

        if (_sharedCardPlaneMesh == null)
            _sharedCardPlaneMesh = BuildUprightCardPlaneMesh();

        if (_sharedFillMaterial == null)
        {
            _sharedFillMaterial = RuntimeMaterialUtility.CreateUnlitMaterial(
                PreviewFillColor,
                enableInstancing: false,
                renderQueue: (int)RenderQueue.Transparent);
            SetMaterialTransparent(_sharedFillMaterial, PreviewFillColor);
        }

        if (_sharedEdgeMaterial == null)
        {
            _sharedEdgeMaterial = RuntimeMaterialUtility.CreateUnlitMaterial(
                PreviewEdgeColor,
                enableInstancing: false,
                renderQueue: (int)RenderQueue.Transparent + 1);
            SetMaterialTransparent(_sharedEdgeMaterial, PreviewEdgeColor);
        }
    }

    static void SetMaterialTransparent(Material material, Color color)
    {
        if (material == null)
            return;

        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    static float PreviewCardWidth()
    {
        float width = CardDimensions.Width;
        return width > 0.001f ? width : 0.126f;
    }

    static float PreviewCardHeight()
    {
        float height = CardDimensions.Height;
        return height > 0.001f ? height : 0.176f;
    }

    /// <summary>
    /// Upright card quad in local space: bottom-center pivot, face toward +Z.
    /// </summary>
    static Mesh BuildUprightCardPlaneMesh()
    {
        float halfW = PreviewCardWidth() * 0.5f;
        float height = PreviewCardHeight();
        const float z = 0.001f;

        var mesh = new Mesh { name = "ShelfSlotCardPlane" };
        mesh.vertices = new[]
        {
            new Vector3(-halfW, 0f, z),
            new Vector3(halfW, 0f, z),
            new Vector3(halfW, height, z),
            new Vector3(-halfW, height, z),
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        };
        // Front + back so it reads from both sides while editing.
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
