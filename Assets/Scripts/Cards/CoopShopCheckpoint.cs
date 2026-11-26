using System;
using System.Collections.Generic;
using System.IO;
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
        public ThrownPhysicsSaveState physics;
    }
    string checkpointPath;
    uint savedRevision;
    bool checkpointInMotion;
    float nextCheckpoint;

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
                    if (placements != null) break;
                }
                catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is UnauthorizedAccessException)
                { Debug.LogWarning("Co-op save: " + ex.Message); }
            }
            if (placements == null) return false;
            foreach (var placement in placements)
            {
                var m = placement.message;
                Commit(m);
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
        savedRevision = resume ? revision : uint.MaxValue;
        return true;
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
            if (!layout.AvailablePack(id) && !layout.Cards.ContainsKey(id) && !layout.SourcePack(id, out _)) return null;
            var m = new CoopCardMessage { Card = id, Position = item.position, Rotation = item.rotation,
                PhysicalPose = (byte)(item.physicalPose ? 1 : 0), Action = CoopCardAction.Ground };
            if (!string.IsNullOrEmpty(item.slot))
            {
                if (!Guid.TryParse(item.slot, out m.Slot) || !layout.Slots.ContainsKey(m.Slot) || !slots.Add(m.Slot)) return null;
                m.Action = CoopCardAction.OnShelf;
            }
            if (item.physics != null && item.physics.isSimulating)
            {
                if (m.Action != CoopCardAction.Ground) return null;
                m.Action = CoopCardAction.Flight;
            }
            result.Add((m, item.physics));
        }
        return result;
    }

    void TickCheckpoint()
    {
        if (checkpointPath == null || Time.unscaledTime < nextCheckpoint) return;
        nextCheckpoint = Time.unscaledTime + 4f;
        FlushCheckpoint();
    }

    // Hand contents are set down so that absent guests cannot lock items away.
    public bool FlushCheckpoint()
    {
        if (checkpointPath == null || (savedRevision == revision && !checkpointInMotion)) return true;
        bool moving = false;
        var data = new Checkpoint { version = 1, items = new List<Item>() };
        // A series claims a shelf row in placement order. Rebuild that same order after a reload.
        var changes = new List<CoopCardMessage>(states.Values);
        changes.Sort((a, b) => a.Revision.CompareTo(b.Revision));
        foreach (var m in changes)
        {
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
        if (!SaveFileIO.TryWriteAtomic(checkpointPath, JsonUtility.ToJson(data), out var error))
        { Debug.LogError("Co-op save: " + error); return false; }
        savedRevision = revision;
        checkpointInMotion = moving;
        return true;
    }
}
