namespace ServiceBusConsole;

public class CachingTopicService(ITopicService inner, ServiceBusConnection connection, FileCache cache) : ITopicService
{
    public async IAsyncEnumerable<TopicInfo> GetTopicsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var key = $"{connection.CurrentNamespace}/topics.json";

        var cached = await cache.LoadAsync<TopicInfo>(key);
        if (cached != null)
            foreach (var t in cached)
            {
                if (ct.IsCancellationRequested) yield break;
                t.IsRefreshing = true;
                yield return t;
            }

        var live = new List<TopicInfo>();
        await foreach (var t in inner.GetTopicsAsync(ct))
        {
            live.Add(t);
            yield return t;
        }

        if (!ct.IsCancellationRequested && live.Count > 0)
            _ = cache.SaveAsync(key, live);
    }

    public async IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(
        string topicName, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var key = $"{connection.CurrentNamespace}/topics/{topicName}/subscriptions.json";

        var cached = await cache.LoadAsync<SubscriptionInfo>(key);
        if (cached != null)
            foreach (var s in cached)
            {
                if (ct.IsCancellationRequested) yield break;
                s.IsRefreshing = true;
                yield return s;
            }

        var live = new List<SubscriptionInfo>();
        await foreach (var s in inner.GetSubscriptionsAsync(topicName, ct))
        {
            live.Add(s);
            yield return s;
        }

        if (!ct.IsCancellationRequested && live.Count > 0)
            _ = cache.SaveAsync(key, live);
    }

    public async IAsyncEnumerable<SubscriptionInfo> GetAllSubscriptionsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var key = $"{connection.CurrentNamespace}/all-subscriptions.json";

        var cached = await cache.LoadAsync<SubscriptionInfo>(key);
        if (cached != null)
            foreach (var s in cached)
            {
                if (ct.IsCancellationRequested) yield break;
                s.IsRefreshing = true;
                yield return s;
            }

        var live = new List<SubscriptionInfo>();
        await foreach (var s in inner.GetAllSubscriptionsAsync(ct))
        {
            live.Add(s);
            yield return s;
        }

        if (!ct.IsCancellationRequested && live.Count > 0)
            _ = cache.SaveAsync(key, live);
    }

    public Task DeleteTopicAsync(string topicName) => inner.DeleteTopicAsync(topicName);
    public Task DeleteSubscriptionAsync(string topicName, string subscriptionName) => inner.DeleteSubscriptionAsync(topicName, subscriptionName);
}
