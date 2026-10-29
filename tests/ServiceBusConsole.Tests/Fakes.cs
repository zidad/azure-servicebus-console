using System.Runtime.CompilerServices;
using System.Threading.Channels;

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

    /// <summary>Changes a queue while a page may be polling.</summary>
    public void Update(string name, Func<QueueInfo, QueueInfo> change)
    {
        lock (Queues) Queues[Queues.FindIndex(q => q.Name == name)] = change(Queues.Single(q => q.Name == name));
    }

    public void Add(QueueInfo queue)
    {
        lock (Queues) Queues.Add(queue);
    }

    public async IAsyncEnumerable<QueueInfo> GetQueuesAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        Loads++;
        await Task.Yield();
        if (LoadError is not null) throw LoadError;
        List<QueueInfo> queues;
        lock (Queues) queues = [.. Queues];
        foreach (var q in queues) yield return q;
    }

    public Task DeleteQueueAsync(string queueName, CancellationToken ct = default)
    {
        Deleted.Add(queueName);
        lock (Queues) Queues.RemoveAll(q => q.Name == queueName);
        return Task.CompletedTask;
    }
}

public sealed class FakeTopicService : ITopicService
{
    public List<TopicInfo> Topics { get; } = [];
    public List<SubscriptionInfo> Subscriptions { get; } = [];

    /// <summary>Changes the topic list while a page may be polling.</summary>
    public void Change(Action<List<TopicInfo>> change)
    {
        lock (Topics) change(Topics);
    }

    public async IAsyncEnumerable<TopicInfo> GetTopicsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        List<TopicInfo> topics;
        lock (Topics) topics = [.. Topics];
        foreach (var t in topics) yield return t;
    }

    public async IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(string topicName, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        foreach (var s in Subscriptions.Where(s => s.TopicName == topicName).ToList()) yield return s;
    }

    public async IAsyncEnumerable<SubscriptionInfo> GetAllSubscriptionsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        foreach (var s in Subscriptions.ToList()) yield return s;
    }

    public Task DeleteTopicAsync(string topicName, CancellationToken ct = default) => Task.CompletedTask;

    public Task DeleteSubscriptionAsync(string topicName, string subscriptionName, CancellationToken ct = default) => Task.CompletedTask;
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
            yield return [.. page.Select(m => m with { State = MessageState.Received })];
        }
    }

    // Completes run in parallel, so recording must be thread-safe.
    public System.Collections.Concurrent.ConcurrentQueue<long> Completed { get; } = [];

    /// <summary>When set, the lock scan stops after <see cref="PauseScanAfter"/> messages until it completes.</summary>
    public TaskCompletionSource? PauseScan { get; set; }
    public int PauseScanAfter { get; set; }

    /// <summary>Sequence numbers someone else holds a lock on: the lock scan cannot get them.</summary>
    public HashSet<long> LockedElsewhere { get; } = [];

    public ILockedMessages StartReceiving(MessageSource source, int count, CancellationToken ct = default)
    {
        var batch = Messages.Take(count).Select(m => m with { State = MessageState.Locked }).ToList();
        return new FakeLockedMessages(this, batch, batch.ToAsyncEnumerable());
    }

    public ILockedMessages StartLocking(MessageSource source, IReadOnlyCollection<long> sequenceNumbers, CancellationToken ct = default)
    {
        var lockable = Messages.Where(m => sequenceNumbers.Contains(m.SequenceNumber) && !LockedElsewhere.Contains(m.SequenceNumber))
            .Select(m => m with { State = MessageState.Locked }).ToList();
        return new FakeLockedMessages(this, lockable, ScanAsync(lockable, ct));
    }

    private async IAsyncEnumerable<MessageInfo> ScanAsync(IReadOnlyList<MessageInfo> lockable, [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var (i, m) in lockable.Index())
        {
            if (i == PauseScanAfter && PauseScan is { } pause) await pause.Task.WaitAsync(ct);
            yield return m;
        }
    }

    public List<(MessageSource Source, long SequenceNumber)> Sent { get; } = [];

    /// <summary>When set, sending waits on it, so a test can see an operation in progress.</summary>
    public TaskCompletionSource? HoldSend { get; set; }

    public async IAsyncEnumerable<int> SendCopiesAsync(MessageSource source, IReadOnlyList<MessageInfo> messages, [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (HoldSend is { } hold) await hold.Task.WaitAsync(ct);
        Sent.AddRange(messages.Select(m => (source, m.SequenceNumber)));
        yield return messages.Count;
    }

    public async IAsyncEnumerable<int> PurgeAsync(MessageSource source, [EnumeratorCancellation] CancellationToken ct = default)
    {
        Purged.Add(source);
        await Task.Yield();
        yield return Messages.Count;
    }
}

/// <summary>Holds <paramref name="messages"/> from the start; <paramref name="locking"/> is what arrives on <see cref="Held"/>.</summary>
public sealed class FakeLockedMessages(FakeMessageService service, IEnumerable<MessageInfo> messages, IAsyncEnumerable<MessageInfo> locking) : ILockedMessages
{
    private readonly HashSet<long> _held = [.. messages.Select(m => m.SequenceNumber)];
    private readonly Channel<long> _lockLost = Channel.CreateUnbounded<long>();

    public ChannelReader<MessageInfo> Held { get; } = Pump(locking);

    public ChannelReader<long> LockLost => _lockLost.Reader;

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
        _lockLost.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    private static ChannelReader<MessageInfo> Pump(IAsyncEnumerable<MessageInfo> locking)
    {
        var channel = Channel.CreateUnbounded<MessageInfo>();
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var m in locking) await channel.Writer.WriteAsync(m);
                channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
            }
        });
        return channel.Reader;
    }

    private void Settle(MessageInfo message)
    {
        bool removed;
        lock (_held) removed = _held.Remove(message.SequenceNumber);
        if (!removed)
            throw new InvalidOperationException($"Message #{message.SequenceNumber} is no longer locked");
    }
}
