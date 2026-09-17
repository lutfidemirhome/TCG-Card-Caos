using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Profiling;
#endif

/// <summary>Development-only frame summaries; no per-frame log or scene scan.</summary>
public static class GameplayPerformance
{
    public enum Area { Interaction, Hand, CardDraw, Throw, Dog }
    public struct Sample
    {
        internal long Ticks;
        internal long AllocatedBytes;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    static readonly double[] WorkMs = new double[5];
    static readonly double[] MaxWorkMs = new double[5];
    static readonly long[] AllocatedBytes = new long[5];
    static ProfilerRecorder _drawCalls;
    static ProfilerRecorder _gcBytes;
    static ProfilerRecorder _mainThread;
    static ProfilerRecorder _renderThread;
    static bool _started;
    static int _frames, _slowFrames, _gcStart;
    static double _elapsed, _maxMs, _drawSum, _gcSum;
    static double _mainMaxMs, _renderMaxMs;
    static int _skipFrames;
    static string _pauseTransition;
    static int _pauseTransitionFrame;
    static double _pauseTransitionMs, _pauseTransitionKB;
#endif

    public static Sample BeginSample()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return new Sample
        {
            Ticks = System.Diagnostics.Stopwatch.GetTimestamp(),
            AllocatedBytes = System.GC.GetAllocatedBytesForCurrentThread(),
        };
#else
        return default;
#endif
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void EndSample(Area area, Sample startedAt)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - startedAt.Ticks)
            * 1000d / System.Diagnostics.Stopwatch.Frequency;
        WorkMs[(int)area] += ms;
        MaxWorkMs[(int)area] = System.Math.Max(MaxWorkMs[(int)area], ms);
        AllocatedBytes[(int)area] += System.GC.GetAllocatedBytesForCurrentThread() - startedAt.AllocatedBytes;
#endif
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void EndPauseTransition(string action, Sample startedAt)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _pauseTransition = action;
        _pauseTransitionFrame = Time.frameCount;
        _pauseTransitionMs = (System.Diagnostics.Stopwatch.GetTimestamp() - startedAt.Ticks)
            * 1000d / System.Diagnostics.Stopwatch.Frequency;
        _pauseTransitionKB = (System.GC.GetAllocatedBytesForCurrentThread() - startedAt.AllocatedBytes) / 1024d;
#endif
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Tick()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Report separately, before paused frames are excluded from gameplay averages.
        // The next frame's delta includes the transition frame's UI/render work too;
        // it is whole-frame time, not time attributed solely to the pause menu.
        if (_pauseTransition != null && Time.frameCount > _pauseTransitionFrame)
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}",
                $"[Performance] Pause={_pauseTransition} cpu={_pauseTransitionMs:F2}ms "
                + $"scriptAlloc={_pauseTransitionKB:F1}KB transitionFrame={Time.unscaledDeltaTime * 1000f:F1}ms");
            _pauseTransition = null;
        }
        if (GamePause.IsPaused || GameSceneLoader.IsLoading || !CardInstancedRenderManager.IsGameplayReady
            || !Application.isFocused)
        {
            ClearWindow();
            _skipFrames = 2;
            return;
        }
        if (!_started)
        {
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
            _gcBytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            _mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            _renderThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1);
            _started = true;
            ClearWindow();
            return;
        }
        // Exclude the resume frame (and its previous-frame profiler counters).
        if (_skipFrames > 0)
        {
            _skipFrames--;
            ClearWindow();
            return;
        }
        double delta = Time.unscaledDeltaTime;
        _elapsed += delta;
        _frames++;
        _maxMs = System.Math.Max(_maxMs, delta * 1000d);
        if (delta > 1d / 30d)
            _slowFrames++;
        if (_drawCalls.Valid)
            _drawSum += _drawCalls.LastValue;
        if (_gcBytes.Valid)
            _gcSum += _gcBytes.LastValue;
        if (_mainThread.Valid)
            _mainMaxMs = System.Math.Max(_mainMaxMs, _mainThread.LastValue / 1000000d);
        if (_renderThread.Valid)
            _renderMaxMs = System.Math.Max(_renderMaxMs, _renderThread.LastValue / 1000000d);
        if (_elapsed < 10d)
            return;

        string draws = _drawCalls.Valid ? (_drawSum / _frames).ToString("F0") : "n/a";
        string allocated = _gcBytes.Valid ? (_gcSum / _frames / 1024d).ToString("F1") : "n/a";
        string main = _mainThread.Valid ? _mainMaxMs.ToString("F1") : "n/a";
        string render = _renderThread.Valid ? _renderMaxMs.ToString("F1") : "n/a";
        // Avoid collecting a stack trace for a routine diagnostic summary.
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}",
            $"[Performance] FPS={_frames / _elapsed:F1} avg={_elapsed * 1000d / _frames:F1}ms "
            + $"worst={_maxMs:F1}ms over33ms={_slowFrames}/{_frames} "
            + $"interaction={WorkMs[0] / _frames:F2}ms hand={WorkMs[1] / _frames:F2}ms "
            + $"cardDrawCPU={WorkMs[2] / _frames:F2}ms draws={draws} "
            + $"peakInteraction={MaxWorkMs[0]:F2}ms peakHand={MaxWorkMs[1]:F2}ms "
            + $"peakCardDrawCPU={MaxWorkMs[2]:F2}ms mainMax={main}ms renderMax={render}ms "
            + $"throwCPU={WorkMs[3] / _frames:F2}ms peakThrow={MaxWorkMs[3]:F2}ms "
            + $"scriptAllocKB(I/H/D/T)={AllocatedBytes[0] / (1024d * _frames):F1}/"
            + $"{AllocatedBytes[1] / (1024d * _frames):F1}/{AllocatedBytes[2] / (1024d * _frames):F1}/"
            + $"{AllocatedBytes[3] / (1024d * _frames):F1} "
            // Dog samples cover behaviour/alignment scripts; Unity's native animation,
            // skinning and GPU rendering run outside these measured sections.
            + $"dogCPU={WorkMs[4] / _frames:F2}ms peakDog={MaxWorkMs[4]:F2}ms "
            + $"scriptAllocDogKB={AllocatedBytes[4] / (1024d * _frames):F1} "
            + $"GC/frame={allocated}KB collections={System.GC.CollectionCount(0) - _gcStart}");
        ClearWindow();
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _drawCalls.Dispose();
        _gcBytes.Dispose();
        _mainThread.Dispose();
        _renderThread.Dispose();
        _started = false;
        _pauseTransition = null;
        _skipFrames = 2;
        ClearWindow();
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    static void ClearWindow()
    {
        _frames = _slowFrames = 0;
        _elapsed = _maxMs = _drawSum = _gcSum = 0d;
        _mainMaxMs = _renderMaxMs = 0d;
        _gcStart = System.GC.CollectionCount(0);
        System.Array.Clear(WorkMs, 0, WorkMs.Length);
        System.Array.Clear(MaxWorkMs, 0, MaxWorkMs.Length);
        System.Array.Clear(AllocatedBytes, 0, AllocatedBytes.Length);
    }
#endif
}
