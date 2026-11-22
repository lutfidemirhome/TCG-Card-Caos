using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public enum CoopCardAction : byte { Sync, Take, Collect, Throw, Shelve, OpenPack, CancelPack, Return, Denied, PackClaimed, PackReleased, Held, OnShelf, Flight, Ground, Motion }

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CoopCardMessage
{
    public CoopCardAction Action;
    public Guid Card, Slot;
    public ulong Actor;
    public uint Revision, Request;
    public Vector3 Position, Velocity;
    public Quaternion Rotation;
}

public sealed class CoopShopCards : IDisposable
{
    static readonly Guid EntranceCard = new Guid("fdc2d8ec316e4d94bbe9b1456e942322");
    readonly ulong local, host;
    readonly IReadOnlyList<ulong> members;
    readonly Action<ulong, CoopCardMessage> send;
    readonly Func<ulong, Transform> playerBody;
    readonly Dictionary<Guid, CoopCardMessage> states = new Dictionary<Guid, CoopCardMessage>();
    readonly Dictionary<Guid, CoopCardMessage> pending = new Dictionary<Guid, CoopCardMessage>();
    readonly Dictionary<Guid, ulong> packOwners = new Dictionary<Guid, ulong>();
    readonly Dictionary<Guid, Transform> remoteHands = new Dictionary<Guid, Transform>();
    readonly HashSet<Guid> held = new HashSet<Guid>();
    readonly List<Guid> changed = new List<Guid>();
    readonly List<CoopCardMessage> retry = new List<CoopCardMessage>();
    // Reliable changes are replayable in commit order. An untouched authored card needs no packet.
    readonly List<CoopCardMessage> history = new List<CoopCardMessage>();
    readonly Dictionary<ulong, uint> cursors = new Dictionary<ulong, uint>();
    readonly CoopCardFlights flights;
    readonly CoopShopLayout layout;
    uint revision, nextRequest;
    int reservations;
    float nextSync, nextRetry;
    public static CoopShopCards Active { get; private set; }
    public static int ReservedSlots => Active?.reservations ?? 0;
    public static bool Controls(WorldCard card) => card && Active != null && Active.layout.CardIds.ContainsKey(card);
    public static bool SlotPending(Component slot) => Active != null && Active.IsSlotPending(slot);

    public CoopShopCards(ulong local, ulong host, IReadOnlyList<ulong> members,
        Action<ulong, CoopCardMessage> send, Func<ulong, Transform> playerBody)
    {
        this.local = local;
        this.host = host;
        this.members = members;
        this.send = send;
        this.playerBody = playerBody;
        layout = new CoopShopLayout();
        flights = new CoopCardFlights(local == host, BroadcastMotion, Commit);
        SetUpEntranceCard();
        Active = this;
    }

    void SetUpEntranceCard()
    {
        if (!layout.Cards.TryGetValue(EntranceCard, out var card)) return;
        var counter = GameObject.Find("KartTutucu9 (2)/Counter_attlsv");
        if (!counter || !counter.TryGetComponent(out Collider top)) return;
        var b = top.bounds;
        var above = b.center + new Vector3(b.extents.x * .52f, b.extents.y + .35f, -b.extents.z * .66f);
        if (top.Raycast(new Ray(above, Vector3.down), out var hit, b.size.y + .7f))
            CoopCardFlights.ShowGround(card, hit.point + Vector3.up * (.002f + CardDimensions.Thickness * CardDimensions.GroundCardScale * .5f), Quaternion.identity);
    }

    public void Tick()
    {
        if (local == host)
        {
            changed.Clear();
            foreach (var id in held) if (!IsMember(states[id].Actor)) changed.Add(id);
            foreach (var id in changed) Release(id);
            changed.Clear();
            foreach (var pair in packOwners) if (!IsMember(pair.Value)) changed.Add(pair.Key);
            foreach (var id in changed) Commit(new CoopCardMessage { Card = id, Action = CoopCardAction.PackReleased });
            for (int i = 0; i < members.Count; i++)
            {
                ulong peer = members[i];
                if (peer == local || !cursors.TryGetValue(peer, out uint cursor)) continue;
                int budget = 16;
                while (cursor < history.Count && budget-- > 0) send(peer, history[(int)cursor++]);
                cursors[peer] = cursor;
            }
        }
        else
        {
            float now = Time.unscaledTime;
            if (now >= nextSync)
            {
                nextSync = now + 2f;
                send(host, new CoopCardMessage { Action = CoopCardAction.Sync, Revision = revision });
            }
            if (pending.Count != 0 && now >= nextRetry)
            {
                nextRetry = now + 1f;
                retry.Clear();
                retry.AddRange(pending.Values);
                foreach (var request in retry) send(host, request);
            }
        }
        flights.Tick();
        foreach (var pair in remoteHands)
        {
            Transform body = playerBody(states[pair.Key].Actor);
            var anchor = pair.Value;
            if (anchor.gameObject.activeSelf != (body != null)) anchor.gameObject.SetActive(body != null);
            if (body) anchor.SetPositionAndRotation(body.TransformPoint(new Vector3(.35f, 1.1f, .3f)), body.rotation);
        }
    }

