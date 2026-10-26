namespace ServiceBusConsole.Services.Caching;

public class CachingNamespaceService(INamespaceService inner, FileCache cache) : INamespaceService
{
    public async IAsyncEnumerable<AzureSubscription> GetSubscriptionsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        const string key = "subscriptions.json";

        var cached = await cache.LoadAsync<AzureSubscription>(key);
        if (cached != null)
            foreach (var sub in cached)
            {
                if (ct.IsCancellationRequested) yield break;
                yield return sub;
            }

        var live = new List<AzureSubscription>();
        await foreach (var sub in inner.GetSubscriptionsAsync(ct))
        {
            live.Add(sub);
            yield return sub;
        }

        if (!ct.IsCancellationRequested && live.Count > 0)
            _ = cache.SaveAsync(key, live);
    }

    public async IAsyncEnumerable<bool> HasNamespacesAsync(
        string subscriptionId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var key = $"{subscriptionId}/has-namespaces.json";

        if (await cache.LoadAsync<bool>(key) is [var cached]) yield return cached;

        await foreach (var has in inner.HasNamespacesAsync(subscriptionId, ct))
        {
            _ = cache.SaveAsync(key, [has]);
            yield return has;
        }
    }

    public async IAsyncEnumerable<NamespaceInfo> GetNamespacesAsync(
        string subscriptionId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var key = $"{subscriptionId}/namespaces.json";

        var cached = await cache.LoadAsync<NamespaceInfo>(key);
        if (cached != null)
            foreach (var ns in cached)
            {
                if (ct.IsCancellationRequested) yield break;
                yield return ns;
            }

        var live = new List<NamespaceInfo>();
        await foreach (var ns in inner.GetNamespacesAsync(subscriptionId, ct))
        {
            live.Add(ns);
            yield return ns;
        }

        if (!ct.IsCancellationRequested && live.Count > 0)
            _ = cache.SaveAsync(key, live);
    }
}
