namespace ServiceBusConsole.Services.Caching;

public sealed class CachingTopicService(ITopicService inner, NamespaceInfo ns, FileCache cache) : ITopicService
{
    public IAsyncEnumerable<TopicInfo> GetTopicsAsync(CancellationToken ct = default) =>
        cache.CachedThenLiveAsync(CacheKeys.Topics(ns), () => inner.GetTopicsAsync(ct), t => t with { IsRefreshing = true }, ct);

    public IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(string topicName, CancellationToken ct = default) =>
        cache.CachedThenLiveAsync(CacheKeys.Subscriptions(ns, topicName), () => inner.GetSubscriptionsAsync(topicName, ct), s => s with { IsRefreshing = true }, ct);

    public IAsyncEnumerable<SubscriptionInfo> GetAllSubscriptionsAsync(CancellationToken ct = default) =>
        cache.CachedThenLiveAsync(CacheKeys.AllSubscriptions(ns), () => inner.GetAllSubscriptionsAsync(ct), s => s with { IsRefreshing = true }, ct);
}
