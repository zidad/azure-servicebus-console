namespace ServiceBusConsole.Abstractions;

public interface ITopicService
{
    IAsyncEnumerable<TopicInfo> GetTopicsAsync(CancellationToken ct = default);
    IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(string topicName, CancellationToken ct = default);
    IAsyncEnumerable<SubscriptionInfo> GetAllSubscriptionsAsync(CancellationToken ct = default);

    /// <summary>Where each subscription forwards its messages; one listing per topic, so only on request, not for polling.</summary>
    IAsyncEnumerable<SubscriptionForwarding> GetForwardingAsync(CancellationToken ct = default);
}
