namespace ServiceBusConsole;

public interface INamespaceService
{
    IAsyncEnumerable<AzureSubscription> GetSubscriptionsAsync(CancellationToken ct = default);

    /// <summary>Whether the subscription has any Service Bus namespace: cached answer first, then live.</summary>
    IAsyncEnumerable<bool> HasNamespacesAsync(string subscriptionId, CancellationToken ct = default);
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
    /// <summary>Peeks up to <paramref name="count"/> messages, one page per round trip.</summary>
    IAsyncEnumerable<IReadOnlyList<MessageInfo>> PeekAsync(MessageSource source, int count, long? fromSequenceNumber = null, CancellationToken ct = default);

    /// <summary>Receives and deletes up to <paramref name="count"/> messages, one page per round trip.</summary>
    IAsyncEnumerable<IReadOnlyList<MessageInfo>> ReceiveAndDeleteAsync(MessageSource source, int count, CancellationToken ct = default);

    /// <summary>
    /// Starts receiving up to <paramref name="count"/> messages in PeekLock mode; each is written to
    /// <paramref name="received"/> once held. See <see cref="LockedMessages.StartReceive"/>.
    /// </summary>
    ILockedMessages StartReceiving(MessageSource source, int count, System.Threading.Channels.ChannelWriter<Azure.Messaging.ServiceBus.ServiceBusReceivedMessage> received, CancellationToken ct = default);

    /// <summary>
    /// Starts locking these messages by receiving from the front; each is written to <paramref name="found"/>
    /// once held. See <see cref="LockedMessages.StartScan"/>.
    /// </summary>
    ILockedMessages StartLocking(MessageSource source, IReadOnlyCollection<long> sequenceNumbers, System.Threading.Channels.ChannelWriter<Azure.Messaging.ServiceBus.ServiceBusReceivedMessage> found, CancellationToken ct = default);
    /// <summary>Sends copies in order, in as few batches as fit; <paramref name="onSent"/> gets the running total after each batch.</summary>
    Task SendCopiesAsync(MessageSource source, IReadOnlyList<MessageInfo> messages, Action<int> onSent, CancellationToken ct = default);
    /// <summary>Deletes every message; <paramref name="onPurged"/> gets the running total.</summary>
    Task<int> PurgeMessagesAsync(MessageSource source, Action<int> onPurged, CancellationToken ct = default);
}
