#if TCG_STEAMWORKS_NET && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX)
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Steamworks;
using UnityEngine;

// Password proofs travel directly to the owner on a separate reliable Steam channel.
// Only the host can grant admission. Lobby metadata contains the admitted Steam IDs.
internal sealed class SteamRoomAdmission
{
    const int Channel = 32;
    const byte Request = 1, Granted = 2, Incorrect = 3, Unavailable = 4;
    const int Header = 25, ProofLength = 32;
    const float TicketLifetime = 30f;
    readonly IntPtr[] incoming = new IntPtr[12];
    readonly HashSet<ulong> admitted = new HashSet<ulong>();
    readonly Dictionary<ulong, float> tickets = new Dictionary<ulong, float>();
    readonly Dictionary<ulong, (int count, float until)> failures = new Dictionary<ulong, (int, float)>();
    readonly List<ulong> expired = new List<ulong>();
    byte[] hostKey, requestId;
    ulong hostRoom, requestedRoom, requestedOwner;
    bool accepting;

    public bool CanReceiveRequests => accepting;
    public bool IsAdmitted(ulong user) => admitted.Contains(user);

    public static byte[] PasswordKey(string password)
    {
        if (string.IsNullOrEmpty(password)) return null;
        byte[] bytes = Encoding.UTF8.GetBytes(password);
        try { using var hash = SHA256.Create(); return hash.ComputeHash(bytes); }
        finally { Array.Clear(bytes, 0, bytes.Length); }
    }

    public void Open(ulong room, ulong owner, byte[] key)
    {
        hostRoom = room;
        hostKey = key;
        accepting = true;
        admitted.Add(owner);
    }

    public void CloseAdmission()
    {
        accepting = false;
        tickets.Clear();
    }

    public void Forget(ulong member)
    {
        admitted.Remove(member);
        tickets.Remove(member);
    }

    public void AdmitMembers(List<ulong> members)
    {
        admitted.RemoveWhere(id => !members.Contains(id));
        foreach (ulong id in members)
            if (tickets.TryGetValue(id, out float until) && until > Time.realtimeSinceStartup)
            {
                admitted.Add(id);
                tickets.Remove(id);
            }
    }

    public void Reset()
    {
        if (hostKey != null) Array.Clear(hostKey, 0, hostKey.Length);
        hostKey = null;
        hostRoom = 0;
        admitted.Clear();
        CloseAdmission();
        failures.Clear();
        CancelRequest();
    }

    public void CancelRequest()
    {
        requestedRoom = requestedOwner = 0;
        requestId = null;
    }

    static byte[] Proof(byte[] key, byte[] header, ulong user, ulong owner)
    {
        byte[] challenge = new byte[Header + 16];
        Buffer.BlockCopy(header, 0, challenge, 0, Header);
        Buffer.BlockCopy(BitConverter.GetBytes(user), 0, challenge, Header, 8);
        Buffer.BlockCopy(BitConverter.GetBytes(owner), 0, challenge, Header + 8, 8);
        using var mac = new HMACSHA256(key);
        return mac.ComputeHash(challenge);
    }

    public bool Submit(ulong room, ulong user, ulong owner, string password)
    {
        requestedRoom = room;
        requestedOwner = owner;
        requestId = Guid.NewGuid().ToByteArray();
        byte[] packet = new byte[Header + ProofLength];
        packet[0] = Request;
        Buffer.BlockCopy(BitConverter.GetBytes(room), 0, packet, 1, 8);
        Buffer.BlockCopy(requestId, 0, packet, 9, 16);
        var key = PasswordKey(password);
        if (key == null) return false;
        try { Buffer.BlockCopy(Proof(key, packet, user, owner), 0, packet, Header, ProofLength); }
        finally { Array.Clear(key, 0, key.Length); }
        return Send(owner, packet);
    }

    public void Poll(ulong user, Action<bool, bool> onReply)
    {
        int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, incoming, incoming.Length);
        for (int i = 0; i < count; i++)
        {
            IntPtr ptr = incoming[i];
            try
            {
                var message = SteamNetworkingMessage_t.FromIntPtr(ptr);
                if (message.m_cbSize != Header && message.m_cbSize != Header + ProofLength) continue;
                var bytes = new byte[message.m_cbSize];
                Marshal.Copy(message.m_pData, bytes, 0, bytes.Length);
                ulong sender = message.m_identityPeer.GetSteamID64();
                ulong room = BitConverter.ToUInt64(bytes, 1);
                if (bytes.Length == Header + ProofLength && bytes[0] == Request && accepting && room == hostRoom)
                    Answer(bytes, sender, user);
                else if (bytes.Length == Header && sender == requestedOwner && room == requestedRoom && requestId != null
                    && bytes[0] >= Granted && bytes[0] <= Unavailable
                    && new ReadOnlySpan<byte>(bytes, 9, 16).SequenceEqual(requestId))
                {
                    CancelRequest();
                    onReply(bytes[0] == Granted, bytes[0] == Incorrect);
                }
            }
            finally { SteamNetworkingMessage_t.Release(ptr); }
        }
    }

    void Answer(byte[] packet, ulong sender, ulong owner)
    {
        float now = Time.realtimeSinceStartup;
        expired.Clear();
        foreach (var item in failures) if (item.Value.until <= now) expired.Add(item.Key);
        foreach (ulong id in expired) failures.Remove(id);
        expired.Clear();
        foreach (var item in tickets) if (item.Value <= now) expired.Add(item.Key);
        foreach (ulong id in expired) tickets.Remove(id);

        failures.TryGetValue(sender, out var attempts);
        byte result = Unavailable;
        if (attempts.count < 5 && (attempts.count > 0 || failures.Count < 128))
        {
            var expected = Proof(hostKey, packet, sender, owner);
            if (!CryptographicOperations.FixedTimeEquals(expected, new ReadOnlySpan<byte>(packet, Header, ProofLength)))
            {
                failures[sender] = (attempts.count + 1, attempts.count == 0 ? now + 30f : attempts.until);
                result = Incorrect;
            }
            else if (admitted.Contains(sender) || tickets.ContainsKey(sender) || admitted.Count + tickets.Count < CoopSession.Capacity)
            {
                failures.Remove(sender);
                if (!admitted.Contains(sender)) tickets[sender] = now + TicketLifetime;
                result = Granted;
            }
        }
        var reply = new byte[Header];
        Buffer.BlockCopy(packet, 0, reply, 0, Header);
        reply[0] = result;
        Send(sender, reply);
    }

    static bool Send(ulong target, byte[] packet)
    {
        SteamNetworkingIdentity peer = default;
        peer.SetSteamID64(target);
        var pin = GCHandle.Alloc(packet, GCHandleType.Pinned);
        try
        {
            return SteamNetworkingMessages.SendMessageToUser(ref peer, pin.AddrOfPinnedObject(), (uint)packet.Length,
                Constants.k_nSteamNetworkingSend_Reliable | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession, Channel) == EResult.k_EResultOK;
        }
        finally { pin.Free(); }
    }
}
#endif
