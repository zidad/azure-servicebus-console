namespace ServiceBusConsole.Abstractions;

/// <summary>Azure Monitor metrics of a namespace.</summary>
public interface IMetricsService
{
    /// <summary>Throughput and backlog of <paramref name="scope"/> over <paramref name="window"/>, intervals without data as 0.</summary>
    IAsyncEnumerable<MessageMetrics> GetMetricsAsync(MetricsScope scope, MetricsWindow window, CancellationToken ct = default);

    /// <summary>Incoming messages of every queue and topic over <paramref name="window"/>, in one request.</summary>
    IAsyncEnumerable<EntityTraffic> GetIncomingByEntityAsync(MetricsWindow window, CancellationToken ct = default);
}