    public bool RequestPickup(WorldCard card) =>
        Request(new CoopCardMessage { Action = CoopCardAction.Take, Card = layout.CardIds[card] });

    public bool RequestShelf(WorldCard card, Component slot)
    {
        if (!slot || !layout.SlotIds.TryGetValue(slot, out var key) || !layout.Slots[key].CanPlace(card) || IsSlotPending(slot)) return false;
        return Request(new CoopCardMessage { Action = CoopCardAction.Shelve, Card = layout.CardIds[card], Slot = key });
    }

    bool IsSlotPending(Component slot)
    {
        if (pending.Count == 0 || !slot || !layout.SlotIds.TryGetValue(slot, out var key)) return false;
        foreach (var request in pending.Values) if (request.Action == CoopCardAction.Shelve && request.Slot == key) return true;
        return false;
    }

    public bool RequestThrow(WorldCard card, Vector3 velocity) => Request(new CoopCardMessage
    {
        Action = CoopCardAction.Throw, Card = layout.CardIds[card], Position = card.transform.position,
        Rotation = card.transform.rotation, Velocity = velocity
    });

    public static void BindReveal(WorldBoosterPack pack, int index, WorldCard card) => Active?.layout.BindReveal(pack, index, card);
    public static void DiscardReveal(WorldCard preview) => Active?.layout.DiscardPreview(preview);

    public static bool CollectReveal(WorldCard preview)
    {
        if (Active == null || !Active.layout.ConsumePreview(preview, out var id)) return false;
        Active.Request(new CoopCardMessage { Action = CoopCardAction.Collect, Card = id });
        return true;
    }

    public bool OpenPack(WorldBoosterPack pack, PlayerCardHand hand)
    {
        if (!layout.PackId(pack, out var id)) return hand.BeginPackOpening(pack);
        return !packOwners.ContainsKey(id) && Request(new CoopCardMessage { Action = CoopCardAction.OpenPack, Card = id });
    }

    public void FinishPackOpening(WorldBoosterPack pack)
    {
        if (layout.PackId(pack, out var id) && packOwners.TryGetValue(id, out ulong owner) && owner == local)
            Request(new CoopCardMessage { Action = CoopCardAction.CancelPack, Card = id });
    }

    static bool NeedsSlot(CoopCardAction action) => action == CoopCardAction.Take || action == CoopCardAction.Collect;

    bool Request(CoopCardMessage request)
    {
        if (pending.ContainsKey(request.Card)) return false;
        states.TryGetValue(request.Card, out var previous);
        if (previous.Action == CoopCardAction.Held && NeedsSlot(request.Action)) return false;
        bool packRequest = request.Action == CoopCardAction.OpenPack || request.Action == CoopCardAction.CancelPack;
        if (!packRequest && !NeedsSlot(request.Action) && (previous.Action != CoopCardAction.Held || previous.Actor != local)) return false;
        request.Request = ++nextRequest;
        request.Revision = previous.Revision;
        pending.Add(request.Card, request);
        if (NeedsSlot(request.Action)) reservations++;
        if (local != host) { send(host, request); return true; }
        return HandleRequest(local, request);
    }

    void ClearPending(Guid id)
    {
        if (!pending.TryGetValue(id, out var request)) return;
        if (NeedsSlot(request.Action)) reservations--;
        pending.Remove(id);
    }

    public void Receive(ulong sender, CoopCardMessage m)
    {
        if (!IsMember(sender)) return;
        if (local == host)
        {
            if (m.Action == CoopCardAction.Sync)
                cursors[sender] = Math.Min(m.Revision, revision);
            else if (m.Action >= CoopCardAction.Take && m.Action <= CoopCardAction.Return && m.Request != 0)
                HandleRequest(sender, m);
            return;
        }
        if (sender != host) return;
        if (m.Action == CoopCardAction.Denied)
        {
            if (pending.TryGetValue(m.Card, out var request) && request.Request == m.Request) ClearPending(m.Card);
            return;
        }
        if (m.Action == CoopCardAction.Motion) { flights.Sample(m); return; }
        if (m.Action < CoopCardAction.PackClaimed || m.Action > CoopCardAction.Ground || m.Revision <= revision) return;
        if (m.Revision != revision + 1) { nextSync = 0; return; }
        if (Apply(m)) revision = m.Revision;
    }

