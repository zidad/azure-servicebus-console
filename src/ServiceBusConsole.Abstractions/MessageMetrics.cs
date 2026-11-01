namespace ServiceBusConsole.Abstractions;

/// <summary>
/// Throughput (messages in and out per interval) and backlog (most active and dead-lettered messages in each
/// interval) over a <see cref="MetricsWindow"/>, one point per interval.
/// </summary>
public sealed record MessageMetrics
{
    public required MetricsWindow Window { get; init; }
    public required IReadOnlyList<MetricPoint> Incoming { get; init; }
    public required IReadOnlyList<MetricPoint> Outgoing { get; init; }
    public required IReadOnlyList<MetricPoint> Active { get; init; }
    public required IReadOnlyList<MetricPoint> DeadLettered { get; init; }

    /// <summary>From the cache, while the live metrics are still on their way.</summary>
    public bool IsRefreshing { get; init; }

    public bool HasData => Incoming.Count + Outgoing.Count + Active.Count + DeadLettered.Count > 0;

    /// <summary>These metrics moved to <paramref name="window"/>: points outside it are dropped.</summary>
    public MessageMetrics Within(MetricsWindow window) => this with
    {
        Window = window,
        Incoming = Keep(Incoming, window),
        Outgoing = Keep(Outgoing, window),
        Active = Keep(Active, window),
        DeadLettered = Keep(DeadLettered, window),
    };

    private static List<MetricPoint> Keep(IEnumerable<MetricPoint> points, MetricsWindow window) =>
        [.. points.Where(p => window.IntervalOf(p.Time) is not null)];
}
