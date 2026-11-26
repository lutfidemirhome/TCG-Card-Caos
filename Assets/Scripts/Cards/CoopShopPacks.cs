using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class CoopShopCards
{
    readonly Dictionary<Guid, ulong> packHolders = new Dictionary<Guid, ulong>();
    readonly Dictionary<Guid, Transform> packAnchors = new Dictionary<Guid, Transform>();
    readonly Dictionary<Guid, WorldBoosterPack> movingPacks = new Dictionary<Guid, WorldBoosterPack>();
    readonly Dictionary<Guid, uint> packSamples = new Dictionary<Guid, uint>();
    readonly List<Guid> packChanges = new List<Guid>();
    float nextPackSample;

    public static bool ControlsPack(WorldBoosterPack pack) => Active != null && Active.layout.PackId(pack, out _);

    public bool PickupPack(WorldBoosterPack pack) => layout.PackId(pack, out var id)
        && Request(new CoopCardMessage { Card = id, Action = CoopCardAction.Take });

    public bool ThrowPack(WorldBoosterPack pack, Vector3 velocity) => layout.PackId(pack, out var id)
        && Request(new CoopCardMessage { Card = id, Action = CoopCardAction.Throw,
            Position = pack.transform.position, Rotation = pack.transform.rotation, Velocity = velocity });

    bool ChangePack(ulong actor, CoopCardMessage old, CoopCardMessage m, WorldBoosterPack pack)
    {
        if (packOwners.ContainsKey(m.Card)) return false;
        if (m.Action == CoopCardAction.Take)
        {
            if (packHolders.ContainsKey(m.Card) || !InReach(actor, pack.transform.position)
                || HeldCount(actor) >= CardDimensions.MaximumUpgradedHandSize) return false;
            m.Position = pack.transform.position;
            m.Rotation = pack.transform.rotation;
            m.Action = CoopCardAction.Held;
        }
        else
        {
            if (old.Action != CoopCardAction.Held || old.Actor != actor) return false;
            if (m.Action == CoopCardAction.Return) { DropAbandonedPack(m.Card); return true; }
            if (!InReach(actor, m.Position) || !(m.Velocity.sqrMagnitude <= 100f) || !UnitRotation(m.Rotation)) return false;
            m.Action = CoopCardAction.Flight;
        }
        Commit(m);
        return true;
    }

    void DropAbandonedPack(Guid id)
    {
        var pack = layout.AvailablePack(id);
        if (!pack) return;
        var body = playerBody(packHolders[id]);
        Commit(new CoopCardMessage { Card = id, Action = CoopCardAction.Ground,
            Position = body ? FloorBy(body) : pack.transform.position, Rotation = Quaternion.identity });
    }

    bool ApplyPack(CoopCardMessage m, WorldBoosterPack pack)
    {
        ClearPending(m.Card);
        states[m.Card] = m;
        var hand = PlayerCardHand.Instance;
        if (packHolders.TryGetValue(m.Card, out ulong previous) && previous == local && hand)
            hand.ClearHeldPackReference(pack);
        packHolders.Remove(m.Card);
        packSamples.Remove(m.Card);
        if (m.Action == CoopCardAction.Held)
        {
            movingPacks.Remove(m.Card);
            packHolders[m.Card] = m.Actor;
            if (m.Actor == local)
            {
                if (!hand || !hand.CanPickUpPack || !hand.RestoreHeldPack(pack))
                    Request(new CoopCardMessage { Card = m.Card, Action = CoopCardAction.Return });
                else hand.SelectRightmostFanEntry();
            }
            else
            {
                var anchor = new GameObject("Held co-op pack").transform;
                PositionPackAnchor(anchor, m.Actor);
                packAnchors[m.Card] = anchor;
                pack.RestoreIntoHand(anchor, CardDimensions.GroundCardScale);
            }
            return true;
        }
        if (m.Action != CoopCardAction.Flight && m.Action != CoopCardAction.Ground) return false;
        if (local == host && m.Action == CoopCardAction.Flight && !pack.IsPhysicsSimulating)
        {
            pack.transform.SetPositionAndRotation(m.Position, m.Rotation);
            pack.DropWithPhysics(m.Velocity);
        }
        else if (local != host || !pack.IsPhysicsSimulating)
        {
            pack.transform.SetParent(null, true);
            pack.transform.localScale = Vector3.one * CardDimensions.GroundCardScale;
            pack.transform.SetPositionAndRotation(m.Position, m.Rotation);
            pack.RestoreSavedWorldPose(false, 0, true);
            CardGroundStack.TrackPhysicsPack(pack);
        }
        RemovePackAnchor(m.Card);
        if (local == host && pack.IsPhysicsSimulating) movingPacks[m.Card] = pack;
        return true;
    }

    void RemovePackAnchor(Guid id)
    {
        if (packAnchors.TryGetValue(id, out var anchor) && anchor) UnityEngine.Object.Destroy(anchor.gameObject);
        packAnchors.Remove(id);
    }

    bool SamplePack(CoopCardMessage m)
    {
        var pack = layout.AvailablePack(m.Card);
        if (!pack) return false;
        if (!states.TryGetValue(m.Card, out var state) || state.Action != CoopCardAction.Flight || state.Revision != m.Revision) return true;
        if (packSamples.TryGetValue(m.Card, out var seq) && seq >= m.Request) return true;
        packSamples[m.Card] = m.Request;
        pack.transform.SetPositionAndRotation(m.Position, m.Rotation);
        CardGroundStack.TrackPhysicsPack(pack);
        return true;
    }

    void PositionPackAnchor(Transform anchor, ulong owner)
    {
        var body = playerBody(owner);
        if (anchor.gameObject.activeSelf != (body != null)) anchor.gameObject.SetActive(body != null);
        if (body) anchor.SetPositionAndRotation(body.TransformPoint(new Vector3(.35f, 1.1f, .3f)), body.rotation);
    }

    void TickPacks()
    {
        foreach (var pair in packAnchors) PositionPackAnchor(pair.Value, packHolders[pair.Key]);
        if (local != host) return;
        packChanges.Clear();
        foreach (var pair in packHolders) if (!IsMember(pair.Value)) packChanges.Add(pair.Key);
        foreach (var id in packChanges) DropAbandonedPack(id);
        if (Time.unscaledTime < nextPackSample) return;
        nextPackSample = Time.unscaledTime + 1f / 15f;
        packChanges.Clear();
        packChanges.AddRange(movingPacks.Keys);
        foreach (var id in packChanges)
        {
            var pack = movingPacks[id];
            var body = pack ? pack.PhysicsBody : null;
            if (!body) { movingPacks.Remove(id); continue; }
            var m = states[id];
            bool resting = body.IsSleeping();
            if (resting && m.Action == CoopCardAction.Ground) continue;
            m.Position = body.position;
            m.Rotation = body.rotation;
            if (resting || m.Action == CoopCardAction.Ground)
            {
                m.Action = resting ? CoopCardAction.Ground : CoopCardAction.Flight;
                Commit(m);
            }
            else
            {
                packSamples.TryGetValue(id, out uint seq);
                m.Request = packSamples[id] = seq + 1;
                m.Action = CoopCardAction.Motion;
                BroadcastMotion(m);
            }
        }
    }

    void DisposePacks()
    {
        foreach (var id in packHolders.Keys)
        {
            var pack = layout.AvailablePack(id);
            if (!pack) continue;
            if (PlayerCardHand.Instance) PlayerCardHand.Instance.ClearHeldPackReference(pack);
            pack.transform.SetParent(null, true);
            pack.RestoreSavedWorldPose(false, 0);
        }
        foreach (var anchor in packAnchors.Values) if (anchor) UnityEngine.Object.Destroy(anchor.gameObject);
    }
}
