using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public enum CoopCardAction : byte { Sync, Take, Place, Return, State }

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CoopCardMessage
{
    public CoopCardAction Action;
    public uint Revision, Request;
    public ulong Holder;
    public float X, Y, Z, Yaw;

    public Vector3 Position => new Vector3(X, Y, Z);
    public bool HasValidPose => Mathf.Abs(X) < 10000 && Mathf.Abs(Y) < 10000 && Mathf.Abs(Z) < 10000 && Yaw >= 0 && Yaw < 360;
}

// One existing Stormreach Cliffs 4 instance in MainScene. Its serialized save ID
// identifies the same physical card on every machine, including duplicate prints.
public sealed class CoopTableCard : IDisposable
{
    const string CardId = "fdc2d8ec316e4d94bbe9b1456e942322";
    const string CounterPath = "KartTutucu9 (2)/Counter_attlsv";
    const float AimDistance = 4.5f;
    readonly ulong local, host;
    readonly IReadOnlyList<ulong> members;
    readonly Action<ulong, CoopCardMessage> send;
    readonly Func<ulong, Transform> playerBody;
    WorldCard card;
    Transform remoteHand;
    CoopCardMessage state, pending;
    uint nextRequest;
    float nextSync;

    public static CoopTableCard Active { get; private set; }
    public static bool ReservingSlot => Active != null && Active.pending.Action == CoopCardAction.Take;
    public static bool Controls(WorldCard target) => target && Active != null && Active.card == target;

    public CoopTableCard(ulong local, ulong host, IReadOnlyList<ulong> members,
        Action<ulong, CoopCardMessage> send, Func<ulong, Transform> playerBody)
    {
        this.local = local;
        this.host = host;
        this.members = members;
        this.send = send;
        this.playerBody = playerBody;
        Active = this;
    }

    public void Tick()
    {
        if (!card)
        {
            var identity = PersistentIdRegistry.FindRegistered(CardId);
            if (!PlayerCardHand.Instance || !identity || !identity.TryGetComponent(out card)) return;
            if (card.IsInHand) PlayerCardHand.Instance.RemoveFromHand(card);
            Vector3 start = card.transform.position;
            var counter = GameObject.Find(CounterPath);
            if (counter && counter.TryGetComponent(out Collider top))
            {
                Bounds b = top.bounds;
                Vector3 above = b.center + new Vector3(b.extents.x * .52f, b.extents.y + .35f, -b.extents.z * .66f);
                if (top.Raycast(new Ray(above, Vector3.down), out var hit, b.size.y + .7f))
                    start = RestPoint(hit);
            }
            state = new CoopCardMessage { Action = CoopCardAction.State, Revision = local == host ? 1u : 0u,
                X = start.x, Y = start.y, Z = start.z };
            ShowOnSurface();
        }

        if (local == host && state.Holder != 0 && !IsMember(state.Holder))
            Commit(0, state.Position, state.Yaw);
        if (local != host && Time.unscaledTime >= nextSync)
        {
            nextSync = Time.unscaledTime + (pending.Request != 0 ? 1f : 3f);
            send(host, pending.Request != 0 ? pending : new CoopCardMessage { Action = CoopCardAction.Sync });
        }
        if (state.Holder != 0 && state.Holder != local && remoteHand)
        {
            Transform body = playerBody(state.Holder);
            remoteHand.gameObject.SetActive(body != null);
            if (body) remoteHand.SetPositionAndRotation(body.TransformPoint(new Vector3(.35f, 1.1f, .3f)), body.rotation);
        }
    }

    public bool RequestPickup()
    {
        if (state.Revision == 0 || state.Holder != 0 || pending.Request != 0) return false;
        return Request(CoopCardAction.Take, state.Position, state.Yaw);
    }

    public bool RequestPlacement(Camera camera)
    {
        if (!camera || state.Holder != local || pending.Request != 0) return false;
        var ray = camera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
        if (!SurfaceHit(ray, AimDistance, out var hit)) return false;
        return Request(CoopCardAction.Place, RestPoint(hit), camera.transform.eulerAngles.y);
    }

    bool Request(CoopCardAction action, Vector3 at, float yaw)
    {
        pending = new CoopCardMessage { Action = action, Revision = state.Revision, Request = ++nextRequest,
            X = at.x, Y = at.y, Z = at.z, Yaw = yaw };
        if (local == host)
        {
            uint previous = state.Revision;
            Receive(local, pending);
            return previous != state.Revision;
        }
        nextSync = Time.unscaledTime + 1f;
        send(host, pending);
        return true;
    }