    int HeldCount(ulong actor)
    {
        int n = 0;
        foreach (var id in held) if (states[id].Actor == actor) n++;
        return n;
    }

    bool HandleRequest(ulong sender, CoopCardMessage m)
    {
        states.TryGetValue(m.Card, out var old);
        if (old.Revision == m.Revision && TryChange(sender, old, m)) return true;
        if (sender == local) ClearPending(m.Card);
        else { m.Action = CoopCardAction.Denied; send(sender, m); }
        return false;
    }

    bool TryChange(ulong actor, CoopCardMessage old, CoopCardMessage m)
    {
        m.Actor = actor;
        if (m.Action == CoopCardAction.OpenPack || m.Action == CoopCardAction.CancelPack)
        {
            if (m.Action == CoopCardAction.OpenPack)
            {
                if (!layout.AvailablePack(m.Card) || packOwners.ContainsKey(m.Card)) return false;
                m.Action = CoopCardAction.PackClaimed;
            }
            else
            {
                if (!packOwners.TryGetValue(m.Card, out ulong owner) || owner != actor) return false;
                m.Action = CoopCardAction.PackReleased;
            }
            Commit(m);
            return true;
        }
        if (m.Action == CoopCardAction.Collect)
        {
            if (old.Revision != 0 || !layout.SourcePack(m.Card, out var pack)
                || !packOwners.TryGetValue(pack, out ulong owner) || owner != actor) return false;
            Transform body = playerBody(actor);
            if (!body) return false;
            m.Position = FloorBy(body);
            m.Rotation = Quaternion.identity;
            // Commit the entire fixed pack in one host turn, even if the opener disconnects during delivery.
            foreach (Guid product in layout.Products(pack))
            {
                m.Card = product;
                m.Action = HeldCount(actor) < CardDimensions.MaximumUpgradedHandSize ? CoopCardAction.Held : CoopCardAction.Ground;
                Commit(m);
            }
            return true;
        }
        else if (!layout.Cards.TryGetValue(m.Card, out var card) || !card) return false;
        else if (m.Action == CoopCardAction.Take)
        {
            if (old.Action == CoopCardAction.Held || card.IsFlyingToShelf || card.IsShelfRowCompleteLocked
                || HeldCount(actor) >= CardDimensions.MaximumUpgradedHandSize || !InReach(actor, card.transform.position)) return false;
            m.Position = card.transform.position;
            m.Rotation = card.transform.rotation;
            m.Action = CoopCardAction.Held;
        }
        else
        {
            if (old.Action != CoopCardAction.Held || old.Actor != actor) return false;
            switch (m.Action)
            {
                case CoopCardAction.Shelve:
                    if (!layout.Slots.TryGetValue(m.Slot, out var slot) || !slot.CanPlace(card) || !InReach(actor, slot.Position)) return false;
                    m.Action = CoopCardAction.OnShelf;
                    break;
                case CoopCardAction.Throw:
                    if (!InReach(actor, m.Position) || !(m.Velocity.sqrMagnitude <= 100f) || !UnitRotation(m.Rotation)) return false;
                    m.Action = CoopCardAction.Flight;
                    break;
                case CoopCardAction.Return:
                    Release(m.Card);
                    return true;
                default: return false;
            }
        }
        Commit(m);
        return true;
    }

    static bool UnitRotation(Quaternion q)
    {
        float norm = Quaternion.Dot(q, q);
        return norm > .99f && norm < 1.01f;
    }

    bool InReach(ulong actor, Vector3 position)
    {
        Transform body = playerBody(actor);
        return body && (body.position + Vector3.up - position).sqrMagnitude <= 36f;
    }

    static Vector3 FloorBy(Transform body)
    {
        var origin = body.position + body.forward * .65f + Vector3.up;
        return Physics.Raycast(origin, Vector3.down, out var hit, 6f, Physics.DefaultRaycastLayers & ~CardLayers.WorldCardMask,
            QueryTriggerInteraction.Ignore) ? hit.point + Vector3.up * .01f : body.position + Vector3.up * .01f;
    }

    void Release(Guid id)
    {
        var previous = states[id];
        var body = playerBody(previous.Actor);
        Vector3 position = body ? FloorBy(body) : previous.Position;
        Commit(new CoopCardMessage { Card = id, Action = CoopCardAction.Ground, Position = position, Rotation = Quaternion.identity });
    }

    void Commit(CoopCardMessage m)
    {
        m.Revision = revision + 1;
        // Store before presenting. A full local hand can immediately return a newly granted card.
        revision = m.Revision;
        history.Add(m);
        Apply(m);
    }

