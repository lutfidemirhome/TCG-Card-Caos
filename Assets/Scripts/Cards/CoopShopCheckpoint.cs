using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public sealed partial class CoopShopCards
{
    [Serializable] class Checkpoint { public int version; public List<Item> items; }
    [Serializable] class Item
    {
        public string id, slot;
        public Vector3 position;
        public Quaternion rotation;
        public bool physicalPose;
        public bool keyUsed;
        public ThrownPhysicsSaveState physics;
    }
    string checkpointPath;
    uint savedRevision;
    bool checkpointInMotion;
    bool recoveredBackup;
    float nextCheckpoint;
    Task<string> pendingWrite;
    uint pendingRevision;
    bool pendingMotion;

    public static string SavePath(ulong owner) => Path.Combine(Application.persistentDataPath, "coop-" + owner + ".json");
    public static bool HasSave(ulong owner) => File.Exists(SavePath(owner)) || File.Exists(SavePath(owner) + ".bak");

    public bool UseCheckpoint(string path, bool resume)
    {
        if (local != host || string.IsNullOrEmpty(path)) return false;
        if (resume)
        {
            List<(CoopCardMessage message, ThrownPhysicsSaveState physics)> placements = null;
            foreach (string candidate in new[] { path, path + ".bak" })
            {
                try
                {
                    if (!File.Exists(candidate)) continue;
                    placements = DecodeCheckpoint(File.ReadAllText(candidate));
                    if (placements != null) { recoveredBackup = candidate != path; break; }
                }
                catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is UnauthorizedAccessException)
                { Debug.LogWarning("Co-op save: " + ex.Message); }
            }
            if (placements == null) return false;
            foreach (var placement in placements)
            {
                var m = placement.message;
                Commit(m, true);
                if (placement.physics == null || !placement.physics.isSimulating) continue;
                var pack = layout.AvailablePack(m.Card);
                var body = pack ? pack.PhysicsBody : layout.Cards[m.Card].PhysicsBody;
                if (!body) continue;
                body.transform.SetPositionAndRotation(m.Position, m.Rotation);
                placement.physics.Apply(body);
                checkpointInMotion |= !placement.physics.isSleeping;
                if (placement.physics.isSleeping)
                {
                    m.Action = CoopCardAction.Ground;
                    Commit(m);
                }
            }
        }
        checkpointPath = path;
        savedRevision = resume && !recoveredBackup ? revision : uint.MaxValue;
        return FlushCheckpoint();
    }

    List<(CoopCardMessage message, ThrownPhysicsSaveState physics)> DecodeCheckpoint(string json)
    {
        var data = JsonUtility.FromJson<Checkpoint>(json);
        if (data == null || data.version != 1 || data.items == null) return null;
        var result = new List<(CoopCardMessage, ThrownPhysicsSaveState)>(data.items.Count);
        var ids = new HashSet<Guid>();
        var slots = new HashSet<Guid>();
        foreach (var item in data.items)
        {
            if (item == null || !Guid.TryParse(item.id, out var id) || id == Guid.Empty || !ids.Add(id)
                || !UnitRotation(item.rotation) || !(item.position.sqrMagnitude < 1e10f)) return null;
            if (keys.ContainsKey(id))
            {
                if (!string.IsNullOrEmpty(item.slot) || (item.physics != null && item.physics.isSimulating) || item.physicalPose) return null;
                result.Add((new CoopCardMessage { Card = id, Action = item.keyUsed ? CoopCardAction.KeyUsed : CoopCardAction.KeyGround,
                    Position = item.position, Rotation = item.rotation }, null));
                continue;
            }
            if (item.keyUsed) return null;
            if (!layout.AvailablePack(id) && !layout.Cards.ContainsKey(id) && !layout.SourcePack(id, out _)) return null;
            var m = new CoopCardMessage { Card = id, Position = item.position, Rotation = item.rotation,
                PhysicalPose = (byte)(item.physicalPose ? 1 : 0), Action = CoopCardAction.Ground };
            if (!string.IsNullOrEmpty(item.slot))
            {
                if (!Guid.TryParse(item.slot, out m.Slot) || !layout.CanRestorePlacement(id, m.Slot) || !slots.Add(m.Slot)) return null;
                m.Action = CoopCardAction.OnShelf;
            }
            if (item.physics != null && item.physics.isSimulating)
            {
                if (m.Action != CoopCardAction.Ground) return null;
                m.Action = CoopCardAction.Flight;
            }
            result.Add((m, item.physics));
        }
        foreach (Guid id in ids)
            if (layout.SourcePack(id, out var pack))
            {
                if (ids.Contains(pack)) return null;
                foreach (Guid sibling in layout.Products(pack)) if (!ids.Contains(sibling)) return null;
            }
        return result;
    }

    void TickCheckpoint()
    {
        if (pendingWrite != null)
        {
            if (!pendingWrite.IsCompleted) return;
            FinishPendingWrite();
        }
        if (checkpointPath == null || Time.unscaledTime < nextCheckpoint) return;
        nextCheckpoint = Time.unscaledTime + 4f;
        if (savedRevision == revision && !checkpointInMotion) return;
        var data = CaptureCheckpoint(out pendingMotion);
        string path = checkpointPath;
        bool preserveBackup = recoveredBackup;
        pendingRevision = revision;
        pendingWrite = Task.Run(() => WriteCheckpoint(path, data, preserveBackup));
    }

    public bool FlushCheckpoint()
    {
        if (pendingWrite != null) FinishPendingWrite();
        if (checkpointPath == null || (savedRevision == revision && !checkpointInMotion)) return true;
        var data = CaptureCheckpoint(out bool moving);
        return RecordWrite(WriteCheckpoint(checkpointPath, data, recoveredBackup), revision, moving);
    }

    void FinishPendingWrite()
    {
        string error = pendingWrite.GetAwaiter().GetResult();
        pendingWrite = null;
        RecordWrite(error, pendingRevision, pendingMotion);
    }

    static string WriteCheckpoint(string path, Checkpoint data, bool preserveBackup)
    {
        try { return SaveFileIO.TryWriteAtomic(path, JsonUtility.ToJson(data), out var error, preserveBackup) ? null : error; }
        catch (Exception ex) { return ex.Message; }
    }

    bool RecordWrite(string error, uint writtenRevision, bool moving)
    {
        if (error != null) { Debug.LogError("Co-op save: " + error); return false; }
        savedRevision = writtenRevision;
        checkpointInMotion = moving;
        recoveredBackup = false;
        return true;
    }

    // Read live transforms on the main thread before handing a detached snapshot to the writer.
    Checkpoint CaptureCheckpoint(out bool moving)
    {
        moving = false;
        var data = new Checkpoint { version = 1, items = new List<Item>() };
        // A series claims a shelf row in placement order. Rebuild that same order after a reload.
        var changes = new List<CoopCardMessage>(states.Values);
        changes.Sort((a, b) => a.Revision.CompareTo(b.Revision));
        foreach (var m in changes)
        {
            if (IsKeyState(m.Action))
            {
                var owner = m.Action == CoopCardAction.KeyHeld ? playerBody(m.Actor) : null;
                data.items.Add(new Item { id = m.Card.ToString("N"), position = owner ? FloorBy(owner) : m.Position,
                    rotation = keys[m.Card].AuthoredRotation, keyUsed = m.Action == CoopCardAction.KeyUsed });
                continue;
            }
            var pack = layout.AvailablePack(m.Card);
            layout.Cards.TryGetValue(m.Card, out var card);
            if (!layout.UnconsumedPack(m.Card) && !card) continue;
            Transform t = pack ? pack.transform : card ? card.transform : null;
            Vector3 p = t ? t.position : m.Position;
            Quaternion q = t ? t.rotation : m.Rotation;
            if (m.Action == CoopCardAction.Held || m.Action == CoopCardAction.PackClaimed)
            {
                var body = playerBody(m.Actor);
                if (body) p = FloorBy(body);
                q = Quaternion.identity;
            }
            var physics = m.PhysicalPose == 0 ? null : ThrownPhysicsSaveState.Capture(pack ? pack.PhysicsBody : card ? card.PhysicsBody : null);
            moving |= physics != null && !physics.isSleeping;
            data.items.Add(new Item { id = m.Card.ToString("N"), position = p, rotation = q, physics = physics,
                physicalPose = m.PhysicalPose != 0 && (m.Action == CoopCardAction.Flight || m.Action == CoopCardAction.Ground),
                slot = m.Action == CoopCardAction.OnShelf ? m.Slot.ToString("N") : null });
        }
        return data;
    }
}
