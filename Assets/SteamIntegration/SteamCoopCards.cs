#if TCG_STEAMWORKS_NET && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX)
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using UnityEngine;

internal sealed class SteamCoopCards : IDisposable
{
    const int Channel = 33;
    const int MotionChannel = 34;
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct Packet { public ulong Room; public CoopCardMessage Message; }

    static readonly int PacketSize = Marshal.SizeOf<Packet>();
    readonly IntPtr outgoing = Marshal.AllocHGlobal(PacketSize);
    readonly IntPtr[] incoming = new IntPtr[24];
    readonly ulong room, local, host;
    readonly List<ulong> peers;
    readonly CoopShopCards card;
    float lastHostPacket = Time.realtimeSinceStartup;
    public bool ConnectionTimedOut => local != host && Time.realtimeSinceStartup - lastHostPacket > 30f;

    public SteamCoopCards(ulong room, ulong local, ulong host, List<ulong> peers, Func<ulong, Transform> playerBody)
    {
        this.room = room;
        this.local = local;
        this.host = host;
        this.peers = peers;
        card = new CoopShopCards(local, host, peers, Send, playerBody);
    }

    public void Tick()
    {
        card.Tick();
        Receive(Channel);
        if (local != host) Receive(MotionChannel);
    }

    void Receive(int channel)
    {
        int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(channel, incoming, incoming.Length);
        for (int i = 0; i < count; i++)
        {
            IntPtr ptr = incoming[i];
            try
            {
                var message = SteamNetworkingMessage_t.FromIntPtr(ptr);
                ulong sender = message.m_identityPeer.GetSteamID64();
                if (message.m_cbSize != PacketSize || sender == local || (local != host && sender != host) || !peers.Contains(sender)) continue;
                var packet = Marshal.PtrToStructure<Packet>(message.m_pData);
                if (packet.Room == room && (packet.Message.Action == CoopCardAction.Motion) == (channel == MotionChannel))
                {
                    if (sender == host) lastHostPacket = Time.realtimeSinceStartup;
                    card.Receive(sender, packet.Message);
                }
            }
            finally { SteamNetworkingMessage_t.Release(ptr); }
        }
    }

    void Send(ulong target, CoopCardMessage message)
    {
        SteamNetworkingIdentity peer = default;
        peer.SetSteamID64(target);
        Marshal.StructureToPtr(new Packet { Room = room, Message = message }, outgoing, false);
        bool motion = message.Action == CoopCardAction.Motion;
        int flags = motion ? Constants.k_nSteamNetworkingSend_Unreliable : Constants.k_nSteamNetworkingSend_Reliable;
        SteamNetworkingMessages.SendMessageToUser(ref peer, outgoing, (uint)PacketSize,
            flags | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession, motion ? MotionChannel : Channel);
    }

    public void Dispose()
    {
        card.Dispose();
        foreach (ulong id in peers)
            if (id != local && (local == host || id == host))
            {
                SteamNetworkingIdentity peer = default;
                peer.SetSteamID64(id);
                SteamNetworkingMessages.CloseChannelWithUser(ref peer, Channel);
                SteamNetworkingMessages.CloseChannelWithUser(ref peer, MotionChannel);
            }
        Marshal.FreeHGlobal(outgoing);
    }
}
#endif
