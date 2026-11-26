using System;
using System.Collections.Generic;

public interface ICoopVoiceSettings
{
    bool VoiceEnabled { get; }
    void SetVoiceEnabled(bool value);
    bool IsMuted(ulong player);
    void ToggleMute(ulong player);
}

public static class CoopVoiceRoute
{
    public static bool Accept(ulong session, ulong packetRoom, ulong local, ulong host, ulong sender, ulong speaker, List<ulong> members)
    {
        return session == packetRoom && speaker != local && sender != local && members.Contains(speaker)
            && (local == host ? sender == speaker : sender == host);
    }
}

// Unity pulls PCM on its audio thread. Buffer brief network jitter and cap queued latency.
public sealed class CoopVoiceBuffer
{
    public const int Rate = 24000;
    readonly float[] samples = new float[Rate / 4];
    int read, count;
    bool started;
    public void Write(byte[] pcm, int bytes)
    {
        lock (samples)
        {
            for (int i = 0; i + 1 < bytes; i += 2)
            {
                if (count == samples.Length) { read = (read + 1) % samples.Length; count--; }
                samples[(read + count++) % samples.Length] = (short)(pcm[i] | (pcm[i+1] << 8)) / 32768f;
            }
        }
    }

    public void Read(float[] output)
    {
        lock (samples)
        {
            if (!started) started = count >= Rate / 25;
            int available = started ? Math.Min(count, output.Length) : 0;
            for (int i = 0; i < available; i++) { output[i] = samples[read]; read = (read + 1) % samples.Length; }
            count -= available;
            if (available < output.Length) { Array.Clear(output, available, output.Length - available); started = false; }
        }
    }
}
