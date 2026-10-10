using System;
using UnityEngine;

[Serializable]
public sealed class MinorSkillSaveRecord
{
    public int version = 3;
    public MinorKeyGroundRecord[] groundKeys;
    public bool chestIntroductionSeen;
    public int ownedKeysMask;
    public int unlockedSkillsMask;
}

/// <summary>
/// Four permanent key/chest identities. These IDs do not depend on scene names,
/// object positions, or the presentation order of the main skill system.
/// </summary>
public static class MinorSkillProgress
{
    public const int Count = 4;
    // Stable chest/save identities, independent of labels and icon order.
    public const int LongJump = 0;
    public const int Run = 1;
    public const int CapacityPlusTwo = 2;
    public const int CapacityPlusThree = 3;
    public const float UnlockedRunMultiplier = 1.35f;
    const int ValidMask = (1 << Count) - 1;
    public static bool ChestIntroductionSeen { get; private set; }
    public static void MarkChestIntroductionSeen()
    {
        ChestIntroductionSeen = true;
        GameSaveDirtyTracker.MarkDirty();
    }
    static int _ownedKeys;
    static int _unlockedSkills;

    public static bool Ready { get; private set; }
    public static event Action Changed;
    public static event Action<int> Unlocked;

    public static float MovementMultiplier => IsUnlocked(Run) ? UnlockedRunMultiplier : 1f;
    public static int ExtraHandSlots => (IsUnlocked(CapacityPlusTwo) ? 2 : 0)
        + (IsUnlocked(CapacityPlusThree) ? 3 : 0);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Ready = false;
        ChestIntroductionSeen = false;
        _ownedKeys = _unlockedSkills = 0;
        Changed = null;
        Unlocked = null;
    }

    public static bool IsValidIndex(int index) => index >= 0 && index < Count;
    public static bool IsUnlocked(int index) => IsValidIndex(index) && (_unlockedSkills & (1 << index)) != 0;
    public static bool HasKey(int index) => IsValidIndex(index) && (_ownedKeys & (1 << index)) != 0;

    public static void PrepareForSceneReload()
    {
        Ready = false;
        ChestIntroductionSeen = false;
        _ownedKeys = _unlockedSkills = 0;
        Changed?.Invoke();
    }

    public static bool TryCollectKey(int index)
    {
        if (!Ready || !IsValidIndex(index) || IsUnlocked(index) || HasKey(index)) return false;
        _ownedKeys |= 1 << index;
        GameSaveDirtyTracker.MarkDirty();
        Changed?.Invoke();
        return true;
    }

    public static bool TryDropKey(int index)
    {
        if (!Ready || !HasKey(index) || IsUnlocked(index)) return false;
        _ownedKeys &= ~(1 << index);
        GameSaveDirtyTracker.MarkDirty();
        Changed?.Invoke();
        return true;
    }

    public static bool TryUnlock(int index)
    {
        if (!Ready || !IsValidIndex(index) || IsUnlocked(index) || !HasKey(index)) return false;
        _ownedKeys &= ~(1 << index);
        _unlockedSkills |= 1 << index;
        GameSaveDirtyTracker.MarkDirty();
        Changed?.Invoke();
        // Restoring a save must never display a new-unlock popup.
        Unlocked?.Invoke(index);
        return true;
    }

    public static MinorSkillSaveRecord Capture() => new MinorSkillSaveRecord
    {
        chestIntroductionSeen = ChestIntroductionSeen,
        ownedKeysMask = _ownedKeys,
        unlockedSkillsMask = _unlockedSkills,
        groundKeys = WorldMinorSkillKey.CaptureGroundStates()
    };

    public static void Restore(MinorSkillSaveRecord saved)
    {
        ChestIntroductionSeen = saved != null && saved.chestIntroductionSeen;
        _unlockedSkills = saved != null ? saved.unlockedSkillsMask & ValidMask : 0;
        // A key already spent on its chest cannot also remain in inventory.
        _ownedKeys = saved != null ? saved.ownedKeysMask & ValidMask & ~_unlockedSkills : 0;
        WorldMinorSkillKey.RestoreGroundStates(saved?.groundKeys);
        Ready = true;
        Changed?.Invoke();
    }
}
