#if TCG_STEAMWORKS_NET && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX)
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Steamworks;

// A private Steam lobby needs a native invitation. The shared code identifies its
// owner and authorizes that owner to issue an invitation for this room only.
internal sealed class SteamRoomCodes
{
    const int Channel = 36;
    const int PacketSize = 17;
    readonly IntPtr[] incoming = new IntPtr[8];
    ulong hostSecret, pendingSecret, pendingOwner;

    public string Open(ulong owner)
    {
        byte[] bytes = new byte[8];
        using var random = RandomNumberGenerator.Create();
        do
        {
            random.GetBytes(bytes);
            hostSecret = BitConverter.ToUInt64(bytes, 0);
        } while (hostSecret == 0);
        string secret = hostSecret.ToString("X16", CultureInfo.InvariantCulture);
        return ((uint)owner).ToString("X8", CultureInfo.InvariantCulture) + "-" +
            secret.Substring(0, 8) + "-" + secret.Substring(8);
    }

    public bool Request(ulong local, string code)
    {
        string value = code?.Trim().Replace("-", string.Empty);
        if (value == null || value.Length != 24 ||
            !uint.TryParse(value.Substring(0, 8), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint account) ||
            !ulong.TryParse(value.Substring(8), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong secret) ||
            account == 0 || secret == 0) return false;
        ulong owner = (local & 0xffffffff00000000UL) | account;
        if (owner == local || !Send(owner, 1, secret, 0)) return false;
        pendingOwner = owner;
        pendingSecret = secret;
        return true;
    }

    public void Poll(Func<ulong, ulong> invite, Action<ulong> join)
    {
        int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, incoming, incoming.Length);
        for (int i = 0; i < count; i++)
        {
            IntPtr ptr = incoming[i];
            try
            {
                var msg = SteamNetworkingMessage_t.FromIntPtr(ptr);
                if (msg.m_cbSize != PacketSize) continue;
                IntPtr data = msg.m_pData;
                byte kind = Marshal.ReadByte(data);
                ulong secret = unchecked((ulong)Marshal.ReadInt64(data, 1));
                ulong sender = msg.m_identityPeer.GetSteamID64();
                if (kind == 1 && hostSecret != 0 && secret == hostSecret)
                    Send(sender, 2, secret, invite(sender));
                else if (kind == 2 && pendingOwner != 0 && sender == pendingOwner && secret == pendingSecret)
                {
                    Cancel();
                    join(unchecked((ulong)Marshal.ReadInt64(data, 9)));
                }
            }
            finally { SteamNetworkingMessage_t.Release(ptr); }
        }
    }

    static bool Send(ulong target, byte kind, ulong secret, ulong room)
    {
        SteamNetworkingIdentity peer = default;
        peer.SetSteamID64(target);
        IntPtr ptr = Marshal.AllocHGlobal(PacketSize);
        try
        {
            Marshal.WriteByte(ptr, kind);
            Marshal.WriteInt64(ptr, 1, unchecked((long)secret));
            Marshal.WriteInt64(ptr, 9, unchecked((long)room));
            return SteamNetworkingMessages.SendMessageToUser(ref peer, ptr, PacketSize,
                Constants.k_nSteamNetworkingSend_Reliable | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession, Channel) == EResult.k_EResultOK;
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    public void Cancel() { pendingOwner = pendingSecret = 0; }
    public void Reset() { hostSecret = 0; Cancel(); }
}
#endif
