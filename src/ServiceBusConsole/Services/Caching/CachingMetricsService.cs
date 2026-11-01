using System.Runtime.CompilerServices;

namespace ServiceBusConsole.Services.Caching;

/// <summary>The cached metrics still inside the window first, then the live ones.</summary>
public sealed class CachingMetricsService(IMetricsService inner, ServiceBusConnection connection, FileCache cache) : IMetricsService
{
    public async IAsyncEnumerable<MessageMetrics> GetMetricsAsync(MetricsScope scope, MetricsWindow window, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var key = $"{connection.CurrentNamespace}/metrics/{scope.Match(_ => "namespace", e => $"entities/{e.Name}")}.json";
        if (await cache.LoadAsync<MessageMetrics>(key) is [var cached] && cached.Within(window) is { HasData: true } recent)
            yield return recent with { IsRefreshing = true };

        await foreach (var live in inner.GetMetricsAsync(scope, window, ct))
        {
            _ = cache.SaveAsync(key, [live]);
            yield return live;
        }
    }
}
