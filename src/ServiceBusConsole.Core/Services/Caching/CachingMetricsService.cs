using System.Runtime.CompilerServices;

namespace ServiceBusConsole.Core.Services.Caching;

/// <summary>The cached metrics of the same span and interval still inside the window first, then the live ones.</summary>
public sealed class CachingMetricsService(IMetricsService inner, NamespaceInfo ns, FileCache cache) : IMetricsService
{
    public async IAsyncEnumerable<MessageMetrics> GetMetricsAsync(MetricsScope scope, MetricsWindow window, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var key = CacheKeys.Metrics(ns, scope, window);
        if (await cache.LoadAsync<MessageMetrics>(key) is [var cached] && cached.Within(window) is { HasData: true } recent)
            yield return recent with { IsRefreshing = true };

        await foreach (var live in inner.GetMetricsAsync(scope, window, ct))
        {
            _ = cache.SaveAsync(key, [live]);
            yield return live;
        }
    }

    public async IAsyncEnumerable<EntityTraffic> GetIncomingByEntityAsync(MetricsWindow window, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var key = CacheKeys.IncomingByEntity(ns, window);
        if (await cache.LoadAsync<EntityTraffic>(key) is [var cached])
            yield return cached with { IsRefreshing = true };

        await foreach (var live in inner.GetIncomingByEntityAsync(window, ct))
        {
            _ = cache.SaveAsync(key, [live]);
            yield return live;
        }
    }
}
