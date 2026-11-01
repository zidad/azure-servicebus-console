namespace ServiceBusConsole.Services.Caching;

public sealed class CachingQueueService(IQueueService inner, NamespaceInfo ns, FileCache cache) : IQueueService
{
    public IAsyncEnumerable<QueueInfo> GetQueuesAsync(CancellationToken ct = default) =>
        cache.CachedThenLiveAsync(CacheKeys.Queues(ns), () => inner.GetQueuesAsync(ct), q => q with { IsRefreshing = true }, ct);
}
