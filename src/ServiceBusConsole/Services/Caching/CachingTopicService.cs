namespace ServiceBusConsole.Services.Caching;

public sealed class CachingTopicService(ITopicService inner, ServiceBusConnection connection, FileCache cache) : ITopicService
{
    private string TopicsKey => $"{connection.CurrentNamespace}/topics.json";
    private string AllSubscriptionsKey => $"{connection.CurrentNamespace}/all-subscriptions.json";
    private string SubscriptionsKey(string topicName) => $"{connection.CurrentNamespace}/topics/{topicName}/subscriptions.json";

    public IAsyncEnumerable<TopicInfo> GetTopicsAsync(CancellationToken ct = default) =>
        cache.CachedThenLiveAsync(TopicsKey, () => inner.GetTopicsAsync(ct), t => t with { IsRefreshing = true }, ct);

    public IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(string topicName, CancellationToken ct = default) =>
        cache.CachedThenLiveAsync(SubscriptionsKey(topicName), () => inner.GetSubscriptionsAsync(topicName, ct), s => s with { IsRefreshing = true }, ct);

    public IAsyncEnumerable<SubscriptionInfo> GetAllSubscriptionsAsync(CancellationToken ct = default) =>
        cache.CachedThenLiveAsync(AllSubscriptionsKey, () => inner.GetAllSubscriptionsAsync(ct), s => s with { IsRefreshing = true }, ct);

    public async Task DeleteTopicAsync(string topicName)
    {
        await inner.DeleteTopicAsync(topicName);
        await cache.RemoveAsync<TopicInfo>(TopicsKey, t => t.Name.Equals(topicName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task DeleteSubscriptionAsync(string topicName, string subscriptionName)
    {
        await inner.DeleteSubscriptionAsync(topicName, subscriptionName);
        await cache.RemoveAsync<SubscriptionInfo>(SubscriptionsKey(topicName),
            s => s.Name.Equals(subscriptionName, StringComparison.OrdinalIgnoreCase));
        await cache.RemoveAsync<SubscriptionInfo>(AllSubscriptionsKey,
            s => s.TopicName.Equals(topicName, StringComparison.OrdinalIgnoreCase)
                 && s.Name.Equals(subscriptionName, StringComparison.OrdinalIgnoreCase));
    }
}
