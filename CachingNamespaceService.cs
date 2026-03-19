namespace ServiceBusConsole;

public class CachingNamespaceService(INamespaceService inner, FileCache cache) : INamespaceService
{
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
