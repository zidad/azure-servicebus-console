namespace ServiceBusConsole.Abstractions;

/// <summary>A time range of equal intervals, e.g. the last 24 hours in 15-minute steps.</summary>
public readonly record struct MetricsWindow(DateTimeOffset From, DateTimeOffset To, TimeSpan Interval)
{
    /// <summary><paramref name="span"/> up to the end of the interval that holds <paramref name="now"/>, so that interval is included.</summary>
    public static MetricsWindow Last(TimeSpan span, TimeSpan interval, DateTimeOffset now)
    {
        var to = new DateTimeOffset(now.UtcTicks - now.UtcTicks % interval.Ticks, TimeSpan.Zero) + interval;
        return new(to - span, to, interval);
    }

    public IEnumerable<DateTimeOffset> IntervalStarts() => Steps(From, Interval, (int)((To - From) / Interval));

    /// <summary>The start of the interval holding <paramref name="time"/>, or null outside the window.</summary>
    public DateTimeOffset? IntervalOf(DateTimeOffset time) =>
        time >= From && time < To ? From + (time - From).Ticks / Interval.Ticks * Interval : null;

    private static IEnumerable<DateTimeOffset> Steps(DateTimeOffset from, TimeSpan interval, int count) =>
        Enumerable.Range(0, count).Select(i => from + i * interval);
}
