using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using ServiceBusConsole.AzureServiceBus.Resources;

namespace ServiceBusConsole.AzureServiceBus.Emulator;

/// <summary>Topics and subscriptions from the emulator's config file, counts from peeking: it has no management API.</summary>
internal sealed class EmulatorTopicService(ServiceBusClient bus, ILogger<EmulatorTopicService> logger) : ITopicService
{
    // Two peeks per subscription, run a few at a time.
    private const int MaxConcurrency = 8;

    public IAsyncEnumerable<TopicInfo> GetTopicsAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Listing topics");
        return EmulatorCounts.Config().Topics
            .Select(t => new TopicInfo { Name = t.Name, SubscriptionCount = t.Subscriptions.Count })
            .ToAsyncEnumerable();
    }

    public IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(string topicName, CancellationToken ct = default)
    {
        logger.LogInformation("Listing subscriptions for topic {Topic}", topicName);
        return AsyncParallel.MapAsync(Subscriptions(topicName).ToAsyncEnumerable(), PeekSubscriptionAsync, MaxConcurrency, ct);
    }

    public IAsyncEnumerable<SubscriptionInfo> GetAllSubscriptionsAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Listing all subscriptions across all topics");
        return AsyncParallel.MapAsync(
            EmulatorCounts.Config().Topics.SelectMany(t => Subscriptions(t.Name)).ToAsyncEnumerable(),
            PeekSubscriptionAsync, MaxConcurrency, ct);
    }

    private static IEnumerable<(string Topic, string Subscription)> Subscriptions(string topicName) =>
        (EmulatorCounts.Config().Topics.FirstOrDefault(t => t.Name == topicName)
            ?? throw new InvalidOperationException(Strings.FormatEmulatorTopicService_TopicNotFound(topicName)))
        .Subscriptions.Select(s => (topicName, s.Name));

    private async Task<SubscriptionInfo> PeekSubscriptionAsync((string Topic, string Subscription) entity, CancellationToken ct)
    {
        var (active, dlq) = await EmulatorCounts.PeekAsync(bus, entity.Topic, entity.Subscription, logger, ct);
        return new SubscriptionInfo
        {
            Name = entity.Subscription,
            TopicName = entity.Topic,
            ActiveMessageCount = active,
            DeadLetterMessageCount = dlq,
        };
    }
}
