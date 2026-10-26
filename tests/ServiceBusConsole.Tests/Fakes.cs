using Azure.Messaging.ServiceBus;
using System.Runtime.CompilerServices;

namespace ServiceBusConsole.Tests;

public sealed class FakeNamespaceService : INamespaceService
{
    public List<NamespaceInfo> Namespaces { get; } = [];
    public List<AzureSubscription> Subscriptions { get; } = [];
    public Exception? SubscriptionsError { get; set; }

    /// <summary>When set, the listing waits on it after the last subscription, like a slow live refresh.</summary>
    public TaskCompletionSource? HoldListing { get; set; }

    /// <summary>Subscription IDs that report no Service Bus namespaces.</summary>
    public HashSet<string> WithoutNamespaces { get; } = [];

    public async IAsyncEnumerable<bool> HasNamespacesAsync(string subscriptionId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        yield return !WithoutNamespaces.Contains(subscriptionId);
    }

    public async IAsyncEnumerable<AzureSubscription> GetSubscriptionsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        if (SubscriptionsError is not null) throw SubscriptionsError;
        foreach (var sub in Subscriptions)
        {
            await Task.Yield();
            yield return sub;
        }
        if (HoldListing is { } hold) await hold.Task.WaitAsync(ct);
    }

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
        // Fresh objects per listing, like the real service; tests change counts on the stored ones.
        // Tests that change the list during polling lock it.
        List<QueueInfo> queues;
        lock (Queues) queues = Queues.ToList();
        foreach (var q in queues)
            yield return new QueueInfo
            {
                Name = q.Name,
                ActiveMessageCount = q.ActiveMessageCount,
                DeadLetterMessageCount = q.DeadLetterMessageCount,
                ScheduledMessageCount = q.ScheduledMessageCount,
                TransferMessageCount = q.TransferMessageCount,
            };
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
        List<TopicInfo> topics;
        lock (Topics) topics = Topics.ToList();
        foreach (var t in topics) yield return new TopicInfo { Name = t.Name, SubscriptionCount = t.SubscriptionCount, ScheduledMessageCount = t.ScheduledMessageCount };
    }

    public async IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(string topicName, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        foreach (var s in Subscriptions.Where(s => s.TopicName == topicName).ToList()) yield return Copy(s);
    }

    public async IAsyncEnumerable<SubscriptionInfo> GetAllSubscriptionsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        foreach (var s in Subscriptions.ToList()) yield return Copy(s);
    }

    public Task DeleteTopicAsync(string topicName) => Task.CompletedTask;

    public Task DeleteSubscriptionAsync(string topicName, string subscriptionName) => Task.CompletedTask;

    private static SubscriptionInfo Copy(SubscriptionInfo s) => new()
    {
        Name = s.Name,
        TopicName = s.TopicName,
        ActiveMessageCount = s.ActiveMessageCount,
        DeadLetterMessageCount = s.DeadLetterMessageCount,
        TransferMessageCount = s.TransferMessageCount,
    };
}

public sealed class FakeMessageService : IMessageService
{
    public List<MessageInfo> Messages { get; } = [];
    public List<MessageSource> Purged { get; } = [];
    public List<MessageSource> Peeked { get; } = [];

    private const int PageSize = 100;

    /// <summary>When set, peeking pauses after the first page until it completes.</summary>
    public TaskCompletionSource? PausePeek { get; set; }

    public async IAsyncEnumerable<IReadOnlyList<MessageInfo>> PeekAsync(MessageSource source, int count, long? fromSequenceNumber = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        Peeked.Add(source);
        var pages = Messages.Where(m => fromSequenceNumber is null || m.SequenceNumber >= fromSequenceNumber).Take(count).Chunk(PageSize);
        var first = true;
        foreach (var page in pages)
        {
            if (!first && PausePeek is { } pause) await pause.Task.WaitAsync(ct);
            first = false;
            await Task.Yield();
            yield return page;
        }
    }

    public async IAsyncEnumerable<IReadOnlyList<MessageInfo>> ReceiveAndDeleteAsync(MessageSource source, int count, [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var page in Messages.Take(count).Chunk(PageSize))
        {
            await Task.Yield();
            yield return [.. page.Select(m => new MessageInfo(m.Raw, MessageState.Received))];
        }
    }

