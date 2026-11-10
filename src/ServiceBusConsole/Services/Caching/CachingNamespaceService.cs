using System.Runtime.CompilerServices;

namespace ServiceBusConsole.Services.Caching;

public sealed class CachingNamespaceService(INamespaceService inner, FileCache cache) : INamespaceService
{
    public IAsyncEnumerable<AzureSubscription> GetSubscriptionsAsync(CancellationToken ct = default) =>
        cache.CachedThenLiveAsync("subscriptions.json", () => inner.GetSubscriptionsAsync(ct), s => s, ct);

    // Local and changed by `az account set` at any time: never cached.
    public Task<string?> GetCurrentSubscriptionIdAsync(CancellationToken ct = default) => inner.GetCurrentSubscriptionIdAsync(ct);

    public IAsyncEnumerable<NamespaceInfo> GetNamespacesAsync(string subscriptionId, CancellationToken ct = default) =>
        cache.CachedThenLiveAsync(CacheKeys.Namespaces(subscriptionId), () => inner.GetNamespacesAsync(subscriptionId, ct), ns => ns, ct);

    public async IAsyncEnumerable<bool> HasNamespacesAsync(string subscriptionId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var key = $"{subscriptionId}/has-namespaces.json";
        if (await cache.LoadAsync<bool>(key) is [var cached]) yield return cached;

        await foreach (var has in inner.HasNamespacesAsync(subscriptionId, ct))
        {
            _ = cache.SaveAsync(key, [has]);
            yield return has;
        }
    }
}
