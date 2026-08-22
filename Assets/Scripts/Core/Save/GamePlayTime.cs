/// <summary>
/// Accumulated gameplay seconds. Uses scaled delta so paused time is excluded.
/// </summary>
public static class GamePlayTime
{
    static double _accumulatedSeconds;
    static bool _sessionActive;

    public static double TotalSeconds => _accumulatedSeconds;

    public static void BeginSession(double loadedSeconds)
    {
        _accumulatedSeconds = loadedSeconds < 0d ? 0d : loadedSeconds;
        _sessionActive = true;
    }

    public static void EndSession()
    {
        _sessionActive = false;
    }

    public static void Tick(float deltaTime)
    {
        if (!_sessionActive || deltaTime <= 0f)
            return;

        _accumulatedSeconds += deltaTime;
    }

    public static void Reset()
    {
        _accumulatedSeconds = 0d;
        _sessionActive = false;
    }
}
