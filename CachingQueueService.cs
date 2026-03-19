namespace ServiceBusConsole;

public class CachingQueueService(IQueueService inner, ServiceBusConnection connection, FileCache cache) : IQueueService
{
    public async IAsyncEnumerable<QueueInfo> GetQueuesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var key = $"{connection.CurrentNamespace}/queues.json";

        var cached = await cache.LoadAsync<QueueInfo>(key);
        if (cached != null)
            foreach (var q in cached)
            {
                if (ct.IsCancellationRequested) yield break;
                q.IsRefreshing = true;
                yield return q;
            }

        var live = new List<QueueInfo>();
        await foreach (var q in inner.GetQueuesAsync(ct))
        {
            live.Add(q);
            yield return q;
        }

        if (!ct.IsCancellationRequested && live.Count > 0)
            _ = cache.SaveAsync(key, live);
    }

    public Task DeleteQueueAsync(string queueName) => inner.DeleteQueueAsync(queueName);
}
