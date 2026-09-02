namespace ServiceBusConsole;

/// <summary>
/// Rate-limits UI updates while a list streams in, so partial results stay visible
/// without repainting the terminal for every single item.
/// </summary>
public sealed class RenderThrottle(int intervalMs = 150)
{
    private long _nextTick;

    public bool ShouldRender()
    {
        var now = Environment.TickCount64;
        if (now < _nextTick) return false;
        _nextTick = now + intervalMs;
        return true;
    }

    public void Reset() => _nextTick = 0;
}