    bool Apply(CoopCardMessage m)
    {
        if (m.Action == CoopCardAction.PackClaimed || m.Action == CoopCardAction.PackReleased)
        {
            ClearPending(m.Card);
            states[m.Card] = m;
            if (m.Action == CoopCardAction.PackReleased) packOwners.Remove(m.Card);
            else
            {
                packOwners[m.Card] = m.Actor;
                if (m.Actor == local)
                {
                    var pack = layout.AvailablePack(m.Card);
                    var hand = PlayerCardHand.Instance;
                    if (!hand || !pack || !hand.BeginPackOpening(pack))
                        Request(new CoopCardMessage { Card = m.Card, Action = CoopCardAction.CancelPack });
                }
            }
            return true;
        }
        var card = layout.Resolve(m.Card, m.Position);
        if (!card) return false;
        if (m.Action == CoopCardAction.OnShelf && !layout.Slots.ContainsKey(m.Slot)) return false;
        states.TryGetValue(m.Card, out var previous);
        if (previous.Revision == 0 && layout.SourcePack(m.Card, out var source))
        {
            packOwners.Remove(source);
            layout.ConsumePack(source);
        }
        ClearPending(m.Card);
        states[m.Card] = m;
        if (previous.Action == CoopCardAction.Held)
        {
            held.Remove(m.Card);
            if (previous.Actor == local && PlayerCardHand.Instance) PlayerCardHand.Instance.RemoveFromHand(card);
        }
        if (previous.Action == CoopCardAction.OnShelf && layout.Slots.TryGetValue(previous.Slot, out var previousSlot))
            previousSlot.Remove(card);
        if (m.Action != CoopCardAction.Flight && m.Action != CoopCardAction.Ground) flights.Forget(m.Card);

        switch (m.Action)
        {
            case CoopCardAction.Held:
                held.Add(m.Card);
                if (m.Actor == local)
                {
                    var hand = PlayerCardHand.Instance;
                    if (hand && hand.RestoreHeldCard(card))
                    {
                        hand.SelectRightmostFanEntry();
                        GameSoundEffects.Play(GameSoundEffects.Id.CardPickup);
                    }
                    else Request(new CoopCardMessage { Action = CoopCardAction.Return, Card = m.Card });
                }
                else
                {
                    var anchor = new GameObject("Co-op card hand").transform;
                    Transform body = playerBody(m.Actor);
                    if (body) anchor.SetPositionAndRotation(body.TransformPoint(new Vector3(.35f, 1.1f, .3f)), body.rotation);
                    else anchor.gameObject.SetActive(false);
                    card.RestoreIntoHand(anchor, CardDimensions.GroundCardScale);
                    card.transform.localRotation = Quaternion.Euler(0, 165, -12);
                    card.transform.localScale = Vector3.one * CardDimensions.GroundCardScale;
                    remoteHands.Add(m.Card, anchor);
                    ArrangeRemoteHand(m.Actor);
                }
                break;
            case CoopCardAction.OnShelf:
                layout.Slots[m.Slot].Place(card);
                break;
            case CoopCardAction.Flight:
                flights.Start(card, m);
                if (previous.Action == CoopCardAction.Held) GameSoundEffects.Play(GameSoundEffects.Id.CardThrow);
                break;
            case CoopCardAction.Ground:
                flights.Land(card, m);
                break;
        }
        // Drop/shelve reparents the card first, so destroying the old hand cannot destroy it.
        if (previous.Action == CoopCardAction.Held && previous.Actor != local && remoteHands.TryGetValue(m.Card, out var oldHand))
        {
            UnityEngine.Object.Destroy(oldHand.gameObject);
            remoteHands.Remove(m.Card);
            ArrangeRemoteHand(previous.Actor);
        }
        return true;
    }

    void ArrangeRemoteHand(ulong actor)
    {
        int index = 0;
        foreach (var pair in remoteHands)
            if (states[pair.Key].Actor == actor)
                layout.Cards[pair.Key].transform.localPosition = new Vector3(index++ * .012f, 0, 0);
    }

    bool IsMember(ulong id)
    {
        for (int i = 0; i < members.Count; i++) if (members[i] == id) return true;
        return false;
    }

    void BroadcastMotion(CoopCardMessage m)
    {
        for (int i = 0; i < members.Count; i++) if (members[i] != local) send(members[i], m);
    }

    public void Dispose()
    {
        if (Active == this) Active = null;
        foreach (var id in held)
        {
            if (!layout.Cards.TryGetValue(id, out var card) || !card) continue;
            if (states[id].Actor == local && PlayerCardHand.Instance) PlayerCardHand.Instance.RemoveFromHand(card);
            CoopCardFlights.ShowGround(card, states[id].Position, Quaternion.identity);
        }
        foreach (var anchor in remoteHands.Values) if (anchor) UnityEngine.Object.Destroy(anchor.gameObject);
    }
}
