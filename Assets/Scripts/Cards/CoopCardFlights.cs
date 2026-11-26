using System;
using System.Collections.Generic;
using UnityEngine;

// Only the room owner runs the throwing solver. Remote players interpolate samples;
// a reliable transition carries the exact final pose, including edge-on and face-down landings.
internal sealed class CoopCardFlights
{
    const float Interval = 1f / 15f;
    sealed class Flight
    {
        public WorldCard Card;
        public CoopCardMessage State;
        public Vector3 From;
        public Quaternion FromRotation;
        public float SampleTime;
        public uint Sequence;
    }

    readonly bool host;
    readonly Action<CoopCardMessage> sendMotion, commit;
    readonly Dictionary<Guid, Flight> tracked = new Dictionary<Guid, Flight>();
    readonly List<CoopCardMessage> transitions = new List<CoopCardMessage>();
    float nextSample;

    public CoopCardFlights(bool host, Action<CoopCardMessage> sendMotion, Action<CoopCardMessage> commit)
    {
        this.host = host;
        this.sendMotion = sendMotion;
        this.commit = commit;
    }

    public void Forget(Guid id) => tracked.Remove(id);

    public void Start(WorldCard card, CoopCardMessage m)
    {
        if (host)
        {
            if (!card.IsPhysicsSimulating)
            {
                card.transform.SetPositionAndRotation(m.Position, m.Rotation);
                card.DropWithPhysics(m.Velocity);
            }
        }
        else ShowGround(card, m.Position, m.Rotation, true);
        tracked[m.Card] = new Flight { Card = card, State = m, From = card.transform.position,
            FromRotation = card.transform.rotation, SampleTime = Time.unscaledTime };
    }

    public void Land(WorldCard card, CoopCardMessage m)
    {
        if (host && card.IsPhysicsSimulating && tracked.TryGetValue(m.Card, out var f))
        {
            // Keep the real sleeping body. Removing a support or hitting this pile can wake it again.
            f.State = m;
            f.Sequence = 0;
            return;
        }
        bool wasInFlight = tracked.Remove(m.Card);
        ShowGround(card, m.Position, m.Rotation, wasInFlight || m.PhysicalPose != 0);
    }

    public static void ShowGround(WorldCard card, Vector3 position, Quaternion rotation, bool replicatedPhysics = false)
    {
        CardGroundStack.Untrack(card);
        card.SetGroundStackLayer(0);
        card.SetGroundShowsBack(false);
        card.PlaceOnSurface(null, position, rotation, replicatedPhysics);
        card.RegisterForInstancedGround();
        CardGroundStack.TrackPhysicsPose(card);
    }

    public void Sample(CoopCardMessage m)
    {
        if (!tracked.TryGetValue(m.Card, out var f) || m.Revision != f.State.Revision || m.Request <= f.Sequence) return;
        f.Sequence = m.Request;
        f.From = f.Card.transform.position;
        f.FromRotation = f.Card.transform.rotation;
        f.State.Position = m.Position;
        f.State.Rotation = m.Rotation;
        f.SampleTime = Time.unscaledTime;
        // The ordinary interaction ray can target the moving replica as well.
        CardGroundStack.TrackPhysicsPose(f.Card);
    }

    public void Tick()
    {
        if (tracked.Count == 0) return;
        float now = Time.unscaledTime;
        if (host && now < nextSample) return;
        nextSample = now + Interval;
        transitions.Clear();
        foreach (var f in tracked.Values)
        {
            var card = f.Card;
            if (!card) continue;
            if (!host)
            {
                float t = Mathf.Clamp01((now - f.SampleTime) / Interval);
                card.transform.SetPositionAndRotation(Vector3.Lerp(f.From, f.State.Position, t),
                    Quaternion.Slerp(f.FromRotation, f.State.Rotation, t));
                continue;
            }
            var body = card.PhysicsBody;
            bool sleeping = !body || body.IsSleeping();
            var m = f.State;
            if (m.Action == CoopCardAction.Ground && sleeping) continue;
            m.Position = body ? body.position : card.transform.position;
            m.Rotation = body ? body.rotation : card.transform.rotation;
            if (sleeping || m.Action == CoopCardAction.Ground)
            {
                m.Action = sleeping ? CoopCardAction.Ground : CoopCardAction.Flight;
                transitions.Add(m);
            }
            else
            {
                m.Action = CoopCardAction.Motion;
                m.Request = ++f.Sequence;
                sendMotion(m);
            }
        }
        foreach (var m in transitions) commit(m);
    }
}
