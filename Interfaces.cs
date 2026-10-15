namespace ServiceBusConsole;

public interface INamespaceService
{
    IAsyncEnumerable<NamespaceInfo> GetNamespacesAsync(string subscriptionId, CancellationToken ct = default);
}

public interface IQueueService
{
    IAsyncEnumerable<QueueInfo> GetQueuesAsync(CancellationToken ct = default);
    Task DeleteQueueAsync(string queueName);
}

public interface ITopicService
{
    IAsyncEnumerable<TopicInfo> GetTopicsAsync(CancellationToken ct = default);
    IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(string topicName, CancellationToken ct = default);
    IAsyncEnumerable<SubscriptionInfo> GetAllSubscriptionsAsync(CancellationToken ct = default);
    Task DeleteTopicAsync(string topicName);
    Task DeleteSubscriptionAsync(string topicName, string subscriptionName);
}

public interface IMessageService
{
    Task<List<MessageInfo>> PeekAsync(MessageSource source, int count, long? fromSequenceNumber = null, CancellationToken ct = default);
    Task<List<MessageInfo>> ReceiveAndDeleteAsync(MessageSource source, int count, CancellationToken ct = default);
    Task<LockedMessages> ReceiveLockedAsync(MessageSource source, int count, CancellationToken ct = default);
    /// <summary>Sends copies in order, in as few batches as fit; <paramref name="onSent"/> gets the running total after each batch.</summary>
    Task SendCopiesAsync(MessageSource source, IReadOnlyList<MessageInfo> messages, Action<int> onSent, CancellationToken ct = default);
    Task<int> PurgeMessagesAsync(MessageSource source);
}
