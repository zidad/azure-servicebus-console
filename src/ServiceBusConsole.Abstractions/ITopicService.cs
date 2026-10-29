namespace ServiceBusConsole.Abstractions;

public interface ITopicService
{
    IAsyncEnumerable<TopicInfo> GetTopicsAsync(CancellationToken ct = default);
    IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(string topicName, CancellationToken ct = default);
    IAsyncEnumerable<SubscriptionInfo> GetAllSubscriptionsAsync(CancellationToken ct = default);
    Task DeleteTopicAsync(string topicName, CancellationToken ct = default);
    Task DeleteSubscriptionAsync(string topicName, string subscriptionName, CancellationToken ct = default);
}
