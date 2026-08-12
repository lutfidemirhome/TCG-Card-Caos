using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fixed 2D world-space category plaque for a <see cref="CardShelf"/> (e.g. "Normal Common").
/// One component per cabinet — assign a unique category name on each shelf instance.
/// </summary>
[DisallowMultipleComponent]
public class CardShelfCategoryLabel : MonoBehaviour
{
    const string LabelRootName = "CategoryLabel";

    [SerializeField] CardShelf shelf;
    [SerializeField] bool showLabel = true;
    [SerializeField] string categoryName = "Normal Common";

    [Header("Layout")]
    [SerializeField] float heightPadding = 0.1f;
    [SerializeField] float forwardOffset = 0.025f;
    [SerializeField] float worldWidth = 1.35f;
    [SerializeField] float worldHeight = 0.22f;

    [Header("Typography")]
    [SerializeField] int fontSize = 72;
    [SerializeField] Color textColor = new Color(0.98f, 0.98f, 0.94f, 1f);
    [SerializeField] Color backgroundColor = new Color(0.08f, 0.09f, 0.11f, 0.88f);

    RectTransform _labelRoot;
    Text _labelText;
    Image _labelBackground;

#if UNITY_EDITOR
    bool _editorRebuildScheduled;

    static bool CanEditLabelHierarchy(GameObject owner)
    {
        if (owner == null)
            return false;

        if (Application.isPlaying)
            return true;

        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.IsPartOfPrefabContents(owner))
            return true;

        if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(owner))
            return true;

        if (!UnityEditor.PrefabUtility.IsPartOfAnyPrefab(owner))
            return true;

        return false;
    }

    bool ShouldScheduleEditorRebuild()
    {
        if (CanEditLabelHierarchy(gameObject))
            return true;

        return transform.Find(LabelRootName) != null;
    }
#endif

    public string CategoryName
    {
        get => categoryName;
        set
        {
            categoryName = value;
            RebuildLabel();
        }
    }

    public bool ShowLabel
    {
        get => showLabel;
        set
        {
            showLabel = value;
            RebuildLabel();
        }
    }

    void Awake()
    {
        if (shelf == null)
            shelf = GetComponent<CardShelf>();
    }

    void Start()
    {
        RebuildLabel();
    }

    void OnDestroy()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= EditorDeferredRebuild;
#endif
    }

#if UNITY_EDITOR
    void OnEnable()
    {
        if (!Application.isPlaying && ShouldScheduleEditorRebuild())
            ScheduleEditorRebuild();
    }

    void OnValidate()
    {
        if (!isActiveAndEnabled)
            return;

        if (shelf == null)
            shelf = GetComponent<CardShelf>();

        if (ShouldScheduleEditorRebuild())
            ScheduleEditorRebuild();
    }

    void ScheduleEditorRebuild()
    {
        if (_editorRebuildScheduled)
            return;

        _editorRebuildScheduled = true;
        UnityEditor.EditorApplication.delayCall += EditorDeferredRebuild;
    }

    void EditorDeferredRebuild()
    {
        _editorRebuildScheduled = false;
        if (this == null)
            return;

        RebuildLabel();
    }

    public void ScheduleRebuild()
    {
        ScheduleEditorRebuild();
    }

    public static bool CanAuthorLabelHierarchy(GameObject owner)
    {
        return CanEditLabelHierarchy(owner);
    }
#endif

    [ContextMenu("Rebuild Category Label")]
    public void RebuildLabel()
    {
        if (shelf == null)
            shelf = GetComponent<CardShelf>();

        if (!showLabel)
        {
            SetLabelActive(false);
            return;
        }

        string displayName = ResolveDisplayName();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            SetLabelActive(false);
            return;
        }

#if UNITY_EDITOR
        bool canEditHierarchy = Application.isPlaying || CanEditLabelHierarchy(gameObject);
#else
        bool canEditHierarchy = true;
#endif

        if (canEditHierarchy)
        {
            EnsureLabelHierarchy();
        }
        else if (!TryResolveExistingLabel())
        {
            return;
        }

        ApplyLabelVisuals(displayName);
        UpdateLabelTransform();
        SetLabelActive(true);
    }

    string ResolveDisplayName()
    {
        if (shelf == null)
            shelf = GetComponent<CardShelf>();

        if (shelf != null && !string.IsNullOrWhiteSpace(shelf.CategoryDisplayName))
            return shelf.CategoryDisplayName;

        return categoryName;
    }

    void EnsureLabelHierarchy()
    {
        if (!TryResolveExistingLabel())
            BuildLabelHierarchy();
    }

    bool TryResolveExistingLabel()
    {
        if (_labelRoot != null && _labelRoot)
            return _labelText != null && _labelBackground != null;

        Transform existing = transform.Find(LabelRootName);
        if (existing == null)
        {
            ClearLabelReferences();
            return false;
        }

        _labelRoot = existing as RectTransform;
        _labelBackground = existing.Find("Background")?.GetComponent<Image>();
        _labelText = existing.Find("LabelText")?.GetComponent<Text>();
        return _labelRoot != null && _labelBackground != null && _labelText != null;
    }

    void BuildLabelHierarchy()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && !CanEditLabelHierarchy(gameObject))
            return;
