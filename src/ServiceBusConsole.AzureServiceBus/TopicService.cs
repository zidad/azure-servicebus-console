using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.AzureServiceBus;

/// <summary>Topics and subscriptions from the management API.</summary>
internal sealed class TopicService(ServiceBusAdministrationClient admin, ILogger<TopicService> logger) : ITopicService
{
    // Topics listed in parallel (all subscriptions): a handful at once, kept low so a large namespace
    // doesn't trip Service Bus request throttling.
    private const int MaxConcurrency = 8;

    public IAsyncEnumerable<TopicInfo> GetTopicsAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Listing topics");
        // One call per page of topics, so polling stays cheap.
        return admin.GetTopicsRuntimePropertiesAsync(ct).Select(runtime => new TopicInfo
        {
            Name = runtime.Name,
            SubscriptionCount = runtime.SubscriptionCount,
            ScheduledMessageCount = runtime.ScheduledMessageCount,
        });
    }

    public IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(string topicName, CancellationToken ct = default)
    {
        logger.LogInformation("Listing subscriptions for topic {Topic}", topicName);
        return GetSubscriptionsRuntimeAsync(topicName, ct);
    }

    public IAsyncEnumerable<SubscriptionInfo> GetAllSubscriptionsAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Listing all subscriptions across all topics");
        // One paged listing per topic, a few topics at a time.
        return AsyncParallel.MapAsync(
                admin.GetTopicsAsync(ct).Select(t => t.Name),
                (topic, token) => GetSubscriptionsRuntimeAsync(topic, token).ToListAsync(token).AsTask(),
                MaxConcurrency, ct)
            .SelectMany(subs => subs.ToAsyncEnumerable());
    }

    private IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsRuntimeAsync(string topicName, CancellationToken ct) =>
        admin.GetSubscriptionsRuntimePropertiesAsync(topicName, ct).Select(runtime => new SubscriptionInfo
        {
            Name = runtime.SubscriptionName,
            TopicName = topicName,
            ActiveMessageCount = runtime.ActiveMessageCount,
            DeadLetterMessageCount = runtime.DeadLetterMessageCount,
            TransferMessageCount = runtime.TransferMessageCount,
            AccessedAt = runtime.AccessedAt,
        });

    public IAsyncEnumerable<SubscriptionForwarding> GetForwardingAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Listing subscription forwarding across all topics");
        return AsyncParallel.MapAsync(
                admin.GetTopicsAsync(ct).Select(t => t.Name),
                (topic, token) => admin.GetSubscriptionsAsync(topic, token)
                    .Select(s => new SubscriptionForwarding(topic, s.SubscriptionName, Blank(s.ForwardTo), Blank(s.ForwardDeadLetteredMessagesTo)))
                    .ToListAsync(token).AsTask(),
                MaxConcurrency, ct)
            .SelectMany(subs => subs.ToAsyncEnumerable());
    }

    private static string? Blank(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
