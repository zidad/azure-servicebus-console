using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

public class TopicService(ServiceBusConnection connection, ILogger<TopicService> logger) : ITopicService
{
    public async IAsyncEnumerable<TopicInfo> GetTopicsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        logger.LogInformation("Listing topics");

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
        {
            await foreach (var sub in GetSubscriptionsAsync(topic.Name, ct))
                yield return sub;
        }
    }
}
