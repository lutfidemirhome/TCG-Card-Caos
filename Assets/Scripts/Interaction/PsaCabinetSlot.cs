using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One PSA display seat. Slot numbers 7–10 label which PSA cards belong here.
/// Forward (blue axis) = slab face direction when placed.
/// </summary>
public class PsaCabinetSlot : MonoBehaviour
{
    const string LabelObjectName = "SlotNumberLabel";
    const string LabelTextObjectName = "Text";

    [SerializeField] int slotNumber = 7;
    [SerializeField] int defaultVariantIndex = 1;
    [SerializeField] WorldCard occupiedCard;

    [Header("Slot number label")]
    [SerializeField] bool showSlotLabel = true;
    [Tooltip("Optional anchor on the holder face. When empty, labelLocalOffset is used.")]
    [SerializeField] Transform labelAnchor;
    [SerializeField] Vector3 labelLocalOffset = new Vector3(0f, 0.05f, 0.015f);
    [SerializeField] Vector3 labelLocalEuler = new Vector3(0f, 180f, 0f);
    [SerializeField] float labelCanvasScale = 0.00035f;
    [SerializeField] int labelFontSize = 120;
    [SerializeField] Color labelColor = new Color(0.12f, 0.12f, 0.12f, 1f);

    RectTransform _labelCanvasRect;
    Text _labelText;

    public int SlotNumber => slotNumber;
    public int DefaultVariantIndex => Mathf.Max(1, defaultVariantIndex);
    public bool IsEmpty => occupiedCard == null || occupiedCard.IsInHand;
    public WorldCard OccupiedCard => occupiedCard;

    public void SetSlotNumber(int number)
    {
        slotNumber = PsaArtLibrary.ClampCabinetSlotNumber(number);
        RefreshLabel();
    }

    public void Occupy(WorldCard card)
    {
        occupiedCard = card;
    }

    public void ClearOccupant()
    {
        occupiedCard = null;
    }

    public Vector3 GetSpawnPosition()
    {
        return transform.position;
    }

    public Quaternion GetSpawnRotation()
    {
        return transform.rotation;
    }

    public bool AcceptsPsaCard(WorldCard card) =>
        card != null
        && card.UsesPsaSlab
        && card.PsaSlotNumber == SlotNumber;

    void OnEnable()
    {
        EnsureLabelExists();
        RefreshLabel();
    }

    void Start()
    {
        EnsureLabelExists();
        RefreshLabel();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        slotNumber = PsaArtLibrary.ClampCabinetSlotNumber(slotNumber);
        defaultVariantIndex = Mathf.Max(1, defaultVariantIndex);
    }
#endif

    public void RefreshLabel()
    {
        CacheLabelReferences();

        if (!showSlotLabel)
        {
            if (_labelCanvasRect != null)
                _labelCanvasRect.gameObject.SetActive(false);
            return;
        }

        if (_labelText == null)
            return;

        if (_labelCanvasRect != null)
            _labelCanvasRect.gameObject.SetActive(true);

        ApplyLabelTransform();
        ApplyLabelVisuals();
    }

    public void EnsureLabelExists()
    {
        CacheLabelReferences();
        if (_labelText != null)
            return;

        CreateLabelHierarchy();
        CacheLabelReferences();
    }

    void ApplyLabelVisuals()
    {
        if (_labelText == null)
            return;

        _labelText.text = slotNumber.ToString();
        _labelText.fontSize = labelFontSize;
        _labelText.color = labelColor;
    }

    void CacheLabelReferences()
    {
        _labelCanvasRect = null;
        _labelText = null;

        Transform anchor = labelAnchor != null ? labelAnchor : transform;
        Transform existing = anchor.Find(LabelObjectName);
        if (existing == null)
            return;

        _labelCanvasRect = existing as RectTransform;
        if (_labelCanvasRect == null)
            _labelCanvasRect = existing.GetComponent<RectTransform>();

        Transform textTransform = existing.Find(LabelTextObjectName);
        if (textTransform != null)
            _labelText = textTransform.GetComponent<Text>();
    }

    void ApplyLabelTransform()
    {
        if (_labelCanvasRect == null)
            return;

        if (labelAnchor == null)
        {
            _labelCanvasRect.localPosition = labelLocalOffset;
            _labelCanvasRect.localRotation = Quaternion.Euler(labelLocalEuler);
        }
        else
        {
            _labelCanvasRect.localPosition = Vector3.zero;
            _labelCanvasRect.localRotation = Quaternion.Euler(labelLocalEuler);
        }

        _labelCanvasRect.localScale = Vector3.one * labelCanvasScale;
    }

    void CreateLabelHierarchy()
    {
        Transform anchor = labelAnchor != null ? labelAnchor : transform;
        RemoveExistingLabel(anchor);

        var canvasGo = new GameObject(LabelObjectName, typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(anchor, false);

        _labelCanvasRect = canvasGo.GetComponent<RectTransform>();
        _labelCanvasRect.sizeDelta = new Vector2(256f, 256f);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = null;

        var textGo = new GameObject(LabelTextObjectName, typeof(RectTransform));
        textGo.transform.SetParent(canvasGo.transform, false);

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        _labelText = textGo.AddComponent<Text>();
        _labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _labelText.fontSize = labelFontSize;
        _labelText.fontStyle = FontStyle.Bold;
        _labelText.alignment = TextAnchor.MiddleCenter;
        _labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _labelText.verticalOverflow = VerticalWrapMode.Overflow;
        _labelText.raycastTarget = false;
        _labelText.color = labelColor;
        _labelText.text = slotNumber.ToString();

        ApplyLabelTransform();
    }

    void RemoveExistingLabel(Transform anchor)
    {
        Transform existing = anchor.Find(LabelObjectName);
        if (existing == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(existing.gameObject);
        else
#endif
            Destroy(existing.gameObject);
    }

#if UNITY_EDITOR
    public void TryCreateLabelObject()
    {
        UnityEditor.Undo.SetCurrentGroupName("Create PSA Slot Label");
        int undoGroup = UnityEditor.Undo.GetCurrentGroup();

        Transform anchor = labelAnchor != null ? labelAnchor : transform;
        Transform existing = anchor.Find(LabelObjectName);
        if (existing != null)
            UnityEditor.Undo.DestroyObjectImmediate(existing.gameObject);

        CreateLabelHierarchy();
        UnityEditor.Undo.RegisterCreatedObjectUndo(_labelCanvasRect.gameObject, "Create PSA Slot Label");
        UnityEditor.Undo.CollapseUndoOperations(undoGroup);

        RefreshLabel();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.85f);
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(
            CardDimensions.Width * 1.06f,
            CardDimensions.Thickness * 4f,
            CardDimensions.Height));
        Gizmos.matrix = previous;

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.04f, $"PSA Slot {slotNumber}");
#endif
    }
}
