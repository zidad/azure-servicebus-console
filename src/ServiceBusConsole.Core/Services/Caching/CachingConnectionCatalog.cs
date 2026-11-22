using System.Runtime.CompilerServices;

namespace ServiceBusConsole.Core.Services.Caching;

public sealed class CachingConnectionCatalog(IConnectionCatalog inner, FileCache cache) : IConnectionCatalog
{
    public IAsyncEnumerable<ConnectionInfo> GetConnectionsAsync(CancellationToken ct = default) =>
        cache.CachedThenLiveAsync("subscriptions.json", () => inner.GetConnectionsAsync(ct), s => s, ct);

    // Local and changed by `az account set` at any time: never cached.
    public Task<string?> GetCurrentConnectionIdAsync(CancellationToken ct = default) => inner.GetCurrentConnectionIdAsync(ct);

    public IAsyncEnumerable<BrokerInfo> GetBrokersAsync(string subscriptionId, CancellationToken ct = default) =>
        cache.CachedThenLiveAsync(CacheKeys.Brokers(subscriptionId), () => inner.GetBrokersAsync(subscriptionId, ct), ns => ns, ct);

    public async IAsyncEnumerable<bool> HasBrokersAsync(string subscriptionId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var key = $"{subscriptionId}/has-namespaces.json";
        if (await cache.LoadAsync<bool>(key) is [var cached]) yield return cached;

        await foreach (var has in inner.HasBrokersAsync(subscriptionId, ct))
        {
            _ = cache.SaveAsync(key, [has]);
            yield return has;
        }
    }
}