    public void Receive(ulong sender, CoopCardMessage message)
    {
        if (!card || !IsMember(sender)) return;
        if (local != host)
        {
            if (sender != host || message.Action != CoopCardAction.State || !message.HasValidPose || message.Revision == 0) return;
            if (message.Revision > state.Revision)
            {
                pending = default;
                Apply(message);
            }
            else if (message.Revision == state.Revision && message.Request == pending.Request) pending = default;
            return;
        }

        if (message.Action != CoopCardAction.Sync && message.Request != 0 && message.Revision == state.Revision)
        {
            if (message.Action == CoopCardAction.Take && state.Holder == 0 && InReach(sender, state.Position))
            {
                Commit(sender, state.Position, state.Yaw);
                return;
            }
            if (state.Holder == sender)
            {
                if (message.Action == CoopCardAction.Return) { Commit(0, state.Position, state.Yaw); return; }
                if (message.Action == CoopCardAction.Place && message.HasValidPose && InReach(sender, message.Position)
                    && SurfaceHit(new Ray(message.Position + Vector3.up * .12f, Vector3.down), .24f, out var hit))
                {
                    Commit(0, RestPoint(hit), message.Yaw);
                    return;
                }
            }
        }
        if (sender == local) pending = default;
        else { var reply = state; reply.Request = message.Request; send(sender, reply); }
    }

    bool IsMember(ulong id)
    {
        for (int i = 0; i < members.Count; i++) if (members[i] == id) return true;
        return false;
    }

    bool InReach(ulong actor, Vector3 point)
    {
        Transform body = playerBody(actor);
        // Player poses arrive at 10 Hz. Allow a little movement between aiming and receipt.
        return body && (body.position + Vector3.up - point).sqrMagnitude <= 6f * 6f;
    }

    static bool SurfaceHit(Ray ray, float distance, out RaycastHit hit)
    {
        hit = default;
        float nearest = float.PositiveInfinity;
        // A PSA slot has a solid aim target above its counter. It is used by E,
        // but shouldn't keep the card from resting on the actual tabletop.
        foreach (var candidate in Physics.RaycastAll(ray, distance, Physics.DefaultRaycastLayers & ~CardLayers.WorldCardMask,
                     QueryTriggerInteraction.Ignore))
        {
            if (candidate.distance >= nearest || candidate.collider.GetComponentInParent<FirstPersonController>()) continue;
            var slot = candidate.collider.GetComponentInParent<PsaCabinetSlot>();
            if (slot && slot.IsAimCollider(candidate.collider)) continue;
            nearest = candidate.distance;
            hit = candidate;
        }
        return hit.collider && hit.normal.y >= .95f;
    }

    static Vector3 RestPoint(RaycastHit hit) => hit.point + Vector3.up * (.002f + CardDimensions.Thickness * CardDimensions.GroundCardScale * .5f);

    void Commit(ulong holder, Vector3 at, float yaw)
    {
        pending = default;
        var update = new CoopCardMessage { Action = CoopCardAction.State, Revision = state.Revision + 1,
            Holder = holder, X = at.x, Y = at.y, Z = at.z, Yaw = yaw };
        Apply(update);
        for (int i = 0; i < members.Count; i++) if (members[i] != local) send(members[i], state);
    }

    void Apply(CoopCardMessage update)
    {
        ulong previous = state.Holder;
        state = update;
        state.Request = 0;
        if (previous == local && update.Holder != local) PlayerCardHand.Instance?.RemoveFromHand(card);
        if (update.Holder == 0)
        {
            ShowOnSurface();
            if (previous == local) GameSoundEffects.Play(GameSoundEffects.Id.CardThrow);
        }
        else if (update.Holder == local)
        {
            if (PlayerCardHand.Instance && PlayerCardHand.Instance.RestoreHeldCard(card))
            {
                PlayerCardHand.Instance.SelectRightmostFanEntry();
                GameSoundEffects.Play(GameSoundEffects.Id.CardPickup);
            }
            else Request(CoopCardAction.Return, state.Position, state.Yaw);
        }
        else
        {
            if (!remoteHand) remoteHand = new GameObject("Co-op card hand").transform;
            // Keep the card alive when a disconnected peer's avatar is destroyed.
            remoteHand.gameObject.SetActive(false);
            card.RestoreIntoHand(remoteHand, CardDimensions.GroundCardScale);
            card.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0, 165, -12));
            card.transform.localScale = Vector3.one * CardDimensions.GroundCardScale;
        }
    }

    void ShowOnSurface()
    {
        CardGroundStack.Untrack(card);
        card.SetGroundStackLayer(0);
        card.SetGroundShowsBack(false);
        card.PlaceOnSurface(null, state.Position, Quaternion.Euler(0, state.Yaw, 0));
        card.RegisterForInstancedGround();
        CardGroundStack.TrackPhysicsPose(card);
    }

    public void Dispose()
    {
        if (Active == this) Active = null;
        if (card && state.Holder != 0)
        {
            if (state.Holder == local) PlayerCardHand.Instance?.RemoveFromHand(card);
            ShowOnSurface();
        }
        if (remoteHand) UnityEngine.Object.Destroy(remoteHand.gameObject);
    }
}
