using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class CoopShopCards
{
    // Stable identities for the four authored keys, independent of scene load order.
    static readonly Guid[] KeyIds = {
        new Guid("a83f1365-2895-455c-af51-8c866ab75900"), new Guid("a83f1365-2895-455c-af51-8c866ab75901"),
        new Guid("a83f1365-2895-455c-af51-8c866ab75902"), new Guid("a83f1365-2895-455c-af51-8c866ab75903")
    };
    readonly Dictionary<Guid, WorldMinorSkillKey> keys = new Dictionary<Guid, WorldMinorSkillKey>();
    readonly WorldMinorSkillChest[] chests = new WorldMinorSkillChest[MinorSkillProgress.Count];

    void FindRoomKeys()
    {
        foreach (var key in UnityEngine.Object.FindObjectsByType<WorldMinorSkillKey>())
            if (MinorSkillProgress.IsValidIndex(key.SkillIndex)) keys[KeyIds[key.SkillIndex]] = key;
        foreach (var chest in UnityEngine.Object.FindObjectsByType<WorldMinorSkillChest>())
            if (MinorSkillProgress.IsValidIndex(chest.SkillIndex)) chests[chest.SkillIndex] = chest;
    }

    static bool IsKeyRequest(CoopCardAction action) => action >= CoopCardAction.TakeKey && action <= CoopCardAction.UseKey;
    static bool IsKeyState(CoopCardAction action) => action >= CoopCardAction.KeyHeld && action <= CoopCardAction.KeyUsed;

    public bool RequestKeyPickup(WorldMinorSkillKey key) => RequestKey(key.SkillIndex, CoopCardAction.TakeKey);
    public bool RequestKeyDrop(WorldMinorSkillKey key) => RequestKey(key.SkillIndex, CoopCardAction.DropKey);
    public bool RequestKeyUse(WorldMinorSkillChest chest) => RequestKey(chest.SkillIndex, CoopCardAction.UseKey);

    bool RequestKey(int index, CoopCardAction action)
    {
        if (!MinorSkillProgress.IsValidIndex(index)) return false;
        var id = KeyIds[index];
        if (!keys.ContainsKey(id)) return false;
        states.TryGetValue(id, out var state);
        bool canTake = state.Revision == 0 || state.Action == CoopCardAction.KeyGround;
        bool owned = state.Action == CoopCardAction.KeyHeld && state.Actor == local;
        if (action == CoopCardAction.TakeKey ? !canTake : !owned) return false;
        return Request(new CoopCardMessage { Card = id, Action = action });
    }

    bool ChangeKey(CoopCardMessage old, CoopCardMessage request)
    {
        if (!keys.TryGetValue(request.Card, out var key) || !key) return false;
        if (request.Action == CoopCardAction.TakeKey)
        {
            if ((old.Revision != 0 && old.Action != CoopCardAction.KeyGround) || !InReach(request.Actor, key.transform.position)) return false;
            request.Action = CoopCardAction.KeyHeld;
            request.Position = key.transform.position;
            request.Rotation = key.transform.rotation;
        }
        else
        {
            if (old.Action != CoopCardAction.KeyHeld || old.Actor != request.Actor) return false;
            if (request.Action == CoopCardAction.DropKey)
            {
                PutKeyDown(old);
                return true;
            }
            var chest = chests[key.SkillIndex];
            if (!chest || !InReach(request.Actor, chest.transform.position)) return false;
            request.Action = CoopCardAction.KeyUsed;
            request.Rotation = Quaternion.identity;
        }
        Commit(request);
        return true;
    }

    void PutKeyDown(CoopCardMessage state)
    {
        var body = playerBody(state.Actor);
        if (body) state.Position = FloorBy(body);
        state.Action = CoopCardAction.KeyGround;
        state.Actor = 0;
        state.Rotation = keys[state.Card].AuthoredRotation;
        Commit(state);
    }

    void ReleaseDepartedKeys()
    {
        foreach (var id in keys.Keys)
            if (states.TryGetValue(id, out var state) && state.Action == CoopCardAction.KeyHeld && !IsMember(state.Actor))
                PutKeyDown(state);
    }

    bool ApplyKey(CoopCardMessage m, bool restoring)
    {
        if (!keys.TryGetValue(m.Card, out var key) || !key) return false;
        ClearPending(m.Card);
        states[m.Card] = m;
        bool heldKey = m.Action == CoopCardAction.KeyHeld;
        key.SetRoomPose(heldKey && m.Actor != local, m.Action == CoopCardAction.KeyGround, m.Position, m.Rotation);
        MinorSkillProgress.ApplyRoomKey(key.SkillIndex, heldKey && m.Actor == local, m.Action == CoopCardAction.KeyUsed,
            !restoring && m.Actor == local);
        return true;
    }
}
