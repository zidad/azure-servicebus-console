namespace ServiceBusConsole.Services.Caching;

/// <summary>Deletes, then drops the entity from the cached listings, so it doesn't show up again while they load.</summary>
public sealed class CachingDeletionService(IDeletionService inner, NamespaceInfo ns, FileCache cache) : IDeletionService
{
    public async Task DeleteQueueAsync(string queueName, CancellationToken ct = default)
    {
        await inner.DeleteQueueAsync(queueName, ct);
        await cache.RemoveAsync<QueueInfo>(CacheKeys.Queues(ns), q => q.Name.Equals(queueName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task DeleteTopicAsync(string topicName, CancellationToken ct = default)
    {
        await inner.DeleteTopicAsync(topicName, ct);
        await cache.RemoveAsync<TopicInfo>(CacheKeys.Topics(ns), t => t.Name.Equals(topicName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task DeleteSubscriptionAsync(string topicName, string subscriptionName, CancellationToken ct = default)
    {
        await inner.DeleteSubscriptionAsync(topicName, subscriptionName, ct);
        await cache.RemoveAsync<SubscriptionInfo>(CacheKeys.Subscriptions(ns, topicName),
            s => s.Name.Equals(subscriptionName, StringComparison.OrdinalIgnoreCase));
        await cache.RemoveAsync<SubscriptionInfo>(CacheKeys.AllSubscriptions(ns),
            s => s.TopicName.Equals(topicName, StringComparison.OrdinalIgnoreCase)
                 && s.Name.Equals(subscriptionName, StringComparison.OrdinalIgnoreCase));
    }
}
