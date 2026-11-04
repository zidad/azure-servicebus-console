namespace ServiceBusConsole.Abstractions;

/// <summary>Incoming messages per interval of every queue and topic of a namespace over a <see cref="MetricsWindow"/>.</summary>
public sealed record EntityTraffic
{
    public required MetricsWindow Window { get; init; }

    /// <summary>By entity name; entities without any traffic may be missing.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<MetricPoint>> Incoming { get; init; }

    /// <summary>From the cache, while the live traffic is still on its way.</summary>
    public bool IsRefreshing { get; init; }

    /// <summary>The entity's points, matched ignoring case (Azure Monitor lower-cases names); none when unknown.</summary>
    public IReadOnlyList<MetricPoint> Of(string entity) =>
        Incoming.TryGetValue(entity, out var points) ? points
        : Incoming.FirstOrDefault(e => string.Equals(e.Key, entity, StringComparison.OrdinalIgnoreCase)).Value ?? [];
}
