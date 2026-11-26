#if TCG_STEAMWORKS_NET && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX)
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using UnityEngine;

// Guests send only their own pose. The host relays accepted poses to the other room members.
internal sealed class SteamCoopPresence : IDisposable
{
    const int Channel = 31;
    const float SendInterval = 0.1f;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct Pose
    {
        public ulong Room, Actor;
        public uint Sequence;
        public float X, Y, Z, Yaw;

        public bool Valid => Mathf.Abs(X) < 10000 && Mathf.Abs(Y) < 10000 && Mathf.Abs(Z) < 10000 &&
            Yaw >= 0 && Yaw <= 360;
    }

    sealed class Remote
    {
        public GameObject Body;
        public Pose Target;
        public TMPro.TextMeshPro Label;
        public Renderer Renderer;
    }

    static readonly int PoseSize = Marshal.SizeOf<Pose>();
    readonly IntPtr buffer = Marshal.AllocHGlobal(PoseSize);
    readonly IntPtr[] received = new IntPtr[32];
    readonly HashSet<ulong> allowed = new HashSet<ulong>();
    readonly Dictionary<ulong, Remote> avatars = new Dictionary<ulong, Remote>();
    readonly List<ulong> removed = new List<ulong>();
    readonly Callback<SteamNetworkingMessagesSessionRequest_t> requested;
    ulong local, host;
    uint sequence;
    float nextSend;
    FirstPersonController player;
    Material avatarMaterial;
    readonly MaterialPropertyBlock tint = new MaterialPropertyBlock();
    readonly List<ulong> roster = new List<ulong>();

    public SteamCoopPresence(SteamRoomAdmission admission, Func<bool> acceptsCode)
    {
        requested = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(e =>
        {
            var peer = e.m_identityRemote;
            if (CanTalk(peer.GetSteamID64()) || admission.CanReceiveRequests || acceptsCode())
                SteamNetworkingMessages.AcceptSessionWithUser(ref peer);
        });
    }

    bool CanTalk(ulong sender) => sender != local && allowed.Contains(sender) && (local == host || sender == host);

    public Transform PlayerBody(ulong id)
    {
        if (id == local) return player ? player.transform : null;
        return avatars.TryGetValue(id, out Remote remote) && remote.Body ? remote.Body.transform : null;
    }

    public void SetMembers(ulong user, ulong owner, List<ulong> peers)
    {
        local = user;
        host = owner;
        removed.Clear();
        foreach (ulong id in allowed)
            if (!peers.Contains(id)) removed.Add(id);
        foreach (ulong id in removed) RemovePeer(id);
        allowed.Clear();
        foreach (ulong id in peers) allowed.Add(id);
        roster.Clear(); roster.AddRange(peers);
        foreach (var pair in avatars) Tint(pair.Key, pair.Value);
    }

