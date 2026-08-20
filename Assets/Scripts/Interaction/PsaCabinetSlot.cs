using UnityEngine;

/// <summary>
/// One PSA display seat on a <see cref="PsaCabinet"/>. Slot numbers 7–10 (left→right on cabinet top).
/// Forward (blue axis) = slab face direction when placed.
/// </summary>
[ExecuteAlways]
public class PsaCabinetSlot : MonoBehaviour
{
    [SerializeField] int slotNumber = 7;
    [SerializeField] int defaultVariantIndex = 1;
    [SerializeField] WorldCard occupiedCard;

    public int SlotNumber => slotNumber;
    public int DefaultVariantIndex => Mathf.Max(1, defaultVariantIndex);
    public bool IsEmpty => occupiedCard == null || occupiedCard.IsInHand;
    public WorldCard OccupiedCard => occupiedCard;

    public void SetSlotNumber(int number)
    {
        slotNumber = PsaArtLibrary.ClampCabinetSlotNumber(number);
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

#if UNITY_EDITOR
    void OnValidate()
    {
        slotNumber = PsaArtLibrary.ClampCabinetSlotNumber(slotNumber);
        defaultVariantIndex = Mathf.Max(1, defaultVariantIndex);
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
