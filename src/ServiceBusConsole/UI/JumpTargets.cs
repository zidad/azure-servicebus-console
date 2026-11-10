namespace ServiceBusConsole.UI;

/// <summary>What the jump dialog searches: the current namespace's entities, and from the cache only other namespaces and their entities.</summary>
public static class JumpTargets
{
    /// <summary>The queues, topics and subscriptions of a namespace, from the cached listings, or live when one isn't cached yet.</summary>
    public static async Task<IReadOnlyList<JumpTarget>> LoadAsync(IServiceBusNamespace current, FileCache cache)
    {
        var ns = current.Info;
        var queues = await cache.LoadAsync<QueueInfo>(CacheKeys.Queues(ns)) ?? await current.Queues.GetQueuesAsync().ToListAsync();
        if (current is not IHasTopics { Topics: var topicService }) return [.. queues.Select(q => new JumpTarget.Queue(q))];
        var topics = await cache.LoadAsync<TopicInfo>(CacheKeys.Topics(ns)) ?? await topicService.GetTopicsAsync().ToListAsync();
        var subscriptions = await cache.LoadAsync<SubscriptionInfo>(CacheKeys.AllSubscriptions(ns)) ?? await topicService.GetAllSubscriptionsAsync().ToListAsync();
        return Entities(queues, topics, subscriptions, elsewhere: null);
    }

    /// <summary>The namespaces cached for <paramref name="subscriptions"/>; the emulator has no listing.</summary>
    public static async Task<IReadOnlyList<JumpNamespace>> NamespacesAsync(IEnumerable<AzureSubscription> subscriptions, FileCache cache)
    {
        var lists = await Task.WhenAll(subscriptions.Select(async s => s.Id is { } id
            ? (await cache.LoadAsync<NamespaceInfo>(CacheKeys.Namespaces(id)) ?? []).Select(ns => new JumpNamespace(s, ns))
            : []));
        return [.. lists.SelectMany(l => l)];
    }

    /// <summary>Another namespace's entities from its cached listings only: searching all namespaces makes no requests.</summary>
    public static async Task<IReadOnlyList<JumpTarget>> CachedAsync(JumpNamespace place, FileCache cache) =>
        Entities(await cache.LoadAsync<QueueInfo>(CacheKeys.Queues(place.Info)) ?? [],
            await cache.LoadAsync<TopicInfo>(CacheKeys.Topics(place.Info)) ?? [],
            await cache.LoadAsync<SubscriptionInfo>(CacheKeys.AllSubscriptions(place.Info)) ?? [],
            place);

    private static IReadOnlyList<JumpTarget> Entities(IEnumerable<QueueInfo> queues, IEnumerable<TopicInfo> topics, IEnumerable<SubscriptionInfo> subscriptions, JumpNamespace? elsewhere) =>
        [.. queues.Select(q => (JumpTarget)new JumpTarget.Queue(q) { Elsewhere = elsewhere }),
            .. topics.Select(t => new JumpTarget.Topic(t) { Elsewhere = elsewhere }),
            .. subscriptions.Select(s => new JumpTarget.Subscription(s) { Elsewhere = elsewhere })];
}
