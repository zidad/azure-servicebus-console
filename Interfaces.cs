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
    Task<List<MessageInfo>> PeekMessagesAsync(string queueName, int count, bool fromDlq = false);
    Task<List<MessageInfo>> ReceiveMessagesAsync(string queueName, int count, bool fromDlq = false);
    Task<List<MessageInfo>> PeekSubscriptionMessagesAsync(string topicName, string subscriptionName, int count, bool fromDlq = false);
    Task<List<MessageInfo>> ReceiveSubscriptionMessagesAsync(string topicName, string subscriptionName, int count, bool fromDlq = false);
    Task DeleteMessageAsync(MessageSource source, long sequenceNumber);
}
