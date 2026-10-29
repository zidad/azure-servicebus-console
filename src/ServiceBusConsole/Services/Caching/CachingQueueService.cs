namespace ServiceBusConsole.Services.Caching;

public sealed class CachingQueueService(IQueueService inner, ServiceBusConnection connection, FileCache cache) : IQueueService
{
    private string QueuesKey => $"{connection.CurrentNamespace}/queues.json";

    public IAsyncEnumerable<QueueInfo> GetQueuesAsync(CancellationToken ct = default) =>
        cache.CachedThenLiveAsync(QueuesKey, () => inner.GetQueuesAsync(ct), q => q with { IsRefreshing = true }, ct);

    public async Task DeleteQueueAsync(string queueName, CancellationToken ct = default)
    {
        await inner.DeleteQueueAsync(queueName, ct);
        await cache.RemoveAsync<QueueInfo>(QueuesKey, q => q.Name.Equals(queueName, StringComparison.OrdinalIgnoreCase));
    }
}