    // Completes run in parallel, so recording must be thread-safe.
    public System.Collections.Concurrent.ConcurrentQueue<long> Completed { get; } = [];

    /// <summary>When set, the lock scan stops after <see cref="PauseScanAfter"/> messages until it completes.</summary>
    public TaskCompletionSource? PauseScan { get; set; }
    public int PauseScanAfter { get; set; }

    /// <summary>Sequence numbers someone else holds a lock on: the lock scan cannot get them.</summary>
    public HashSet<long> LockedElsewhere { get; } = [];

    public ILockedMessages StartReceiving(MessageSource source, int count, System.Threading.Channels.ChannelWriter<ServiceBusReceivedMessage> received, CancellationToken ct = default)
    {
        var batch = Messages.Take(count).Select(m => m.Raw).ToList();
        _ = Task.Run(async () =>
        {
            foreach (var m in batch) await received.WriteAsync(m, ct);
            received.TryComplete();
        }, ct);
        return new FakeLockedMessages(this, batch);
    }

    public ILockedMessages StartLocking(MessageSource source, IReadOnlyCollection<long> sequenceNumbers, System.Threading.Channels.ChannelWriter<ServiceBusReceivedMessage> found, CancellationToken ct = default)
    {
        var lockable = Messages.Where(m => sequenceNumbers.Contains(m.SequenceNumber) && !LockedElsewhere.Contains(m.SequenceNumber)).ToList();
        _ = Task.Run(async () =>
        {
            try
            {
                var written = 0;
                foreach (var m in lockable)
                {
                    if (written++ == PauseScanAfter && PauseScan is { } pause) await pause.Task.WaitAsync(ct);
                    await found.WriteAsync(m.Raw, ct);
                }
                found.TryComplete();
            }
            catch (Exception ex)
            {
                found.TryComplete(ex);
            }
        }, CancellationToken.None);
        return new FakeLockedMessages(this, lockable.Select(m => m.Raw));
    }

    public List<(MessageSource Source, long SequenceNumber)> Sent { get; } = [];

    /// <summary>When set, sending waits on it, so a test can see an operation in progress.</summary>
    public TaskCompletionSource? HoldSend { get; set; }

    public async Task SendCopiesAsync(MessageSource source, IReadOnlyList<MessageInfo> messages, Action<int> onSent, CancellationToken ct = default)
    {
        if (HoldSend is { } hold) await hold.Task.WaitAsync(ct);
        Sent.AddRange(messages.Select(m => (source, m.SequenceNumber)));
        onSent(messages.Count);
    }

    public Task<int> PurgeMessagesAsync(MessageSource source, Action<int> onPurged, CancellationToken ct = default)
    {
        Purged.Add(source);
        onPurged(Messages.Count);
        return Task.FromResult(Messages.Count);
    }
}

public sealed class FakeLockedMessages(FakeMessageService service, IEnumerable<ServiceBusReceivedMessage> messages) : ILockedMessages
{
    private readonly HashSet<long> _held = [.. messages.Select(m => m.SequenceNumber)];

    public event Action<long>? LockLost { add { } remove { } }

    public bool Disposed { get; private set; }

    public bool IsHeld(long sequenceNumber)
    {
        lock (_held) return _held.Contains(sequenceNumber);
    }

    public Task CompleteAsync(MessageInfo message, CancellationToken ct = default)
    {
        Settle(message);
        service.Completed.Enqueue(message.SequenceNumber);
        return Task.CompletedTask;
    }

    public Task AbandonAsync(MessageInfo message, CancellationToken ct = default)
    {
        Settle(message);
        return Task.CompletedTask;
    }

    public Task DeadLetterAsync(MessageInfo message, string reason, CancellationToken ct = default)
    {
        Settle(message);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }

    private void Settle(MessageInfo message)
    {
        bool removed;
        lock (_held) removed = _held.Remove(message.SequenceNumber);
        if (!removed)
            throw new InvalidOperationException($"Message #{message.SequenceNumber} is no longer locked");
    }
}
