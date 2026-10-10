#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>In-memory checks only. Does not enter Play, create scenes, or write player saves.</summary>
public static class MinorSkillProgressValidation
{
    const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    [MenuItem("TCG Card Chaos/Validation/Validate Minor Skill Progress")]
    public static void Run() => Debug.Log(Validate());

    public static string Validate()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run this validation outside Play mode.");
        string[] minorNames = { "_ownedKeys", "_unlockedSkills", "<Ready>k__BackingField", "Changed", "Unlocked" };
        string[] dirtyNames = { "<IsDirty>k__BackingField", "<Revision>k__BackingField" };
        object[] minorSnapshot = CaptureFields(typeof(MinorSkillProgress), minorNames);
        object[] dirtySnapshot = CaptureFields(typeof(GameSaveDirtyTracker), dirtyNames);
        int checks = 0;

        void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("Minor skill validation failed: " + message);
        }

        try
        {
            // Real UI/world subscribers must never observe the temporary validation state.
            SetField(typeof(MinorSkillProgress), "Changed", null);
            SetField(typeof(MinorSkillProgress), "Unlocked", null);
            int changes = 0, unlocks = 0, lastUnlocked = -1;
            MinorSkillProgress.Changed += () => changes++;
            MinorSkillProgress.Unlocked += index => { unlocks++; lastUnlocked = index; };
            MinorSkillProgress.PrepareForSceneReload();
            Check(!MinorSkillProgress.Ready, "reload must suspend interactions");
            Check(!MinorSkillProgress.TryCollectKey(0) && !MinorSkillProgress.TryUnlock(0), "unready state must reject actions");
            ulong beforeRestore = GameSaveDirtyTracker.Revision;
            MinorSkillProgress.Restore(null);
            Check(MinorSkillProgress.Ready, "new/old save becomes ready");
            Check(GameSaveDirtyTracker.Revision == beforeRestore && unlocks == 0, "restore must not dirty saves or announce unlocks");
            for (int i = 0; i < MinorSkillProgress.Count; i++)
                Check(!MinorSkillProgress.HasKey(i) && !MinorSkillProgress.IsUnlocked(i), "null save begins with four locked slots");
            Check(!MinorSkillProgress.TryCollectKey(-1) && !MinorSkillProgress.TryCollectKey(4)
                && !MinorSkillProgress.TryUnlock(-1) && !MinorSkillProgress.TryUnlock(4), "invalid IDs must be rejected");

            for (int i = 0; i < MinorSkillProgress.Count; i++)
            {
                Check(MinorSkillProgress.TryCollectKey(i) && MinorSkillProgress.HasKey(i), "each key must be independently collectible");
                Check(GameSaveDirtyTracker.IsDirty, "collecting a key must mark the save dirty");
                int previousChanges = changes;
                ulong previousRevision = GameSaveDirtyTracker.Revision;
                Check(!MinorSkillProgress.TryCollectKey(i), "duplicate key pickup must be rejected");
                Check(!MinorSkillProgress.TryUnlock((i + 1) % MinorSkillProgress.Count), "wrong key must not open another chest");
                Check(MinorSkillProgress.HasKey(i) && previousChanges == changes
                    && previousRevision == GameSaveDirtyTracker.Revision, "rejected actions must not consume key, emit changes, or dirty save");
                Check(MinorSkillProgress.TryUnlock(i), "matching key must unlock its chest");
                Check(MinorSkillProgress.IsUnlocked(i) && !MinorSkillProgress.HasKey(i), "unlock must consume matching key");
                Check(unlocks == i + 1 && lastUnlocked == i, "one notification for each correct slot");
                previousChanges = changes;
                previousRevision = GameSaveDirtyTracker.Revision;
                Check(!MinorSkillProgress.TryUnlock(i) && !MinorSkillProgress.TryCollectKey(i), "opened chest cannot grant another key or unlock");
                Check(previousChanges == changes && previousRevision == GameSaveDirtyTracker.Revision,
                    "repeated chest use must be a no-op");
            }

            MinorSkillProgress.Restore(null);
            MinorSkillProgress.TryCollectKey(0);
            MinorSkillProgress.TryUnlock(0);
            MinorSkillProgress.TryCollectKey(1);
            int notificationsBeforeRestore = unlocks;
            var gameSave = new GameSaveData { minorSkills = MinorSkillProgress.Capture() };
            GameSaveData loaded = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(gameSave));
            MinorSkillProgress.Restore(loaded.minorSkills);
            Check(MinorSkillProgress.IsUnlocked(0) && !MinorSkillProgress.HasKey(0)
                && MinorSkillProgress.HasKey(1) && !MinorSkillProgress.IsUnlocked(1), "JSON roundtrip must preserve held keys and opened chests");
            Check(unlocks == notificationsBeforeRestore, "restoring an opened chest must not replay its popup");
            loaded.minorSkills.ownedKeysMask = 0;
            Check(MinorSkillProgress.HasKey(1), "restore must not retain mutable save-record state");
            MinorSkillSaveRecord detachedCapture = MinorSkillProgress.Capture();
            detachedCapture.ownedKeysMask = 0;
            Check(MinorSkillProgress.HasKey(1), "capture must return an independent save record");

            var corrupt = new MinorSkillSaveRecord { ownedKeysMask = -1, unlockedSkillsMask = (1 << 2) | (1 << 30) };
            MinorSkillProgress.Restore(corrupt);
            MinorSkillSaveRecord cleaned = MinorSkillProgress.Capture();
            Check(cleaned.unlockedSkillsMask == 4 && cleaned.ownedKeysMask == 11, "restore must sanitize unknown IDs and spent keys");
            MinorSkillProgress.PrepareForSceneReload();
            Check(!MinorSkillProgress.Ready && MinorSkillProgress.Capture().ownedKeysMask == 0
                && MinorSkillProgress.Capture().unlockedSkillsMask == 0, "reload must clear previous-world state before new objects enable");
            MinorSkillProgress.Restore(cleaned);
            Check(MinorSkillProgress.Ready && MinorSkillProgress.IsUnlocked(2) && MinorSkillProgress.HasKey(3), "continue restores after reload");
            GameSaveData legacy = JsonUtility.FromJson<GameSaveData>("{\"saveVersion\":1}");
            // Unity may materialize a zero-filled optional serializable object for an
            // absent JSON field; both null and this empty record represent locked progress.
            Check(legacy.minorSkills == null || (legacy.minorSkills.ownedKeysMask == 0
                && legacy.minorSkills.unlockedSkillsMask == 0), "old saves must default the absent minorSkills field to empty progress");
            beforeRestore = GameSaveDirtyTracker.Revision;
            notificationsBeforeRestore = unlocks;
            MinorSkillProgress.Restore(legacy.minorSkills);
            Check(MinorSkillProgress.Capture().ownedKeysMask == 0 && MinorSkillProgress.Capture().unlockedSkillsMask == 0,
                "new game and old saves reset all minor progression");
            Check(GameSaveDirtyTracker.Revision == beforeRestore && unlocks == notificationsBeforeRestore,
                "old-save restore must not dirty saves or announce unlocks");
            Check(!MinorSkillInteraction.CanInteract(), "world interactions must reject Edit mode");
            // Every acquisition order has one of these 16 masks. No stacking from reopening a chest.
            for (int mask = 0; mask < 16; mask++)
            {
                MinorSkillProgress.Restore(new MinorSkillSaveRecord { unlockedSkillsMask = mask });
                int expectedCapacity = 10 + ((mask & 4) != 0 ? 2 : 0) + ((mask & 8) != 0 ? 3 : 0);
                Check(CardDimensions.MaxHandSize == expectedCapacity, "mask " + mask + " capacity");
                Check(PlayerJumpSkill.HeightMultiplier == ((mask & 1) != 0 ? 5f : 1f), "mask " + mask + " single-press jump height");
                Check(Mathf.Approximately(MinorSkillProgress.MovementMultiplier, (mask & 2) != 0 ? 1.35f : 1f),
                    "mask " + mask + " permanent movement speed");
                var roundtrip = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(new GameSaveData { minorSkills = MinorSkillProgress.Capture() }));
                MinorSkillProgress.PrepareForSceneReload();
                Check(CardDimensions.MaxHandSize == 10 && PlayerJumpSkill.HeightMultiplier == 1f
                    && MinorSkillProgress.MovementMultiplier == 1f, "reload clears all passive effects");
                MinorSkillProgress.Restore(roundtrip.minorSkills);
                Check(CardDimensions.MaxHandSize == expectedCapacity, "saved capacity restored before held items");
            }
            return "[Minor Skills] " + checks + " in-memory checks passed; progression, save JSON, old-save defaults, duplicate/wrong-key guards, reload and notifications verified.";
        }
        finally
        {
            RestoreFields(typeof(MinorSkillProgress), minorNames, minorSnapshot);
            RestoreFields(typeof(GameSaveDirtyTracker), dirtyNames, dirtySnapshot);
        }
    }

    static object[] CaptureFields(Type type, string[] names)
    {
        var values = new object[names.Length];
        for (int i = 0; i < names.Length; i++) values[i] = Field(type, names[i]).GetValue(null);
        return values;
    }

    static void RestoreFields(Type type, string[] names, object[] values)
    {
        for (int i = 0; i < names.Length; i++) SetField(type, names[i], values[i]);
    }

    static void SetField(Type type, string name, object value) => Field(type, name).SetValue(null, value);
    static FieldInfo Field(Type type, string name) => type.GetField(name, StaticPrivate)
        ?? throw new MissingFieldException(type.FullName, name);
}
#endif
