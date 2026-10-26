using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

public class TopicService(ServiceBusConnection connection, ILogger<TopicService> logger) : ITopicService
{
    // Topics listed in parallel (all subscriptions), or emulator peeks: a handful at once,
    // kept low so a large namespace doesn't trip Service Bus request throttling.
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

        // One call per page of topics, so polling stays cheap.
        await foreach (var runtime in connection.AdminClient.GetTopicsRuntimePropertiesAsync(ct))
            yield return new TopicInfo
            {
                Name = runtime.Name,
                SubscriptionCount = runtime.SubscriptionCount,
                ScheduledMessageCount = runtime.ScheduledMessageCount,
            };
    }

    public async IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(
        string topicName, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        logger.LogInformation("Listing subscriptions for topic {Topic}", topicName);

        if (connection.IsEmulator)
        {
            await foreach (var sub in AsyncParallel.MapAsync(
                EmulatorSubscriptionNames(topicName),
                (name, token) => GetEmulatorSubscriptionInfoAsync(topicName, name, token),
                MaxConcurrency, ct))
                yield return sub;
            yield break;
        }

        await foreach (var sub in GetSubscriptionsRuntimeAsync(topicName, ct))
            yield return sub;
    }

    public async IAsyncEnumerable<SubscriptionInfo> GetAllSubscriptionsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        logger.LogInformation("Listing all subscriptions across all topics");

        if (connection.IsEmulator)
        {
            // Two bounded stages: list each topic's subscriptions, then peek their counts.
            var byTopic = AsyncParallel.MapAsync(
                GetTopicNamesAsync(ct),
                async (topic, token) => (topic, names: await EmulatorSubscriptionNames(topic).ToListAsync(token)),
                MaxConcurrency, ct);
            await foreach (var sub in AsyncParallel.MapAsync(
                Flatten(byTopic, ct),
                (pair, token) => GetEmulatorSubscriptionInfoAsync(pair.topic, pair.name, token),
                MaxConcurrency, ct))
                yield return sub;
            yield break;
        }

        // One paged listing per topic, a few topics at a time.
        await foreach (var subs in AsyncParallel.MapAsync(
            GetTopicNamesAsync(ct),
            (topic, token) => GetSubscriptionsRuntimeAsync(topic, token).ToListAsync(token).AsTask(),
            MaxConcurrency, ct))
            foreach (var sub in subs)
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

    private static IAsyncEnumerable<string> EmulatorSubscriptionNames(string topicName) =>
        (LoadEmulatorConfig().Topics.FirstOrDefault(t => t.Name == topicName)
            ?? throw new InvalidOperationException($"Topic '{topicName}' not found in emulator config."))
        .Subscriptions.Select(s => s.Name).ToAsyncEnumerable();

    private async IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsRuntimeAsync(
        string topicName, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var runtime in connection.AdminClient.GetSubscriptionsRuntimePropertiesAsync(topicName, ct))
            yield return new SubscriptionInfo
            {
                Name = runtime.SubscriptionName,
                TopicName = topicName,
                ActiveMessageCount = runtime.ActiveMessageCount,
                DeadLetterMessageCount = runtime.DeadLetterMessageCount,
                TransferMessageCount = runtime.TransferMessageCount,
            };
    }

    // The emulator has no management API: count by peeking.
    private async Task<SubscriptionInfo> GetEmulatorSubscriptionInfoAsync(string topicName, string subscriptionName, CancellationToken ct)
    {
        var (active, dlq) = await PeekSubscriptionCountsAsync(topicName, subscriptionName, ct);
        return new SubscriptionInfo
        {
            Name = subscriptionName, TopicName = topicName,
            ActiveMessageCount = active, DeadLetterMessageCount = dlq
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
