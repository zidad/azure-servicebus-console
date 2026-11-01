using System.Runtime.CompilerServices;
using System.Xml;

namespace ServiceBusConsole.Services.Caching;

/// <summary>The cached metrics of the same span and interval still inside the window first, then the live ones.</summary>
public sealed class CachingMetricsService(IMetricsService inner, ServiceBusConnection connection, FileCache cache) : IMetricsService
{
    public async IAsyncEnumerable<MessageMetrics> GetMetricsAsync(MetricsScope scope, MetricsWindow window, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var key = $"{connection.CurrentNamespace}/metrics/{scope.Match(_ => "namespace", e => $"entities/{e.Name}")}/{Grain(window.To - window.From)}-{Grain(window.Interval)}.json";
        if (await cache.LoadAsync<MessageMetrics>(key) is [var cached] && cached.Within(window) is { HasData: true } recent)
            yield return recent with { IsRefreshing = true };

        await foreach (var live in inner.GetMetricsAsync(scope, window, ct))
        {
            _ = cache.SaveAsync(key, [live]);
            yield return live;
        }
    }

    // ISO 8601, as Azure Monitor names its grains: P1D, PT30M.
    private static string Grain(TimeSpan span) => XmlConvert.ToString(span);
}