    public void Tick(ulong room)
    {
        if (!GameScenes.IsActiveGameScene() || !CardInstancedRenderManager.IsGameplayReady) return;
        if (!player) player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
        if (!player) return;
        float now = Time.unscaledTime;
        if (now >= nextSend)
        {
            nextSend = now + SendInterval;
            Vector3 p = player.transform.position;
            var pose = new Pose { Room = room, Actor = local, Sequence = ++sequence, X = p.x, Y = p.y, Z = p.z, Yaw = player.transform.eulerAngles.y };
            if (local == host) Broadcast(pose);
            else Send(host, pose);
        }

        int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, received, received.Length);
        for (int i = 0; i < count; i++)
        {
            IntPtr ptr = received[i];
            try
            {
                var msg = SteamNetworkingMessage_t.FromIntPtr(ptr);
                ulong sender = msg.m_identityPeer.GetSteamID64();
                if (!CanTalk(sender) || msg.m_cbSize != PoseSize) continue;
                var pose = Marshal.PtrToStructure<Pose>(msg.m_pData);
                if (pose.Room != room || pose.Actor == local || !allowed.Contains(pose.Actor) || !pose.Valid) continue;
                if (local == host && pose.Actor != sender) continue;
                if (!avatars.TryGetValue(pose.Actor, out Remote avatar))
                {
                    avatar = new Remote { Body = MakeAvatar(pose.Actor), Target = pose };
                    avatar.Renderer = avatar.Body.GetComponentInChildren<MeshRenderer>();
                    var labelObject = new GameObject("Player name");
                    labelObject.transform.SetParent(avatar.Body.transform, false);
                    labelObject.transform.localPosition = Vector3.up * (player.GetComponent<CharacterController>().center.y + player.GetComponent<CharacterController>().height * .5f + .18f);
                    avatar.Label = labelObject.AddComponent<TMPro.TextMeshPro>();
                    avatar.Label.richText = false;
                    avatar.Label.fontSize = 2;
                    avatar.Label.alignment = TMPro.TextAlignmentOptions.Center;
                    avatar.Label.rectTransform.sizeDelta = new Vector2(3f, .45f);
                    avatar.Label.text = SteamFriends.GetFriendPersonaName(new CSteamID(pose.Actor));
                    Tint(pose.Actor, avatar);
                    avatars.Add(pose.Actor, avatar);
                    avatar.Body.transform.position = Position(pose);
                }
                else if (unchecked((int)(pose.Sequence - avatar.Target.Sequence)) <= 0) continue;
                avatar.Target = pose;
                if (local == host) Broadcast(pose);
            }
            finally { SteamNetworkingMessage_t.Release(ptr); }
        }
        float blend = 1 - Mathf.Exp(-18f * Time.unscaledDeltaTime);
        foreach (Remote remote in avatars.Values)
        {
            Transform t = remote.Body.transform;
            var camera = Camera.main;
            remote.Label.enabled = camera && (t.position - player.transform.position).sqrMagnitude < 36f;
            if (remote.Label.enabled) remote.Label.transform.rotation = camera.transform.rotation;
            t.SetPositionAndRotation(Vector3.Lerp(t.position, Position(remote.Target), blend),
                Quaternion.Slerp(t.rotation, Quaternion.Euler(0, remote.Target.Yaw, 0), blend));
        }
    }

    static Vector3 Position(Pose pose) => new Vector3(pose.X, pose.Y, pose.Z);

    void Tint(ulong id, Remote remote)
    {
        Color color = CoopPlayerColors.For(id, roster);
        tint.SetColor("_BaseColor", color);
        tint.SetColor("_Color", color);
        remote.Renderer.SetPropertyBlock(tint);
        remote.Label.color = color;
    }

    GameObject MakeAvatar(ulong id)
    {
        var root = new GameObject("Co-op player " + id);
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(root.transform, false);
        var controller = player.GetComponent<CharacterController>();
        float h = controller.height;
        body.transform.localPosition = controller.center;
        body.transform.localScale = new Vector3(controller.radius * 2, h / 2, controller.radius * 2);
        var collider = body.GetComponent<Collider>();
        collider.enabled = false;
        UnityEngine.Object.Destroy(collider);
        if (!avatarMaterial)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            avatarMaterial = shader ? new Material(shader) : new Material(body.GetComponent<Renderer>().sharedMaterial);
            avatarMaterial.color = new Color(0.18f, 0.72f, 0.86f);
        }
        body.GetComponent<Renderer>().sharedMaterial = avatarMaterial;
        return root;
    }

    void Broadcast(Pose pose)
    {
        foreach (ulong peer in allowed)
            if (peer != local && peer != pose.Actor) Send(peer, pose);
    }

    void Send(ulong target, Pose pose)
    {
        SteamNetworkingIdentity peer = default;
        peer.SetSteamID64(target);
        Marshal.StructureToPtr(pose, buffer, false);
        SteamNetworkingMessages.SendMessageToUser(ref peer, buffer, (uint)PoseSize,
            Constants.k_nSteamNetworkingSend_Unreliable | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession, Channel);
    }

    void RemovePeer(ulong id)
    {
        if (CanTalk(id))
        {
            SteamNetworkingIdentity peer = default;
            peer.SetSteamID64(id);
            SteamNetworkingMessages.CloseChannelWithUser(ref peer, Channel);
        }
        if (!avatars.TryGetValue(id, out Remote avatar)) return;
        if (avatar.Body) UnityEngine.Object.Destroy(avatar.Body);
        avatars.Remove(id);
    }

    public void Clear()
    {
        foreach (ulong peer in allowed) RemovePeer(peer);
        allowed.Clear();
        player = null;
        sequence = 0;
        nextSend = 0;
        if (avatarMaterial) UnityEngine.Object.Destroy(avatarMaterial);
        avatarMaterial = null;
    }

    public void Dispose()
    {
        Clear();
        requested.Dispose();
        Marshal.FreeHGlobal(buffer);
    }
}
#endif
