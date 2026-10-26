using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.Services;

public sealed class TopicService(ServiceBusConnection connection, ILogger<TopicService> logger) : ITopicService
{
    // Topics listed in parallel (all subscriptions), or emulator peeks: a handful at once,
    // kept low so a large namespace doesn't trip Service Bus request throttling.
    private const int MaxConcurrency = 8;

    public IAsyncEnumerable<TopicInfo> GetTopicsAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Listing topics");
        return connection.IsEmulator
            ? EmulatorCounts.Config().Topics
                .Select(t => new TopicInfo { Name = t.Name, SubscriptionCount = t.Subscriptions.Count })
                .ToAsyncEnumerable()
            // One call per page of topics, so polling stays cheap.
            : connection.AdminClient.GetTopicsRuntimePropertiesAsync(ct).Select(runtime => new TopicInfo
            {
                Name = runtime.Name,
                SubscriptionCount = runtime.SubscriptionCount,
                ScheduledMessageCount = runtime.ScheduledMessageCount,
            });
    }

    public IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(string topicName, CancellationToken ct = default)
    {
        logger.LogInformation("Listing subscriptions for topic {Topic}", topicName);
        return connection.IsEmulator
            ? AsyncParallel.MapAsync(EmulatorSubscriptions(topicName).ToAsyncEnumerable(), PeekSubscriptionAsync, MaxConcurrency, ct)
            : GetSubscriptionsRuntimeAsync(topicName, ct);
    }

    public IAsyncEnumerable<SubscriptionInfo> GetAllSubscriptionsAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Listing all subscriptions across all topics");
        return connection.IsEmulator
            ? AsyncParallel.MapAsync(
                EmulatorCounts.Config().Topics.SelectMany(t => EmulatorSubscriptions(t.Name)).ToAsyncEnumerable(),
                PeekSubscriptionAsync, MaxConcurrency, ct)
            // One paged listing per topic, a few topics at a time.
            : AsyncParallel.MapAsync(
                    connection.AdminClient.GetTopicsAsync(ct).Select(t => t.Name),
                    (topic, token) => GetSubscriptionsRuntimeAsync(topic, token).ToListAsync(token).AsTask(),
                    MaxConcurrency, ct)
                .SelectMany(subs => subs.ToAsyncEnumerable());
    }

    public async Task DeleteTopicAsync(string topicName)
    {
        logger.LogWarning("Deleting topic {Topic}", topicName);
        if (connection.IsEmulator)
            throw new NotSupportedException("Delete topic is not supported by the local emulator.");
        await connection.AdminClient.DeleteTopicAsync(topicName);
    }

    public async Task DeleteSubscriptionAsync(string topicName, string subscriptionName)
    {
        logger.LogWarning("Deleting subscription {Subscription} from topic {Topic}", subscriptionName, topicName);
        if (connection.IsEmulator)
            throw new NotSupportedException("Delete subscription is not supported by the local emulator.");
        await connection.AdminClient.DeleteSubscriptionAsync(topicName, subscriptionName);
    }

    private IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsRuntimeAsync(string topicName, CancellationToken ct) =>
        connection.AdminClient.GetSubscriptionsRuntimePropertiesAsync(topicName, ct).Select(runtime => new SubscriptionInfo
        {
            Name = runtime.SubscriptionName,
            TopicName = topicName,
            ActiveMessageCount = runtime.ActiveMessageCount,
            DeadLetterMessageCount = runtime.DeadLetterMessageCount,
            TransferMessageCount = runtime.TransferMessageCount,
        });

    private static IEnumerable<(string Topic, string Subscription)> EmulatorSubscriptions(string topicName) =>
        (EmulatorCounts.Config().Topics.FirstOrDefault(t => t.Name == topicName)
            ?? throw new InvalidOperationException($"Topic '{topicName}' not found in emulator config."))
        .Subscriptions.Select(s => (topicName, s.Name));

    private async Task<SubscriptionInfo> PeekSubscriptionAsync((string Topic, string Subscription) entity, CancellationToken ct)
    {
        var (active, dlq) = await EmulatorCounts.PeekAsync(connection.BusClient, entity.Topic, entity.Subscription, logger, ct);
        return new SubscriptionInfo
        {
            Name = entity.Subscription,
            TopicName = entity.Topic,
            ActiveMessageCount = active,
            DeadLetterMessageCount = dlq,
        };
    }
}
