using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.UI;

/// <summary>
/// The last 24 hours of incoming messages per queue and topic, for a list page's sparkline column: cached first, then
/// live, then reloaded every <see cref="LiveCountsOptions.TrafficInterval"/>. One Azure Monitor request per load,
/// whatever the number of entities. Call from the page's dispatcher; <see cref="Changed"/> runs there too.
/// </summary>
public sealed class IncomingTraffic(IMetricsService metrics, LiveCountsOptions options, ILogger logger) : IDisposable
{
    public static readonly TimeSpan Span = TimeSpan.FromHours(24);
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly CancellationTokenSource _cts = new();

    public EntityTraffic? Traffic { get; private set; }

    /// <summary>Render; after each load.</summary>
    public required Func<Task> Changed { get; init; }

    public IReadOnlyList<MetricPoint> Of(string entity) => Traffic?.Of(entity) ?? [];

    /// <summary>Loads, then keeps reloading until <see cref="Dispose"/>; a failed load keeps what was shown.</summary>
    public async Task RunAsync()
    {
        var token = _cts.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await foreach (var traffic in metrics.GetIncomingByEntityAsync(MetricsWindow.Last(Span, Interval, DateTimeOffset.UtcNow), token))
                    {
                        Traffic = traffic;
                        await Changed();
                    }
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "Loading incoming messages per entity failed; retrying");
                }
                await Task.Delay(options.TrafficInterval, token);
            }
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
