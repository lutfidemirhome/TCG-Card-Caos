using UnityEngine;

/// <summary>
/// PSA graded-card display cabinet with author-placed numbered holders.
/// Attach to the cabinet model; add <see cref="PsaCabinetSlot"/> children on each seat.
/// </summary>
public class PsaCabinet : MonoBehaviour
{
    [SerializeField] PsaCabinetSlot[] slots;

    bool _slotCacheValid;
    bool _celebratedCabinet;

    void OnDisable() => CabinetCompletionEffect.Cancel(transform);

    /// <summary>Called only by a real placement landing, never by save restoration.</summary>
    public bool TryPlayCompletionFeedback()
    {
        if (_celebratedCabinet || !IsComplete()) return false;
        foreach (PsaCabinetSlot slot in slots)
        {
            WorldCard card = slot != null ? slot.OccupiedCard : null;
            if (!card || !card.isActiveAndEnabled || card.IsFlyingToShelf) return false;
        }
        if (!CabinetCompletionEffect.Play(this)) return false;
        _celebratedCabinet = true;
        return true;
    }

    public PsaCabinetSlot[] Slots { get { EnsureSlotCache(); return slots; } }

    void OnTransformChildrenChanged() => _slotCacheValid = false;

    void EnsureSlotCache()
    {
        if (!_slotCacheValid || !Application.isPlaying) CollectSlots();
    }

    void Awake()
    {
        PersistentId.GetOrCreate(gameObject);
        EnsureSlotCache();
    }

    public int CountPlaceableSlots()
    {
        EnsureSlotCache();
        int count = 0;
        if (slots == null)
            return 0;

        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot != null && slot.gameObject.activeInHierarchy)
                count++;
        }

        return count;
    }

    public int CountOccupiedSlots()
    {
        EnsureSlotCache();
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
        CollectHudProgress(out int placed, out _);
        return placed;
    }

    public bool IsComplete()
    {
        CollectHudProgress(out _, out bool complete);
        return complete;
    }

    /// <summary>Single slot scan for HUD / save progress.</summary>
    public void CollectHudProgress(out int correctlyPlaced, out bool complete)
    {
        EnsureSlotCache();
        correctlyPlaced = 0;
        complete = slots != null && slots.Length > 0;

        if (slots == null)
        {
            complete = false;
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot == null || slot.IsEmpty || !PsaArtLibrary.IsCabinetSlotNumber(slot.SlotNumber))
            {
                complete = false;
                continue;
            }

            WorldCard card = slot.OccupiedCard;
            if (card != null && !card.IsInHand && slot.IsCorrectPlacement(card))
                correctlyPlaced++;
            else
                complete = false;
        }
    }

    /// <summary>The whole PSA cabinet is one skill row, regardless of its holder layout.</summary>
    public bool CopyCompletedSkillCardKeys(System.Collections.Generic.List<string> keys)
    {
        keys.Clear();
        EnsureSlotCache();
        if (slots == null || slots.Length == 0) return false;
        foreach (PsaCabinetSlot slot in slots)
        {
            if (slot == null || !slot.gameObject.activeInHierarchy || slot.IsEmpty) return false;
            WorldCard card = slot.OccupiedCard;
            if (!card || card.IsInHand || card.IsFlyingToShelf || !slot.IsCorrectPlacement(card)) return false;
            string key = "psa:" + (int)card.PsaSet + ":" + card.PsaSlotNumber + ":" + card.PsaVariantIndex;
            // A duplicated slab cannot fill two required places to manufacture progress.
            if (keys.Contains(key)) return false;
            keys.Add(key);
        }
        return keys.Count > 0;
    }

    public void SetSkillCompletionLock(bool locked)
    {
        EnsureSlotCache();
        if (slots == null) return;
        foreach (PsaCabinetSlot slot in slots)
            if (slot != null && slot.OccupiedCard != null) slot.OccupiedCard.SetSkillCompletionLocked(locked);
    }

    public bool AcceptsSkillCard(WorldCard card)
    {
        if (!card || !card.UsesPsaSlab) return false;
        EnsureSlotCache();
        if (slots == null) return false;
        foreach (PsaCabinetSlot slot in slots)
            if (slot != null && slot.gameObject.activeInHierarchy && slot.IsCorrectPlacement(card)) return true;
        return false;
    }

    public bool TryFindSkillSlot(WorldCard card, out PsaCabinetSlot result)
    {
        result = null;
        if (!card || !card.UsesPsaSlab) return false;
        EnsureSlotCache();
        if (slots == null) return false;
        foreach (PsaCabinetSlot slot in slots)
        {
            WorldCard occupied = slot != null ? slot.OccupiedCard : null;
            if (occupied != null && !occupied.IsInHand && occupied.UsesPsaSlab
                && occupied.PsaSet == card.PsaSet && occupied.PsaSlotNumber == card.PsaSlotNumber
                && occupied.PsaVariantIndex == card.PsaVariantIndex) return false;
        }
        foreach (PsaCabinetSlot slot in slots)
            if (slot != null && slot.gameObject.activeInHierarchy && slot.IsEmpty && slot.IsCorrectPlacement(card))
            { result = slot; return true; }
        return false;
    }

    public bool TryPlaceSkillCard(PlayerCardHand hand, WorldCard card)
    {
        return TryFindSkillSlot(card, out PsaCabinetSlot slot) && slot.TryPlaceSkillCard(hand, card);
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
        // Stable insertion sort keeps the authored hierarchy order for cabinets whose
        // holders all have the same PSA grade. Array.Sort could reshuffle those ties.
        for (int i = 1; i < slots.Length; i++)
        {
            PsaCabinetSlot current = slots[i];
            int j = i - 1;
            while (j >= 0 && slots[j].SlotNumber > current.SlotNumber)
            {
                slots[j + 1] = slots[j];
                j--;
            }
            slots[j + 1] = current;
        }
        _slotCacheValid = true;
    }

    public PsaCabinetSlot FindSlot(int slotNumber)
    {
        EnsureSlotCache();
        if (slots == null) return null;
        for (int i = 0; i < slots.Length; i++)
        {
            PsaCabinetSlot slot = slots[i];
            if (slot != null && slot.SlotNumber == slotNumber)
                return slot;
        }

        return null;
    }

    /// <summary>
    /// Holders stay empty on a new game. Ground scatter creates the four PSA cards (7–10).
    /// </summary>
    public void SpawnDefaultSlabs(Transform parentRoot)
    {
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

        EnsureSlotCache();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
