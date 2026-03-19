using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

public class TopicService(ServiceBusConnection connection, ILogger<TopicService> logger)
{
    public async Task<List<TopicInfo>> GetTopicsAsync()
    {
        logger.LogInformation("Listing topics");
        var topics = new List<TopicInfo>();

        await foreach (var page in connection.AdminClient.GetTopicsAsync().AsPages())
        {
            foreach (var topic in page.Values)
            {
                var runtime = await connection.AdminClient.GetTopicRuntimePropertiesAsync(topic.Name);
                topics.Add(new TopicInfo
                {
                    Name = topic.Name,
                    SubscriptionCount = runtime.Value.SubscriptionCount,
                    ScheduledMessageCount = runtime.Value.ScheduledMessageCount
                });
            }
        }

        logger.LogInformation("Found {Count} topics", topics.Count);
        return topics;
    }

    public async Task<List<SubscriptionInfo>> GetSubscriptionsAsync(string topicName)
    {
        logger.LogInformation("Listing subscriptions for topic {Topic}", topicName);
        var subscriptions = new List<SubscriptionInfo>();

        await foreach (var page in connection.AdminClient.GetSubscriptionsAsync(topicName).AsPages())
        {
            foreach (var sub in page.Values)
            {
                var runtime = await connection.AdminClient.GetSubscriptionRuntimePropertiesAsync(topicName, sub.SubscriptionName);
                subscriptions.Add(new SubscriptionInfo
                {
                    Name = sub.SubscriptionName,
                    ActiveMessageCount = runtime.Value.ActiveMessageCount,
                    DeadLetterMessageCount = runtime.Value.DeadLetterMessageCount,
                    TransferMessageCount = runtime.Value.TransferMessageCount
                });
            }
        }

        logger.LogInformation("Found {Count} subscriptions for {Topic}", subscriptions.Count, topicName);
        return subscriptions;
    }
}
