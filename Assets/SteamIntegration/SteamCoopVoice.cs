#if TCG_STEAMWORKS_NET && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX)
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using UnityEngine;

internal sealed class SteamCoopVoice : IDisposable
{
    const int Channel = 35, Header = 20, MaxFrame = 16384;
    readonly ulong room, local, host;
    readonly List<ulong> peers;
    readonly byte[] encoded = new byte[MaxFrame], pcm = new byte[49152];
    readonly IntPtr outgoing = Marshal.AllocHGlobal(Header + MaxFrame);
    readonly IntPtr[] received = new IntPtr[32];
    readonly Dictionary<ulong, Speaker> playback = new Dictionary<ulong, Speaker>();
    readonly Dictionary<ulong, uint> latest = new Dictionary<ulong, uint>();
    readonly Func<ulong, bool> muted;
    readonly List<ulong> departed = new List<ulong>();
    uint sequence;
    bool recording, draining;

    sealed class Speaker : IDisposable
    {
        public readonly CoopVoiceBuffer Queue = new CoopVoiceBuffer();
        readonly GameObject root;
        readonly AudioClip clip;
        public Speaker()
        {
            root = new GameObject("Co-op voice");
            var source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0;
            source.ignoreListenerPause = true;
            clip = AudioClip.Create("Room voice", CoopVoiceBuffer.Rate, 1, CoopVoiceBuffer.Rate, true, Queue.Read);
            source.clip = clip; source.Play();
        }
        public void Dispose()
        {
            if (root) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
            if (clip) UnityEngine.Object.Destroy(clip);
        }
    }

    public SteamCoopVoice(ulong room, ulong local, ulong host, List<ulong> peers, Func<ulong,bool> muted)
    { this.room = room; this.local = local; this.host = host; this.peers = peers; this.muted = muted; }

    public void Tick(bool enabled)
    {
        bool talk = enabled && Application.isFocused && !GamePause.IsPaused && Input.GetKey(KeyCode.V);
        if (talk && !recording) { SteamUser.StartVoiceRecording(); recording = true; draining = false; }
        if (!talk && recording) StopMic();
        if (recording || draining)
        {
            var result = SteamUser.GetVoice(true, encoded, MaxFrame, out uint bytes);
            if (result == EVoiceResult.k_EVoiceResultNotRecording) draining = false;
            if (enabled && Application.isFocused && !GamePause.IsPaused && result == EVoiceResult.k_EVoiceResultOK && bytes > 0 && bytes <= MaxFrame)
            {
                Marshal.WriteInt64(outgoing, 0, unchecked((long)room));
                Marshal.WriteInt64(outgoing, 8, unchecked((long)local));
                Marshal.WriteInt32(outgoing, 16, unchecked((int)++sequence));
                Marshal.Copy(encoded, 0, IntPtr.Add(outgoing, Header), (int)bytes);
                Dispatch(local, outgoing, (uint)Header + bytes);
            }
        }
        int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, received, received.Length);
        for (int i = 0; i < count; i++)
        {
            try
            {
                var packet = SteamNetworkingMessage_t.FromIntPtr(received[i]);
                if (packet.m_cbSize <= Header || packet.m_cbSize > Header + MaxFrame) continue;
                IntPtr data = packet.m_pData;
                ulong sender = packet.m_identityPeer.GetSteamID64();
                ulong speaker = unchecked((ulong)Marshal.ReadInt64(data, 8));
                if (!CoopVoiceRoute.Accept(room, unchecked((ulong)Marshal.ReadInt64(data)), local, host, sender, speaker, peers)) continue;
                uint n = unchecked((uint)Marshal.ReadInt32(data, 16));
                if (latest.TryGetValue(speaker, out uint previous) && unchecked((int)(n - previous)) <= 0) continue;
                latest[speaker] = n;
                if (local == host) Dispatch(speaker, data, (uint)packet.m_cbSize);
                if (!enabled || muted(speaker)) continue;
                int length = packet.m_cbSize - Header;
                Marshal.Copy(IntPtr.Add(data, Header), encoded, 0, length);
                if (SteamUser.DecompressVoice(encoded, (uint)length, pcm, (uint)pcm.Length, out uint written, CoopVoiceBuffer.Rate)
                    != EVoiceResult.k_EVoiceResultOK || written == 0) continue;
                if (!playback.TryGetValue(speaker, out var player)) playback.Add(speaker, player = new Speaker());
                player.Queue.Write(pcm, (int)written);
            }
            finally { SteamNetworkingMessage_t.Release(received[i]); }
        }
    }

    void Dispatch(ulong speaker, IntPtr packet, uint length)
    {
        foreach (ulong id in peers)
        {
            if (id == local || id == speaker || (local != host && id != host)) continue;
            SteamNetworkingIdentity peer = default; peer.SetSteamID64(id);
            SteamNetworkingMessages.SendMessageToUser(ref peer, packet, length,
                Constants.k_nSteamNetworkingSend_UnreliableNoDelay | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession, Channel);
        }
    }
    void StopMic() { SteamUser.StopVoiceRecording(); recording = false; draining = true; }
    public void Silence(ulong id)
    {
        if (playback.TryGetValue(id, out var s)) { s.Dispose(); playback.Remove(id); }
    }
    public void SilenceAll()
    {
        if (recording) StopMic();
        foreach (var s in playback.Values) s.Dispose();
        playback.Clear();
    }
    public void UpdateMembers()
    {
        departed.Clear();
        foreach (var id in latest.Keys) if (!peers.Contains(id)) departed.Add(id);
        foreach (var id in departed) { latest.Remove(id); Silence(id); }
    }
    public void Dispose()
    {
        SilenceAll();
        foreach (ulong id in peers)
        {
            if (local == id || (local != host && id != host)) continue;
            SteamNetworkingIdentity peer = default; peer.SetSteamID64(id);
            SteamNetworkingMessages.CloseChannelWithUser(ref peer, Channel);
        }
        Marshal.FreeHGlobal(outgoing);
    }
}
#endif
