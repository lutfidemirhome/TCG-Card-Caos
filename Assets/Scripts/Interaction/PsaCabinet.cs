using UnityEngine;

/// <summary>
/// PSA graded-card display cabinet. Holds four top slots numbered 7–10 (left→right).
/// Attach to the cabinet model; add <see cref="PsaCabinetSlot"/> children on each seat.
/// </summary>
public class PsaCabinet : MonoBehaviour
{
    [SerializeField] PsaCabinetSlot[] slots;

    public PsaCabinetSlot[] Slots => slots;

    void Awake()
    {
        PersistentId.GetOrCreate(gameObject);
        CollectSlots();
    }

    public int CountOccupiedSlots()
    {
        CollectSlots();
        int count = 0;
        if (slots == null)
            return 0;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && !slots[i].IsEmpty)
                count++;
        }

        return count;
    }

    public int CountCorrectlyPlacedCards()
    {
        CollectSlots();
        int count = 0;
        if (slots == null)
            return 0;

        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot == null || slot.IsEmpty || !PsaArtLibrary.IsCabinetSlotNumber(slot.SlotNumber))
                continue;

            WorldCard card = slot.OccupiedCard;
            if (card != null && !card.IsInHand && slot.IsCorrectPlacement(card))
                count++;
        }

        return count;
    }

    public bool IsComplete()
    {
        CollectSlots();
        if (slots == null || slots.Length == 0)
            return false;

        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot == null || slot.IsEmpty || !slot.IsCorrectPlacement(slot.OccupiedCard))
                return false;
        }

        return true;
    }

    public bool TryRestoreCard(WorldCard card, int slotNumber)
    {
        PsaCabinetSlot slot = FindSlot(slotNumber);
        if (slot == null || card == null)
            return false;

        return slot.RestoreOccupiedCard(card);
    }

    public void CollectSlots()
    {
        slots = GetComponentsInChildren<PsaCabinetSlot>(true);
        System.Array.Sort(slots, (a, b) => a.SlotNumber.CompareTo(b.SlotNumber));
    }

    public PsaCabinetSlot FindSlot(int slotNumber)
    {
        if (slots == null || slots.Length == 0)
            CollectSlots();

        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot != null && slot.SlotNumber == slotNumber)
                return slot;
        }

        return null;
    }

    /// <summary>Spawns default variant-1 slabs on empty cabinet slots.</summary>
    public void SpawnDefaultSlabs(Transform parentRoot)
    {
        CollectSlots();
        if (slots == null || slots.Length == 0)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot == null || !slot.IsEmpty)
                continue;

            WorldCard card = CardFactory.CreateWorldPsaCard(
                slot.GetSpawnPosition(),
                slot.GetSpawnRotation(),
                slot.SlotNumber,
                slot.DefaultVariantIndex,
                cardName: $"PSA_{slot.SlotNumber}_{slot.DefaultVariantIndex}");

            if (parentRoot != null)
                card.transform.SetParent(parentRoot, true);

            slot.Occupy(card);
            CardGroundStack.ApplyStackHeight(card, placeOnTop: false);
        }
    }

#if UNITY_EDITOR
    void Reset()
    {
        EnsureDefaultSlots();
    }

    [ContextMenu("Ensure Default PSA Slots (7–10)")]
    void EnsureDefaultSlots()
    {
        var existing = new System.Collections.Generic.List<PsaCabinetSlot>(
            GetComponentsInChildren<PsaCabinetSlot>(true));

        Transform slotsRoot = transform.Find("PsaCabinetSlots");
        if (slotsRoot == null)
        {
            var rootGo = new GameObject("PsaCabinetSlots");
            rootGo.transform.SetParent(transform, false);
            slotsRoot = rootGo.transform;
        }

        float spacing = CardDimensions.Width * 1.15f;
        float startX = -spacing * 1.5f;

        for (int i = 0; i < PsaArtLibrary.CabinetSlotNumbers.Length; i++)
        {
            int slotNumber = PsaArtLibrary.CabinetSlotNumbers[i];
            PsaCabinetSlot slot = existing.Find(s => s != null && s.SlotNumber == slotNumber);
            if (slot == null)
            {
                var slotGo = new GameObject($"PsaCabinetSlot_{slotNumber}");
                slotGo.transform.SetParent(slotsRoot, false);
                slot = slotGo.AddComponent<PsaCabinetSlot>();
            }

            slot.SetSlotNumber(slotNumber);
            slot.transform.localPosition = new Vector3(startX + spacing * i, 0f, 0f);
            slot.transform.localRotation = Quaternion.identity;
        }

        CollectSlots();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
