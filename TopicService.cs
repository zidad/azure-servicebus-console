using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

public class TopicService(ServiceBusConnection connection, ILogger<TopicService> logger) : ITopicService
{
    public async IAsyncEnumerable<TopicInfo> GetTopicsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        logger.LogInformation("Listing topics");

        if (connection.IsEmulator)
        {
            var config = EmulatorConfigReader.TryLoad()
                ?? throw new InvalidOperationException("emulator/Config.json not found. Run from the project directory.");

            foreach (var t in config.Topics)
            {
                ct.ThrowIfCancellationRequested();
                yield return new TopicInfo { Name = t.Name, SubscriptionCount = t.Subscriptions.Count };
            }
            yield break;
        }

        await foreach (var page in connection.AdminClient.GetTopicsAsync().AsPages().WithCancellation(ct))
        {
            foreach (var topic in page.Values)
            {
                ct.ThrowIfCancellationRequested();
                var runtime = await connection.AdminClient.GetTopicRuntimePropertiesAsync(topic.Name, ct);
                yield return new TopicInfo
                {
                    Name = topic.Name,
                    SubscriptionCount = runtime.Value.SubscriptionCount,
                    ScheduledMessageCount = runtime.Value.ScheduledMessageCount
                };
            }
        }
    }

    public async IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(
        string topicName, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        logger.LogInformation("Listing subscriptions for topic {Topic}", topicName);

        if (connection.IsEmulator)
        {
            var config = EmulatorConfigReader.TryLoad()
                ?? throw new InvalidOperationException("emulator/Config.json not found. Run from the project directory.");

            var topic = config.Topics.FirstOrDefault(t => t.Name == topicName)
                ?? throw new InvalidOperationException($"Topic '{topicName}' not found in emulator config.");

            foreach (var sub in topic.Subscriptions)
            {
                ct.ThrowIfCancellationRequested();
                var (active, dlq) = await PeekSubscriptionCountsAsync(topicName, sub.Name, ct);
                yield return new SubscriptionInfo
                {
                    Name = sub.Name, TopicName = topicName,
                    ActiveMessageCount = active, DeadLetterMessageCount = dlq
                };
            }
            yield break;
        }

        await foreach (var page in connection.AdminClient.GetSubscriptionsAsync(topicName).AsPages().WithCancellation(ct))
        {
            foreach (var sub in page.Values)
            {
                ct.ThrowIfCancellationRequested();
                var runtime = await connection.AdminClient.GetSubscriptionRuntimePropertiesAsync(topicName, sub.SubscriptionName, ct);
                yield return new SubscriptionInfo
                {
                    Name = sub.SubscriptionName,
                    TopicName = topicName,
                    ActiveMessageCount = runtime.Value.ActiveMessageCount,
                    DeadLetterMessageCount = runtime.Value.DeadLetterMessageCount,
                    TransferMessageCount = runtime.Value.TransferMessageCount
                };
            }
        }
    }

    public async IAsyncEnumerable<SubscriptionInfo> GetAllSubscriptionsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        logger.LogInformation("Listing all subscriptions across all topics");
        await foreach (var topic in GetTopicsAsync(ct))
            await foreach (var sub in GetSubscriptionsAsync(topic.Name, ct))
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
