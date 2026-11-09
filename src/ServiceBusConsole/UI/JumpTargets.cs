namespace ServiceBusConsole.UI;

/// <summary>The queues, topics and subscriptions of a namespace, from the cached listings, or live when one isn't cached yet.</summary>
public static class JumpTargets
{
    public static async Task<IReadOnlyList<JumpTarget>> LoadAsync(IServiceBusNamespace current, FileCache cache)
    {
        var ns = current.Info;
        var queues = await cache.LoadAsync<QueueInfo>(CacheKeys.Queues(ns)) ?? await current.Queues.GetQueuesAsync().ToListAsync();
        if (current is not IHasTopics { Topics: var topicService }) return [.. queues.Select(q => new JumpTarget.Queue(q))];
        var topics = await cache.LoadAsync<TopicInfo>(CacheKeys.Topics(ns)) ?? await topicService.GetTopicsAsync().ToListAsync();
        var subscriptions = await cache.LoadAsync<SubscriptionInfo>(CacheKeys.AllSubscriptions(ns)) ?? await topicService.GetAllSubscriptionsAsync().ToListAsync();
        return [.. queues.Select(q => (JumpTarget)new JumpTarget.Queue(q)),
                .. topics.Select(t => new JumpTarget.Topic(t)),
                .. subscriptions.Select(s => new JumpTarget.Subscription(s))];
    }
}