#endif

        DestroyLegacyLabelObjects();

        var rootGo = new GameObject(LabelRootName, typeof(RectTransform), typeof(Canvas));
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.Undo.RegisterCreatedObjectUndo(rootGo, "Build Category Label");
#endif
        rootGo.transform.SetParent(transform, false);
        _labelRoot = rootGo.GetComponent<RectTransform>();

        Canvas canvas = rootGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var backgroundGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backgroundGo.transform.SetParent(rootGo.transform, false);
        _labelBackground = backgroundGo.GetComponent<Image>();
        _labelBackground.raycastTarget = false;

        RectTransform backgroundRect = backgroundGo.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        var textGo = new GameObject("LabelText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(rootGo.transform, false);
        _labelText = textGo.GetComponent<Text>();
        _labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _labelText.fontStyle = FontStyle.Bold;
        _labelText.alignment = TextAnchor.MiddleCenter;
        _labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _labelText.verticalOverflow = VerticalWrapMode.Truncate;
        _labelText.raycastTarget = false;

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 6f);
        textRect.offsetMax = new Vector2(-12f, -6f);
    }

    void DestroyLegacyLabelObjects()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && !CanEditLabelHierarchy(gameObject))
            return;
#endif

        Transform existing = transform.Find(LabelRootName);
        if (existing == null)
        {
            ClearLabelReferences();
            return;
        }

        if (Application.isPlaying)
            Destroy(existing.gameObject);
        else
            DestroyImmediate(existing.gameObject, true);

        ClearLabelReferences();
    }

    void ApplyLabelVisuals(string displayName)
    {
        if (_labelRoot == null || _labelText == null || _labelBackground == null)
            return;

        float aspect = worldWidth > 0.0001f ? worldHeight / worldWidth : 0.16f;
        Vector2 pixelSize = new Vector2(900f, 900f * aspect);
        _labelRoot.sizeDelta = pixelSize;
        float scale = worldWidth / pixelSize.x;
        // Match shelf card left-right orientation (see CardArtLibrary.ShelfVisualScale).
        _labelRoot.localScale = new Vector3(-scale, scale, scale);

        _labelText.text = displayName;
        _labelText.fontSize = fontSize;
        _labelText.color = textColor;
        _labelBackground.color = backgroundColor;
    }

    void UpdateLabelTransform()
    {
        if (_labelRoot == null)
            return;

        Bounds bounds = CalculateShelfVisualBounds();
        Vector3 faceDirection = GetCustomerFacingDirection();
        float labelHalfHeight = worldHeight * 0.5f;
        Vector3 topCenter = new Vector3(
            bounds.center.x,
            bounds.max.y + heightPadding + labelHalfHeight,
            bounds.center.z);

        _labelRoot.position = topCenter + faceDirection * forwardOffset;
        _labelRoot.rotation = Quaternion.LookRotation(faceDirection, Vector3.up);
    }

    Vector3 GetCustomerFacingDirection()
    {
        if (shelf != null)
            return shelf.GetCustomerFacingDirection();

        Vector3 face = transform.forward;
        face.y = 0f;
        if (face.sqrMagnitude < 0.0001f)
            return Vector3.forward;

        return face.normalized;
    }

    Bounds CalculateShelfVisualBounds()
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || ShouldIgnoreRenderer(renderer))
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
            bounds = new Bounds(transform.position, new Vector3(1f, 2f, 0.4f));

        return bounds;
    }

    static bool ShouldIgnoreRenderer(Renderer renderer)
    {
        Transform current = renderer.transform;
        while (current != null)
        {
            string objectName = current.name;
            if (objectName == LabelRootName
                || objectName == "Background"
                || objectName == "LabelText"
                || objectName == "SlotCardPreview"
                || objectName == "ShelfPlacementOutline")
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    void SetLabelActive(bool active)
    {
        if (_labelRoot != null && _labelRoot)
            _labelRoot.gameObject.SetActive(active);
    }

    void ClearLabelReferences()
    {
        _labelRoot = null;
        _labelText = null;
        _labelBackground = null;
    }
}
