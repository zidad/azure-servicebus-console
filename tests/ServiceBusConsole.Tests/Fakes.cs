using System.Runtime.CompilerServices;

namespace ServiceBusConsole.Tests;

public sealed class FakeNamespaceService : INamespaceService
{
    public List<NamespaceInfo> Namespaces { get; } = [];

    public async IAsyncEnumerable<NamespaceInfo> GetNamespacesAsync(string subscriptionId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var ns in Namespaces)
        {
            await Task.Yield();
            yield return ns;
        }
    }
}

public sealed class FakeQueueService : IQueueService
{
    public List<QueueInfo> Queues { get; } = [];
    public List<string> Deleted { get; } = [];
    public Exception? LoadError { get; set; }

    public int Loads { get; private set; }

    public async IAsyncEnumerable<QueueInfo> GetQueuesAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        Loads++;
        await Task.Yield();
        if (LoadError is not null) throw LoadError;
        foreach (var q in Queues.ToList()) yield return q;
    }

    public Task DeleteQueueAsync(string queueName)
    {
        Deleted.Add(queueName);
        Queues.RemoveAll(q => q.Name == queueName);
        return Task.CompletedTask;
    }
}

public sealed class FakeTopicService : ITopicService
{
    public List<TopicInfo> Topics { get; } = [];
    public List<SubscriptionInfo> Subscriptions { get; } = [];

    public async IAsyncEnumerable<TopicInfo> GetTopicsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        foreach (var t in Topics) yield return t;
    }

    public async IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(string topicName, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        foreach (var s in Subscriptions.Where(s => s.TopicName == topicName)) yield return s;
    }

    public async IAsyncEnumerable<SubscriptionInfo> GetAllSubscriptionsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        foreach (var s in Subscriptions) yield return s;
    }

    public Task DeleteTopicAsync(string topicName) => Task.CompletedTask;

    public Task DeleteSubscriptionAsync(string topicName, string subscriptionName) => Task.CompletedTask;
}

public sealed class FakeMessageService : IMessageService
{
    public List<MessageInfo> Messages { get; } = [];
    public List<MessageSource> Purged { get; } = [];
    public List<MessageSource> Peeked { get; } = [];

    public Task<List<MessageInfo>> PeekAsync(MessageSource source, int count, long? fromSequenceNumber = null, CancellationToken ct = default)
    {
        Peeked.Add(source);
        return Task.FromResult(Messages.Where(m => fromSequenceNumber is null || m.SequenceNumber >= fromSequenceNumber).Take(count).ToList());
    }

    public Task<List<MessageInfo>> ReceiveAndDeleteAsync(MessageSource source, int count, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<LockedMessages> ReceiveLockedAsync(MessageSource source, int count, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task SendCopiesAsync(MessageSource source, IReadOnlyList<MessageInfo> messages, Action<int> onSent, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<int> PurgeMessagesAsync(MessageSource source)
    {
        Purged.Add(source);
        return Task.FromResult(0);
    }
}
