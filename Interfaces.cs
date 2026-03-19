namespace ServiceBusConsole;

public interface INamespaceService
{
    IAsyncEnumerable<NamespaceInfo> GetNamespacesAsync(string subscriptionId, CancellationToken ct = default);
}

public interface IQueueService
{
    IAsyncEnumerable<QueueInfo> GetQueuesAsync(CancellationToken ct = default);
}

public interface ITopicService
{
    IAsyncEnumerable<TopicInfo> GetTopicsAsync(CancellationToken ct = default);
    IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(string topicName, CancellationToken ct = default);
    IAsyncEnumerable<SubscriptionInfo> GetAllSubscriptionsAsync(CancellationToken ct = default);
}

public interface IMessageService
{
    Task<List<MessageInfo>> PeekMessagesAsync(string queueName, int count, bool fromDlq = false);
    Task<List<MessageInfo>> ReceiveMessagesAsync(string queueName, int count, bool fromDlq = false);
}
