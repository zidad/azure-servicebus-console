using Azure.ResourceManager.Monitor.Models;

namespace ServiceBusConsole.AzureServiceBus;

/// <summary>Azure Monitor queries for <see cref="MessageMetrics"/>, and their results mapped back.</summary>
internal static class MonitorMetrics
{
    public const string Incoming = "IncomingMessages";
    public const string Outgoing = "OutgoingMessages";
    public const string Active = "ActiveMessages";
    public const string DeadLettered = "DeadletteredMessages";

    /// <summary>One query per aggregation: counts per interval are totals, backlog sizes their maximum.</summary>
    public static IReadOnlyList<ArmResourceGetMonitorMetricsOptions> Queries(MetricsScope scope, MetricsWindow window) =>
    [
        Query([Incoming, Outgoing], "Total", scope, window),
        Query([Active, DeadLettered], "Maximum", scope, window),
    ];

    /// <summary>
    /// Only queues and topics are dimensions; the namespace is the unfiltered total. Entity names can't contain quotes,
    /// and Azure Monitor rejects escaped ones.
    /// </summary>
    public static string? Filter(MetricsScope scope) =>
        scope.Match<string?>(_ => null, entity => $"EntityName eq '{entity.Name}'");

    public static MessageMetrics ToMessageMetrics(MetricsWindow window, IEnumerable<MonitorMetric> metrics)
    {
        var byName = metrics.ToLookup(m => m.Name.Value, StringComparer.OrdinalIgnoreCase);
        return new()
        {
            Window = window,
            Incoming = Points(byName[Incoming], window, v => v.Total),
            Outgoing = Points(byName[Outgoing], window, v => v.Total),
            Active = Points(byName[Active], window, v => v.Maximum),
            DeadLettered = Points(byName[DeadLettered], window, v => v.Maximum),
        };
    }

    private static ArmResourceGetMonitorMetricsOptions Query(string[] names, string aggregation, MetricsScope scope, MetricsWindow window) => new()
    {
        Timespan = $"{window.From.UtcDateTime:O}/{window.To.UtcDateTime:O}",
        Interval = window.Interval,
        Metricnames = string.Join(',', names),
        Aggregation = aggregation,
        Filter = Filter(scope),
    };

    // Every interval of the window, 0 where Azure reports nothing; several time series add up.
    private static List<MetricPoint> Points(IEnumerable<MonitorMetric> metrics, MetricsWindow window, Func<MonitorMetricValue, double?> value)
    {
        var byInterval = metrics.SelectMany(m => m.Timeseries).SelectMany(series => series.Data).ToLookup(v => window.IntervalOf(v.TimeStamp));
        return [.. window.IntervalStarts().Select(start => new MetricPoint(start, byInterval[start].Sum(v => value(v) ?? 0)))];
    }
}
