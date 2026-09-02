using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

public class TopicService(ServiceBusConnection connection, ILogger<TopicService> logger) : ITopicService
{
    // Every count below costs one round-trip, and they dominate load time. Run a handful
    // at once — kept low so a large namespace doesn't trip Service Bus request throttling.
    private const int MaxConcurrency = 8;

    public async IAsyncEnumerable<TopicInfo> GetTopicsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        logger.LogInformation("Listing topics");

        if (connection.IsEmulator)
        {
            foreach (var t in LoadEmulatorConfig().Topics)
            {
                ct.ThrowIfCancellationRequested();
                yield return new TopicInfo { Name = t.Name, SubscriptionCount = t.Subscriptions.Count };
            }
            yield break;
        }

        await foreach (var topic in AsyncParallel.MapAsync(
            GetTopicNamesAsync(ct), GetTopicInfoAsync, MaxConcurrency, ct))
            yield return topic;
    }

    public async IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(
        string topicName, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        logger.LogInformation("Listing subscriptions for topic {Topic}", topicName);

        await foreach (var sub in AsyncParallel.MapAsync(
            GetSubscriptionNamesAsync(topicName, ct),
            (name, token) => GetSubscriptionInfoAsync(topicName, name, token),
            MaxConcurrency, ct))
            yield return sub;
    }

    public async IAsyncEnumerable<SubscriptionInfo> GetAllSubscriptionsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        logger.LogInformation("Listing all subscriptions across all topics");

        // Only topic names are needed here, so the per-topic runtime lookup GetTopicsAsync
        // does is skipped. Two bounded stages: list each topic's subscriptions, then fetch
        // their counts — the second stage overlaps subscriptions from different topics.
        var byTopic = AsyncParallel.MapAsync(
            GetTopicNamesAsync(ct),
            async (topic, token) => (topic, names: await GetSubscriptionNamesAsync(topic, token).ToListAsync(token)),
            MaxConcurrency, ct);

        await foreach (var sub in AsyncParallel.MapAsync(
            Flatten(byTopic, ct),
            (pair, token) => GetSubscriptionInfoAsync(pair.topic, pair.name, token),
            MaxConcurrency, ct))
            yield return sub;
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

    private static async IAsyncEnumerable<(string topic, string name)> Flatten(
        IAsyncEnumerable<(string topic, List<string> names)> source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var (topic, names) in source.WithCancellation(ct))
            foreach (var name in names)
                yield return (topic, name);
    }

    private async IAsyncEnumerable<string> GetTopicNamesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (connection.IsEmulator)
        {
            foreach (var t in LoadEmulatorConfig().Topics)
            {
                ct.ThrowIfCancellationRequested();
                yield return t.Name;
            }
            yield break;
        }

        await foreach (var page in connection.AdminClient.GetTopicsAsync().AsPages().WithCancellation(ct))
            foreach (var topic in page.Values)
            {
                ct.ThrowIfCancellationRequested();
                yield return topic.Name;
            }
    }

    private async IAsyncEnumerable<string> GetSubscriptionNamesAsync(
        string topicName, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (connection.IsEmulator)
        {
            var topic = LoadEmulatorConfig().Topics.FirstOrDefault(t => t.Name == topicName)
                ?? throw new InvalidOperationException($"Topic '{topicName}' not found in emulator config.");

            foreach (var sub in topic.Subscriptions)
            {
                ct.ThrowIfCancellationRequested();
                yield return sub.Name;
            }
            yield break;
        }

        await foreach (var page in connection.AdminClient.GetSubscriptionsAsync(topicName).AsPages().WithCancellation(ct))
            foreach (var sub in page.Values)
            {
                ct.ThrowIfCancellationRequested();
                yield return sub.SubscriptionName;
            }
    }

    private async Task<TopicInfo> GetTopicInfoAsync(string topicName, CancellationToken ct)
    {
        var runtime = await connection.AdminClient.GetTopicRuntimePropertiesAsync(topicName, ct);
        return new TopicInfo
        {
            Name = topicName,
            SubscriptionCount = runtime.Value.SubscriptionCount,
            ScheduledMessageCount = runtime.Value.ScheduledMessageCount
        };
    }

    private async Task<SubscriptionInfo> GetSubscriptionInfoAsync(string topicName, string subscriptionName, CancellationToken ct)
    {
        if (connection.IsEmulator)
        {
            var (active, dlq) = await PeekSubscriptionCountsAsync(topicName, subscriptionName, ct);
            return new SubscriptionInfo
            {
                Name = subscriptionName, TopicName = topicName,
                ActiveMessageCount = active, DeadLetterMessageCount = dlq
            };
        }

        var runtime = await connection.AdminClient.GetSubscriptionRuntimePropertiesAsync(topicName, subscriptionName, ct);
        return new SubscriptionInfo
        {
            Name = subscriptionName,
            TopicName = topicName,
            ActiveMessageCount = runtime.Value.ActiveMessageCount,
            DeadLetterMessageCount = runtime.Value.DeadLetterMessageCount,
            TransferMessageCount = runtime.Value.TransferMessageCount
        };
    }

    private static EmulatorNamespace LoadEmulatorConfig() =>
        EmulatorConfigReader.TryLoad()
            ?? throw new InvalidOperationException("emulator/Config.json not found. Run from the project directory.");

    private async Task<(long active, long dlq)> PeekSubscriptionCountsAsync(string topic, string sub, CancellationToken ct)
    {
        long active = 0, dlq = 0;
        try
        {
            await using var r = connection.BusClient.CreateReceiver(topic, sub);
            active = (await r.PeekMessagesAsync(1000, cancellationToken: ct)).Count;
        }
        catch (Exception ex) { logger.LogDebug("Peek active {Topic}/{Sub}: {Error}", topic, sub, ex.Message); }
        try
        {
            await using var r = connection.BusClient.CreateReceiver(topic, sub,
                new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
            dlq = (await r.PeekMessagesAsync(1000, cancellationToken: ct)).Count;
        }
        catch (Exception ex) { logger.LogDebug("Peek DLQ {Topic}/{Sub}: {Error}", topic, sub, ex.Message); }
        return (active, dlq);
    }
}
